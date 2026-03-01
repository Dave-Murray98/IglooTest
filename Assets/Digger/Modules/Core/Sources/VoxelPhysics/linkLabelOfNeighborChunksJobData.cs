using Unity.Jobs;

namespace Digger.Modules.Core.Sources.VoxelPhysics
{
    public struct LinkLabelOfNeighborChunksXJobData
    {
        public LinkLabelOfNeighborChunksXJob Job;
        public JobHandle Handle;
        public VoxelChunk Chunk1;
        public VoxelChunk Chunk2;
    }

    public struct LinkLabelOfNeighborChunksYJobData
    {
        public LinkLabelOfNeighborChunksYJob Job;
        public JobHandle Handle;
        public VoxelChunk Chunk1;
        public VoxelChunk Chunk2;
    }

    public struct LinkLabelOfNeighborChunksZJobData
    {
        public LinkLabelOfNeighborChunksZJob Job;
        public JobHandle Handle;
        public VoxelChunk Chunk1;
        public VoxelChunk Chunk2;
    }
}