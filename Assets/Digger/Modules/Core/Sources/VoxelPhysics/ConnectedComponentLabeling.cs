using Unity.Collections;
using UnityEngine;
using Unity.Mathematics;

namespace Digger.Modules.Core.Sources.VoxelPhysics
{
    public static class ConnectedComponentLabeling
    {
        public const int GROUND_LABEL = -1;
        public const int OUTSIDE_LABEL = -2;

        public static ConnectedComponentLabelingJob Do(VoxelChunk chunk)
        {
            var job = new ConnectedComponentLabelingJob
            {
                SizeVox = chunk.SizeVox,
                SizeVox2 = chunk.SizeVox * chunk.SizeVox,
                ChunkVoxelPosition = chunk.VoxelPosition.ToInt3(),
                Voxels = new NativeArray<Voxel>(chunk.VoxelArray, Allocator.Persistent),
                Labels = new NativeArray<int>(chunk.VoxelArray.Length, Allocator.Persistent),
                QueuedVoxelIndices = new NativeQueue<int>(Allocator.Persistent),
                LabelMap = new NativeParallelHashMap<int, ConnectedComponentLabeling.AABB>(chunk.SizeVox * chunk.SizeVox, Allocator.Persistent),
            };
            return job;
        }

        public static void Complete(ConnectedComponentLabelingJob job, VoxelChunk chunk)
        {
            job.Labels.CopyTo(chunk.LabelArray);
            LinkLabelOfNeighborChunks.NativeAABBHashMapToDictionary(job.LabelMap, chunk.LabelMap);

            job.Voxels.Dispose();
            job.Labels.Dispose();
            job.QueuedVoxelIndices.Dispose();
            job.LabelMap.Dispose();
        }

        public struct AABB
        {
            public int3 Min;
            public int3 Max;

            public void Expand(int3 position)
            {
                Min = math.min(Min, position);
                Max = math.max(Max, position);
            }

            public int GreatestSideLength => math.max(Max.x - Min.x, math.max(Max.y - Min.y, Max.z - Min.z));
        }
    }
}