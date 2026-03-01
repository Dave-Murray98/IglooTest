using System;
using System.Collections.Generic;
using Digger.Modules.Core.Sources.Jobs;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Digger.Modules.Core.Sources.Generators
{
    [CreateAssetMenu(fileName = "AdvancedVoxelGenerator", menuName = "Digger/Voxel Generators/Advanced Generator", order = 2)]
    public class AdvancedVoxelGenerator : ScriptableObject, IVoxelGenerator
    {
        [Serializable]
        public enum NoiseBlendMode
        {
            Replace = 0,
            Add = 1
        }

        [Serializable]
        public class DepthLayer
        {
            [Tooltip("Minimum depth below surface (in world units) where this texture starts to appear")]
            public float minDepth = 5f;
            
            [Tooltip("Texture index to use for this depth range")]
            public int textureIndex = 1;
            
            [Tooltip("Whether voxels in this layer can be destroyed")]
            public bool destructible = true;
        }

        [Serializable]
        public class NoiseLayer
        {
            [Tooltip("Scale of the Perlin noise. Higher values = larger patterns")]
            [Range(1f, 100f)]
            public float scale = 15f;
            
            [Tooltip("Number of noise octaves. More octaves = more detail")]
            [Range(1, 4)]
            public int octaves = 2;
            
            [Tooltip("Persistence of noise between octaves. Higher = rougher")]
            [Range(0.1f, 1f)]
            public float persistence = 0.5f;
            
            [Tooltip("How this layer combines with previous layers")]
            public NoiseBlendMode blendMode = NoiseBlendMode.Add;
            
            [Tooltip("Noise threshold with smooth transition. Effect gradually fades in/out around this value (normalized -1 to 1)")]
            [Range(-1f, 1f)]
            public float threshold = -0.2f;

            [Tooltip("Texture index override (-1 for no override)")]
            public int textureIndex = -1;

            [Tooltip("Whether voxels affected by this noise layer can be destroyed")]
            public bool destructible = true;
        }
        
        [Header("Depth-Based Layers")]
        [Tooltip("Texture and destructibility layers based on depth below surface. Sorted by depth (deepest first).")]
        public List<DepthLayer> depthLayers = new List<DepthLayer>
        {
            new DepthLayer { minDepth = 0f, textureIndex = 0, destructible = true }
        };

        [Header("Noise-Based Layers")]
        [Tooltip("Procedural layers based on Perlin noise. Applied in order after depth layers.")]
        public List<NoiseLayer> noiseLayers = new List<NoiseLayer>();

        public JobHandle GenerateVoxels(
            float[] heightArray,
            int3 chunkVoxelPosition,
            int sizeVox,
            float3 heightmapScale,
            NativeArray<float> heights,
            NativeArray<Voxel> voxels,
            bool refreshOnly)
        {
            // Sort depth layers by depth (highest depth first)
            var sortedDepthLayers = new List<DepthLayer>(depthLayers);
            sortedDepthLayers.Sort((a, b) => b.minDepth.CompareTo(a.minDepth));
            
            // Prepare depth layer data (max 8 layers for performance)
            var maxDepthLayers = Math.Min(sortedDepthLayers.Count, 8);
            var depthThresholds = new NativeArray<float>(8, Allocator.TempJob);
            var depthTextures = new NativeArray<int>(8, Allocator.TempJob);
            var depthDestructible = new NativeArray<float>(8, Allocator.TempJob);
            
            for (int i = 0; i < maxDepthLayers; i++)
            {
                depthThresholds[i] = sortedDepthLayers[i].minDepth;
                depthTextures[i] = sortedDepthLayers[i].textureIndex;
                depthDestructible[i] = sortedDepthLayers[i].destructible ? 0f : 1f;
            }
            
            // Prepare noise layer data (max 8 layers for performance)
            var maxNoiseLayers = Math.Min(noiseLayers.Count, 8);
            var noiseScales = new NativeArray<float>(8, Allocator.TempJob);
            var noiseOctaves = new NativeArray<int>(8, Allocator.TempJob);
            var noisePersistences = new NativeArray<float>(8, Allocator.TempJob);
            var noiseDestructible = new NativeArray<float>(8, Allocator.TempJob);
            var noiseTextureIndices = new NativeArray<int>(8, Allocator.TempJob);
            var noiseBlendModes = new NativeArray<int>(8, Allocator.TempJob);
            var noiseThresholds = new NativeArray<float>(8, Allocator.TempJob);
            
            for (int i = 0; i < maxNoiseLayers; i++)
            {
                noiseScales[i] = noiseLayers[i].scale;
                noiseOctaves[i] = noiseLayers[i].octaves;
                noisePersistences[i] = noiseLayers[i].persistence;
                noiseDestructible[i] = noiseLayers[i].destructible ? 0f : 1f;
                noiseTextureIndices[i] = noiseLayers[i].textureIndex;
                noiseBlendModes[i] = (int)noiseLayers[i].blendMode;
                noiseThresholds[i] = noiseLayers[i].threshold;
            }
            
            var jobData = new AdvancedVoxelGenerationJob
            {
                RefreshOnly = refreshOnly ? 1 : 0,
                ChunkVoxelPosition = chunkVoxelPosition,
                Heights = heights,
                Voxels = voxels,
                SizeVox = sizeVox,
                SizeVox2 = sizeVox * sizeVox,
                HeightmapScale = heightmapScale,
                
                DepthLayerCount = maxDepthLayers,
                DepthThresholds = depthThresholds,
                DepthTextures = depthTextures,
                DepthDestructible = depthDestructible,
                
                NoiseLayerCount = maxNoiseLayers,
                NoiseScales = noiseScales,
                NoiseOctaves = noiseOctaves,
                NoisePersistences = noisePersistences,
                NoiseDestructible = noiseDestructible,
                NoiseTextureIndices = noiseTextureIndices,
                NoiseBlendModes = noiseBlendModes,
                NoiseThresholds = noiseThresholds
            };

            var handle = jobData.Schedule(voxels.Length, 64);
            
            // Dispose arrays after job completes
            handle = depthThresholds.Dispose(handle);
            handle = depthTextures.Dispose(handle);
            handle = depthDestructible.Dispose(handle);
            handle = noiseScales.Dispose(handle);
            handle = noiseOctaves.Dispose(handle);
            handle = noisePersistences.Dispose(handle);
            handle = noiseDestructible.Dispose(handle);
            handle = noiseTextureIndices.Dispose(handle);
            handle = noiseBlendModes.Dispose(handle);
            handle = noiseThresholds.Dispose(handle);
            
            return handle;
        }
    }
}
