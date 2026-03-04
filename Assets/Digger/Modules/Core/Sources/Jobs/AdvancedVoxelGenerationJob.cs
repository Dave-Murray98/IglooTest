using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Digger.Modules.Core.Sources.Jobs
{
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
    public struct AdvancedVoxelGenerationJob : IJobParallelFor
    {
        public int3 ChunkVoxelPosition;
        public int SizeVox;
        public int SizeVox2;
        public float3 HeightmapScale;
        public int RefreshOnly;
        
        // Depth layer data
        public int DepthLayerCount;
        [ReadOnly] public NativeArray<float> DepthThresholds;
        [ReadOnly] public NativeArray<int> DepthTextures;
        [ReadOnly] public NativeArray<float> DepthDestructible;
        
        // Noise layer data
        public int NoiseLayerCount;
        [ReadOnly] public NativeArray<float> NoiseScales;
        [ReadOnly] public NativeArray<int> NoiseOctaves;
        [ReadOnly] public NativeArray<float> NoisePersistences;
        [ReadOnly] public NativeArray<float> NoiseDestructible;
        [ReadOnly] public NativeArray<int> NoiseTextureIndices;
        [ReadOnly] public NativeArray<int> NoiseBlendModes; // 0=Replace, 1=Add
        [ReadOnly] public NativeArray<float> NoiseThresholds;

        [ReadOnly] [NativeDisableParallelForRestriction]
        public NativeArray<float> Heights;

        public NativeArray<Voxel> Voxels;

        public void Execute(int index)
        {
            if (RefreshOnly == 1 && Voxels[index].IsAlteredFarOrNearSurface)
                return;
            
            var pi = Utils.IndexToXYZ(index, SizeVox, SizeVox2);
            var height = Heights[Utils.XYZToHeightIndex(pi, SizeVox)];
            var p = Utils.ChunkVoxelToUnityPosition(ChunkVoxelPosition, pi, HeightmapScale);
            var voxelAltitude = p.y;

            if (RefreshOnly == 1 && !Voxels[index].IsAlteredFarOrNearSurface) {
                Voxels[index].SetValue(p.y - height, HeightmapScale.y);
            } else {
                // Calculate SDF value
                var voxel = new Voxel(voxelAltitude - height, HeightmapScale.y);
                
                // Compute smoothed strength and texture index
                var strength = ComputeStrengthAt(pi);
                var textureIndex = ComputeTextureIndexAt(pi);
                
                // Set final voxel properties
                voxel.SetMaxValue(HeightmapScale.y - 2 * strength * HeightmapScale.y, HeightmapScale.y);
                voxel.AddTexture(textureIndex, 1f);
                
                Voxels[index] = voxel;
            }
        }
        
        /// <summary>
        /// Computes the strength value at a specific world position (x, y, z)
        /// </summary>
        private float ComputeStrengthAt(int3 pi)
        {
            var height = Heights[Utils.XYZToHeightIndex(pi, SizeVox)];
            var p = Utils.ChunkVoxelToUnityPosition(ChunkVoxelPosition, pi, HeightmapScale);
            var depthBelowSurface = height - p.y;
            
            float strength = 0f;
            
            // ===== STEP 1: Apply Depth Layers =====
            if (DepthLayerCount > 0 && depthBelowSurface > 0)
            {
                for (int i = 0; i < DepthLayerCount && i < 8; i++)
                {
                    if (depthBelowSurface >= DepthThresholds[i])
                    {
                        // Add a smooth transition near the depth threshold
                        strength = math.lerp(strength, NoiseDestructible[i], math.clamp((depthBelowSurface - DepthThresholds[i]) / HeightmapScale.y, 0f, 1f));
                        break;
                    }
                }
            }
            
            // ===== STEP 2: Apply Noise Layers =====
            for (int i = 0; i < NoiseLayerCount && i < 8; i++)
            {
                var noiseValue = Noise3D(
                    p / NoiseScales[i], 
                    NoiseOctaves[i], 
                    NoisePersistences[i]);

                // Add a smooth transition near the surface
                noiseValue += math.saturate(0.8f * depthBelowSurface / HeightmapScale.y) - 1f;
                
                var threshold = NoiseThresholds[i];
                var influence = math.clamp(noiseValue - threshold, -1f, 1f);
                
                var layerStrength = NoiseDestructible[i];
                var blendMode = NoiseBlendModes[i];
                
                switch (blendMode)
                {
                    case 0: // Replace
                        strength = math.lerp(strength, layerStrength, influence);
                        break;
                    case 1: // Add
                        var addAmount = layerStrength * influence;
                        strength = math.clamp(strength + addAmount, 0f, 1f);
                        break;
                }
            }
            
            return strength;
        }
        
        /// <summary>
        /// Computes the texture index at a specific world position (x, y, z)
        /// </summary>
        private uint ComputeTextureIndexAt(int3 pi)
        {
            var height = Heights[Utils.XYZToHeightIndex(pi, SizeVox)];
            var p = Utils.ChunkVoxelToUnityPosition(ChunkVoxelPosition, pi, HeightmapScale);
            var depthBelowSurface = height - p.y;
            
            uint textureIndex = 0;
            
            // ===== STEP 1: Apply Depth Layers =====
            if (DepthLayerCount > 0 && depthBelowSurface > 0)
            {
                for (int i = 0; i < DepthLayerCount && i < 8; i++)
                {
                    if (depthBelowSurface >= DepthThresholds[i])
                    {
                        textureIndex = (uint)DepthTextures[i];
                        break;
                    }
                }
            }
            
            // ===== STEP 2: Apply Noise Layers (texture override) =====
            for (int i = 0; i < NoiseLayerCount && i < 8; i++)
            {
                if (NoiseTextureIndices[i] >= 0)
                {
                    var noiseValue = Noise3D(
                        p / NoiseScales[i], 
                        NoiseOctaves[i], 
                        NoisePersistences[i]);
                    
                    var threshold = NoiseThresholds[i];
                    var influence = math.clamp(noiseValue - threshold, -1f, 1f);
                    
                    if (influence > 0.001f)
                    {
                        textureIndex = (uint)NoiseTextureIndices[i];
                    }
                }
            }
            
            return textureIndex;
        }
        
        /// <summary>
        /// 3D Perlin noise with multiple octaves
        /// </summary>
        private float Noise3D(float3 position, int octaves, float persistence)
        {
            float total = 0f;
            float frequency = 1f;
            float amplitude = 1f;
            float maxValue = 0f;
            
            for (int i = 0; i < octaves; i++)
            {
                total += noise.snoise(position * frequency) * amplitude;
                maxValue += amplitude;
                amplitude *= persistence;
                frequency *= 2f;
            }
            
            return total / maxValue;
        }
    }
}
