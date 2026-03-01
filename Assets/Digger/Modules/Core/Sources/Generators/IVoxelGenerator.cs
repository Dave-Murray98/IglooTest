using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Digger.Modules.Core.Sources.Generators
{
    public interface IVoxelGenerator
    {
        /// <summary>
        /// Generates voxels for a chunk.
        /// </summary>
        /// <param name="heightArray">Array of heights from the terrain heightmap</param>
        /// <param name="chunkPosition">Position of this chunk in chunk coordinates</param>
        /// <param name="sizeVox">Size of voxel grid</param>
        /// <param name="heightmapScale">Scale of the heightmap</param>
        /// <param name="heights">Native array to read heights from</param>
        /// <param name="voxels">Native array to write voxels to</param>
        /// <returns>JobHandle for the generation job</returns>
        JobHandle GenerateVoxels(
            float[] heightArray,
            int3 chunkPosition,
            int sizeVox,
            float3 heightmapScale,
            NativeArray<float> heights,
            NativeArray<Voxel> voxels,
            bool refreshOnly);
    }
}

