using System.Collections.Generic;
using System.Threading;
using Unity.Collections;

namespace Digger.Modules.Core.Sources.VoxelPhysics
{
    public static class RemoveFloatingVoxels
    {
        private static readonly ThreadLocal<HashSet<int>> LabelsToRemove = new ThreadLocal<HashSet<int>>(() => new HashSet<int>());

        public static RemoveFloatingVoxelsJob Do(VoxelChunk chunk)
        {
            var job = new RemoveFloatingVoxelsJob
            {
                Voxels = new NativeArray<Voxel>(chunk.VoxelArray, Allocator.Persistent),
                Labels = new NativeArray<int>(chunk.LabelArray, Allocator.Persistent),
                LabelsConnectedToTheGround = LinkLabelOfNeighborChunks.HashSetToNativeHashSet(chunk.SizeVox * chunk.SizeVox, chunk.LabelsConnectedToTheGroundThroughNeighbors),
                Heights = new NativeArray<float>(chunk.HeightArray, Allocator.Persistent),
                ChunkAltitude = chunk.WorldPosition.y,
                HeightmapScale = chunk.HeightmapScale,
                SizeVox = chunk.SizeVox,
                SizeVox2 = chunk.SizeVox * chunk.SizeVox,
                Holes = new NativeArray<int>(chunk.HolesArray, Allocator.Persistent),
                NewHolesConcurrentCounter = new NativeArray<int>(1, Allocator.Persistent),
            };
            return job;
        }

        public static void Complete(RemoveFloatingVoxelsJob job, VoxelChunk chunk)
        {
            job.Voxels.CopyTo(chunk.VoxelArray);
            job.Labels.CopyTo(chunk.LabelArray);
            LabelsToRemove.Value.Clear();
            foreach (var label in chunk.LabelMap.Keys)
            {
                if (!chunk.LabelsConnectedToTheGroundThroughNeighbors.Contains(label))
                {
                    LabelsToRemove.Value.Add(label);
                }
            }

            foreach (var label in LabelsToRemove.Value)
            {
                chunk.LabelMap.Remove(label);
            }

            var cutter = chunk.Cutter;
            if (job.NewHolesConcurrentCounter[0] > 0)
            {
                cutter.Cut(job.Holes, chunk.VoxelPosition, chunk.ChunkPosition);
            }

            job.Voxels.Dispose();
            job.Labels.Dispose();
            job.LabelsConnectedToTheGround.Dispose();
            job.Heights.Dispose();
            job.Holes.Dispose();
            job.NewHolesConcurrentCounter.Dispose();
        }
    }
}