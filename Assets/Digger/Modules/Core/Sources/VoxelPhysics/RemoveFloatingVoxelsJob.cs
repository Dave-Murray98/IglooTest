using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Digger.Modules.Core.Sources.VoxelPhysics
{
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
    public struct RemoveFloatingVoxelsJob : IJobParallelFor
    {
        [WriteOnly] public NativeArray<Voxel> Voxels;
        public NativeArray<int> Labels;
        [ReadOnly] public NativeParallelHashSet<int> LabelsConnectedToTheGround;
        [ReadOnly] public NativeArray<float> Heights;
        public float ChunkAltitude;
        public float3 HeightmapScale;
        public int SizeVox;
        public int SizeVox2;
        [WriteOnly] public NativeArray<int> Holes;

        [WriteOnly]
        [NativeDisableParallelForRestriction]
        public NativeArray<int> NewHolesConcurrentCounter;

        public void Execute(int index)
        {
            var label = Labels[index];
            if (label > 0 && !LabelsConnectedToTheGround.Contains(label))
            {
                var pi = Utils.IndexToXYZ(index, SizeVox, SizeVox2);
                var p = pi * HeightmapScale;

                var voxel = new Voxel(HeightmapScale.y, HeightmapScale.y);
                voxel.Alteration = Voxel.FarAboveSurface;
                Voxels[index] = voxel;
                Labels[index] = ConnectedComponentLabeling.OUTSIDE_LABEL;

                if (Utils.IsOnSurface(pi, HeightmapScale.y, p.y + ChunkAltitude, SizeVox, Heights))
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
            }
        }
    }
}