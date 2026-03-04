using System;
using Digger.Modules.Core.Sources.NativeCollections;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Digger.Modules.Core.Sources.Jobs
{
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
    public struct VoxelModificationJob : IJobParallelFor, IDisposable
    {
        private const long DoubleToLongMultiplier = 1000000;
        
        public int SizeVox;
        public int SizeVox2;
        public BrushType Brush;
        public ActionType Action;
        public bool PaintWhileDigging;
        public bool BypassDestructability;
        public float3 HeightmapScale;
        public float3 Center;
        public float3 Size;
        public bool UpsideDown;
        public float Intensity;
        public bool IsTargetIntensity;
        public float ChunkAltitude;
        public uint TextureIndex;
        public bool IsIndestructible;

        [ReadOnly]
        [NativeDisableParallelForRestriction]
        public NativeArray<float> Heights;

        [ReadOnly]
        [NativeDisableParallelForRestriction]
        public NativeArray<Voxel> InputVoxels;
        public int3 InputSizeVox;
        public int3 InputOriginVox;

        public NativeArray<Voxel> Voxels;

        [WriteOnly]
        [NativeDisableParallelForRestriction]
        public NativeArray<int> Holes;

        [WriteOnly]
        [NativeDisableParallelForRestriction]
        public NativeArray<int> NewHolesConcurrentCounter;

        [NativeDisableParallelForRestriction]
        public NativeArray<long> RemovedMatterCounter;

        [NativeDisableParallelForRestriction]
        public NativeArray<long> AddedMatterCounter;

        [NativeDisableParallelForRestriction]
        public NativeArray<int> ModifiedVoxelCounter;

        private double coneAngle;
        private float upsideDownSign;

        public void PostConstruct()
        {
            if (Size.y > 0.1f)
                coneAngle = Math.Atan((double)Size.x / Size.y);
            upsideDownSign = UpsideDown ? -1f : 1f;
        }

        public void Execute(int index)
        {
            var pi = Utils.IndexToXYZ(index, SizeVox, SizeVox2);
            var p = pi * HeightmapScale;
            var terrainHeight = Heights[Utils.XYZToHeightIndex(pi, SizeVox)];
            var terrainHeightValue = p.y + ChunkAltitude - terrainHeight;

            float distance;
            switch (Brush)
            {
                case BrushType.Sphere:
                    distance = ComputeSphereDistances(p);
                    break;
                case BrushType.HalfSphere:
                    distance = ComputeHalfSphereDistances(p);
                    break;
                case BrushType.RoundedCube:
                    distance = ComputeCubeDistances(p);
                    break;
                case BrushType.Stalagmite:
                    distance = ComputeConeDistances(p);
                    break;
                case BrushType.Custom:
                    distance = GetValueFromInputVoxels(pi);
                    break;
                default:
                    return; // never happens
            }

            Voxel voxel;
            switch (Action)
            {
                case ActionType.Add:
                case ActionType.Dig:
                    voxel = ApplyDigAdd(index, Action == ActionType.Dig, distance);
                    break;
                case ActionType.Paint:
                    voxel = ApplyPaint(index, distance);
                    break;
                case ActionType.PaintHoles:
                    voxel = ApplyPaintHoles(index, pi, p, distance);
                    break;
                case ActionType.Reset:
                    voxel = ApplyResetBrush(index, pi, p, distance);
                    break;
                default:
                    return; // never happens
            }


            if (voxel.Alteration != Voxel.Unaltered)
            {
                voxel = Utils.AdjustAlteration(voxel, pi, HeightmapScale.y, p.y + ChunkAltitude, terrainHeightValue, SizeVox, Heights);
            }

            if (Action != ActionType.Reset && (voxel.IsAlteredNearBelowSurface || voxel.IsAlteredNearAboveSurface))
            {
                NativeCollections.Utils.IncrementAt(NewHolesConcurrentCounter, 0);
                NativeCollections.Utils.IncrementAt(Holes, Utils.XZToHoleIndex(pi.x, pi.z, SizeVox));
                if (pi.x >= 1)
                {
                    NativeCollections.Utils.IncrementAt(Holes, Utils.XZToHoleIndex(pi.x - 1, pi.z, SizeVox));
                    if (pi.z >= 1)
                    {
                        NativeCollections.Utils.IncrementAt(Holes, Utils.XZToHoleIndex(pi.x - 1, pi.z - 1, SizeVox));
                    }
                }

                if (pi.z >= 1)
                {
                    NativeCollections.Utils.IncrementAt(Holes, Utils.XZToHoleIndex(pi.x, pi.z - 1, SizeVox));
                }
            }

            Voxels[index] = voxel;
        }

        private float GetValueFromInputVoxels(int3 pi)
        {
            var inputVoxPos = (int3)math.round((pi - InputOriginVox) / Size);
            if (inputVoxPos.x < 0 || inputVoxPos.x >= InputSizeVox.x ||
            inputVoxPos.y < 0 || inputVoxPos.y >= InputSizeVox.y ||
            inputVoxPos.z < 0 || inputVoxPos.z >= InputSizeVox.z)
            {
                return -100f;
            }
            return InputVoxels[inputVoxPos.x * InputSizeVox.y * InputSizeVox.z + inputVoxPos.y * InputSizeVox.z + inputVoxPos.z].Value;
        }


        private float ComputeSphereDistances(float3 p)
        {
            var radius = Size.x;
            var radiusHeightRatio = radius / math.max(Size.y, 0.01f);
            var vec = p - Center;
            var distance = math.sqrt(vec.x * vec.x + vec.y * vec.y * radiusHeightRatio * radiusHeightRatio + vec.z * vec.z);
            return radius - distance;
        }

        private float ComputeHalfSphereDistances(float3 p)
        {
            var vec = p - Center;
            var distance = math.sqrt(vec.x * vec.x + vec.y * vec.y + vec.z * vec.z);
            return math.min(Size.x - distance, vec.y);
        }

        private float ComputeCubeDistances(float3 p)
        {
            var vec = p - Center;
            return math.min(math.min(Size.x - math.abs(vec.x), Size.y - math.abs(vec.y)), Size.z - math.abs(vec.z));
        }

        private float ComputeConeDistances(float3 p)
        {
            var coneVertex = Center + new float3(0, upsideDownSign * Size.y * 0.95f, 0);
            var vec = p - coneVertex;
            var distance = math.sqrt(vec.x * vec.x + vec.y * vec.y + vec.z * vec.z);
            var flatDistance = math.sqrt(vec.x * vec.x + vec.z * vec.z);
            var pointAngle = Math.Asin((double)flatDistance / distance);
            var d = -distance * Math.Sin(math.abs(pointAngle - coneAngle)) * Math.Sign(pointAngle - coneAngle);
            return math.min(math.min((float)d, Size.y + upsideDownSign * vec.y), -upsideDownSign * vec.y);
        }

        private Voxel ApplyDigAdd(int index, bool dig, float distance)
        {
            var voxel = Voxels[index];
            var currentValF = voxel.Value;

            if (dig && currentValF <= 0.99f * HeightmapScale.y)
            {
                var maxValue = voxel.GetMaxValue(HeightmapScale.y);
                var newVal = math.select(math.max(currentValF, math.min(currentValF + Intensity * distance, maxValue)), 
                                         math.max(currentValF, currentValF + Intensity * distance),
                                         BypassDestructability);
                
                voxel.SetValue(newVal, HeightmapScale.y);
                if (distance >= 0)
                {
                    voxel.Alteration = Voxel.FarAboveSurface;
                    if (PaintWhileDigging)
                    {
                        if (!voxel.IsIndestructible || BypassDestructability)
                        {
                            voxel.AddTexture(TextureIndex, 1f);
                        }
                    }
                }
            }
            else if (!dig && currentValF >= -0.9f * HeightmapScale.y)
            {
                var newVal = math.min(currentValF, currentValF - Intensity * distance);
                voxel.SetValue(newVal, HeightmapScale.y);
                if (distance >= 0)
                {
                    voxel.Alteration = Voxel.FarAboveSurface;
                    if (PaintWhileDigging)
                    {
                        voxel.AddTexture(TextureIndex, 1f);
                        if (IsIndestructible)
                            voxel.SetMaxValue(voxel.Value, HeightmapScale.y);
                    }
                }
            }

            TrackMatterChange(currentValF, voxel.Value);

            return voxel;
        }

        private Voxel ApplyPaint(int index, float distance)
        {
            var voxel = Voxels[index];

            if (distance >= 0)
            {
                if (IsTargetIntensity)
                {
                    if (TextureIndex < 28)
                    {
                        voxel.SetTexture(TextureIndex, Intensity);
                    }
                    else if (TextureIndex == 28)
                    {
                        voxel.NormalizedWetnessWeight = Intensity;
                    }
                    else if (TextureIndex == 29)
                    {
                        voxel.NormalizedPuddlesWeight = Intensity;
                    }
                }
                else
                {
                    if (TextureIndex < 28)
                    {
                        voxel.AddTexture(TextureIndex, Intensity);
                    }
                    else if (TextureIndex == 28)
                    {
                        voxel.NormalizedWetnessWeight += Intensity;
                    }
                    else if (TextureIndex == 29)
                    {
                        voxel.NormalizedPuddlesWeight += Intensity;
                    }
                }
                if (IsIndestructible) {
                    voxel.SetMaxValue(voxel.Value, HeightmapScale.y);
                } else {
                    voxel.ResetMaxValue();
                }
            }

            return voxel;
        }

        private Voxel ApplyPaintHoles(int index, int3 pi, float3 p, float distance)
        {
            var voxel = Voxels[index];
            if (distance >= 0 && Intensity > 0 && voxel.Alteration != Voxel.Unaltered)
            {
                voxel.Alteration = Voxel.Hole;
            }
            else if (distance >= 0 && Intensity < 0 && voxel.Alteration == Voxel.Hole)
            {
                var onSurface = Utils.IsOnSurface(pi, HeightmapScale.y, p.y + ChunkAltitude, SizeVox, Heights);
                voxel.Alteration = onSurface ? Voxel.OnSurface : Voxel.FarAboveSurface;
            }

            return voxel;
        }

        private Voxel ApplyResetBrush(int index, int3 pi, float3 p, float distance)
        {
            if (distance >= 0)
            {
                var height = Heights[Utils.XYZToHeightIndex(pi, SizeVox)];
                var voxel = new Voxel(p.y + ChunkAltitude - height, HeightmapScale.y);
                if (Utils.IsOnSurface(pi, HeightmapScale.y, p.y + ChunkAltitude, SizeVox, Heights))
                {
                    NativeCollections.Utils.SetZeroAt(Holes, Utils.XZToHoleIndex(pi.x, pi.z, SizeVox));
                }

                return voxel;
            }

            return Voxels[index];
        }

        private void TrackMatterChange(float oldValue, float newValue)
        {
            if (oldValue == newValue)
                return;

            NativeCollections.Utils.IncrementAt(ModifiedVoxelCounter, 0);

            // Removed matter: voxel went from negative (inside terrain) to more positive (digging)
            if (oldValue < 0 && newValue > oldValue)
            {
                var change = (double)(newValue - oldValue);
                NativeCollections.Utils.InterlockedAddDouble(RemovedMatterCounter, 0, change, DoubleToLongMultiplier);
            }
            // Added matter: voxel went from positive (empty space) to more negative (adding)
            else if (oldValue > 0 && newValue < oldValue)
            {
                var change = (double)(oldValue - newValue);
                NativeCollections.Utils.InterlockedAddDouble(AddedMatterCounter, 0, change, DoubleToLongMultiplier);
            }
        }

        public void Dispose()
        {
            Heights.Dispose();
            Voxels.Dispose();
            Holes.Dispose();
        }
    }
}