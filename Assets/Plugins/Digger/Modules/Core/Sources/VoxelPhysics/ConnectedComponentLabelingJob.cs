using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Digger.Modules.Core.Sources.VoxelPhysics
{
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
    public struct ConnectedComponentLabelingJob : IJob
    {
        public int SizeVox;
        public int SizeVox2;
        public int3 ChunkVoxelPosition;

        [ReadOnly] public NativeArray<Voxel> Voxels;

        public NativeArray<int> Labels;

        public NativeQueue<int> QueuedVoxelIndices;
        public NativeParallelHashMap<int, ConnectedComponentLabeling.AABB> LabelMap;
        private int lastLabel;

        public void Execute()
        {
            lastLabel = 0;
            for (var index = 0; index < Voxels.Length; index++) {
                lastLabel++;
                Execute(index);
                while (QueuedVoxelIndices.TryDequeue(out var queuedVoxelIndex)) {
                    var pi = Utils.IndexToXYZ(queuedVoxelIndex, SizeVox, SizeVox2);

                    if (pi.x > 0)
                        Execute((pi.x - 1) * SizeVox2 + pi.y * SizeVox + pi.z);
                    if (pi.y > 0)
                        Execute(pi.x * SizeVox2 + (pi.y - 1) * SizeVox + pi.z);
                    if (pi.z > 0)
                        Execute(pi.x * SizeVox2 + pi.y * SizeVox + (pi.z - 1));
                    if (pi.x < SizeVox - 1)
                        Execute((pi.x + 1) * SizeVox2 + pi.y * SizeVox + pi.z);
                    if (pi.y < SizeVox - 1)
                        Execute(pi.x * SizeVox2 + (pi.y + 1) * SizeVox + pi.z);
                    if (pi.z < SizeVox - 1)
                        Execute(pi.x * SizeVox2 + pi.y * SizeVox + (pi.z + 1));
                }
            }
        }

        private void Execute(int index)
        {
            var voxel = Voxels[index];
            if (!voxel.IsInside) {
                Labels[index] = ConnectedComponentLabeling.OUTSIDE_LABEL;
                return;
            }

            if (Labels[index] == 0) {
                var aabb = LabelMap.TryGetValue(lastLabel, out var existingAabb)
                    ? existingAabb
                    : new ConnectedComponentLabeling.AABB {
                        Min = new int3(int.MaxValue),
                        Max = new int3(int.MinValue)
                    };
                aabb.Expand(Utils.IndexToWorldXYZ(index, SizeVox, SizeVox2, ChunkVoxelPosition));
                LabelMap[lastLabel] = aabb;
                Labels[index] = lastLabel;
                QueuedVoxelIndices.Enqueue(index);
            }
        }
    }
}