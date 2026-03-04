using Digger.Modules.Core.Sources.Jobs;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Digger.Modules.Core.Sources.Generators
{
    [CreateAssetMenu(fileName = "SimpleVoxelGenerator", menuName = "Digger/Voxel Generators/Simple Generator", order = 1)]
    public class SimpleVoxelGenerator : ScriptableObject, IVoxelGenerator
    {
        public JobHandle GenerateVoxels(
            float[] heightArray,
            int3 chunkPosition,
            int sizeVox,
            float3 heightmapScale,
            NativeArray<float> heights,
            NativeArray<Voxel> voxels,
            bool refreshOnly)
        {
            var jobData = new SimpleVoxelGenerationJob
            {
                RefreshOnly = refreshOnly ? 1 : 0,
                ChunkPosition = chunkPosition,
                Heights = heights,
                Voxels = voxels,
                SizeVox = sizeVox,
                SizeVox2 = sizeVox * sizeVox,
                HeightmapScale = heightmapScale,
            };

            return jobData.Schedule(voxels.Length, 64);
        }
    }
}

