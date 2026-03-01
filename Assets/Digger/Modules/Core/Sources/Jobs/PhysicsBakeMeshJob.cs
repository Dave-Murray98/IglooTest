using Unity.Burst;
using Unity.Jobs;

namespace Digger.Modules.Core.Sources.Jobs
{
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
    public struct PhysicsBakeMeshJob : IJob
    {
        public int MeshInstanceId;

        public void Execute()
        {
            UnityEngine.Physics.BakeMesh(MeshInstanceId, false);
        }
    }
}