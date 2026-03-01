using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Digger.Modules.Core.Sources.VoxelPhysics
{
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
    public struct LinkLabelOfNeighborChunksXJob : IJob
    {
        public int SizeVox;
        public int SizeVox2;

        [ReadOnly] public NativeArray<int> Labels1;
        [ReadOnly] public NativeArray<int> Labels2;

        [WriteOnly] public NativeParallelMultiHashMap<int, int> LinksFrom1To2;
        [WriteOnly] public NativeParallelMultiHashMap<int, int> LinksFrom2To1;

        [WriteOnly] public NativeParallelHashSet<int> LabelsConnectedToTheGround;

        public void Execute()
        {
            for (var y = 0; y < SizeVox; y++) {
                for (var z = 0; z < SizeVox; z++) {
                    var index1 = (SizeVox - 2) * SizeVox2 + y * SizeVox + z;
                    var index2 = 0 * SizeVox2 + y * SizeVox + z;

                    var label1 = Labels1[index1];
                    var label2 = Labels2[index2];

                    if (label1 > 0 && label2 > 0) {
                        LinksFrom1To2.Add(label1, label2);
                        LinksFrom2To1.Add(label2, label1);
                    } else if (label1 > 0 && label2 == 0) {
                        LabelsConnectedToTheGround.Add(label1);
                    } else if (label1 == 0 && label2 > 0) {
                        LabelsConnectedToTheGround.Add(label2);
                    }
                }
            }
        }
    }
}