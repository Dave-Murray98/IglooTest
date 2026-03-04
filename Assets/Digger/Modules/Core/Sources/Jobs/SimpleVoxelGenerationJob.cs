using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Digger.Modules.Core.Sources.Jobs
{
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
    public struct SimpleVoxelGenerationJob : IJobParallelFor
    {
        public int3 ChunkPosition;
        public int SizeVox;
        public int SizeVox2;
        public float3 HeightmapScale;

        [ReadOnly] [NativeDisableParallelForRestriction]
        public NativeArray<float> Heights;

        public NativeArray<Voxel> Voxels;
        public int RefreshOnly;
        
        public void Execute(int index)
        {
            if (RefreshOnly == 1 && Voxels[index].IsAlteredFarOrNearSurface)
                return;
            
            var pi = Utils.IndexToXYZ(index, SizeVox, SizeVox2);
            var height = Heights[Utils.XYZToHeightIndex(pi, SizeVox)];
            var p = Utils.ChunkVoxelToUnityPosition(ChunkPosition, pi, HeightmapScale);
            if (RefreshOnly == 1 && !Voxels[index].IsAlteredFarOrNearSurface) {
                Voxels[index].SetValue(p.y - height, HeightmapScale.y);
            } else {
                Voxels[index] = new Voxel(p.y - height, HeightmapScale.y);
            }
        }
    }
}

