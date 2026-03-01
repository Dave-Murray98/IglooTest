using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEngine;

namespace Digger.Modules.Core.Sources.VoxelPhysics
{
    public static class LinkLabelOfNeighborChunks
    {
        public static LinkLabelOfNeighborChunksXJob DoX(int sizeVox, VoxelChunk chunkLeft, VoxelChunk chunkRight)
        {
            var job = new LinkLabelOfNeighborChunksXJob
            {
                SizeVox = sizeVox,
                SizeVox2 = sizeVox * sizeVox,
                Labels1 = BuildChunkLabelNativeArray(sizeVox, chunkLeft),
                Labels2 = BuildChunkLabelNativeArray(sizeVox, chunkRight),
                LinksFrom1To2 = new NativeParallelMultiHashMap<int, int>(sizeVox * sizeVox, Allocator.Persistent),
                LinksFrom2To1 = new NativeParallelMultiHashMap<int, int>(sizeVox * sizeVox, Allocator.Persistent),
                LabelsConnectedToTheGround = new NativeParallelHashSet<int>(sizeVox * sizeVox, Allocator.Persistent)
            };
            return job;
        }

        public static LinkLabelOfNeighborChunksYJob DoY(int sizeVox, VoxelChunk chunkBottom, VoxelChunk chunkTop)
        {
            var job = new LinkLabelOfNeighborChunksYJob
            {
                SizeVox = sizeVox,
                SizeVox2 = sizeVox * sizeVox,
                Labels1 = BuildChunkLabelNativeArray(sizeVox, chunkBottom),
                Labels2 = BuildChunkLabelNativeArray(sizeVox, chunkTop),
                LinksFrom1To2 = new NativeParallelMultiHashMap<int, int>(sizeVox * sizeVox, Allocator.Persistent),
                LinksFrom2To1 = new NativeParallelMultiHashMap<int, int>(sizeVox * sizeVox, Allocator.Persistent),
                LabelsConnectedToTheGround = new NativeParallelHashSet<int>(sizeVox * sizeVox, Allocator.Persistent)
            };
            return job;
        }

        public static LinkLabelOfNeighborChunksZJob DoZ(int sizeVox, VoxelChunk chunkFront, VoxelChunk chunkBack)
        {
            var job = new LinkLabelOfNeighborChunksZJob
            {
                SizeVox = sizeVox,
                SizeVox2 = sizeVox * sizeVox,
                Labels1 = BuildChunkLabelNativeArray(sizeVox, chunkFront),
                Labels2 = BuildChunkLabelNativeArray(sizeVox, chunkBack),
                LinksFrom1To2 = new NativeParallelMultiHashMap<int, int>(sizeVox * sizeVox, Allocator.Persistent),
                LinksFrom2To1 = new NativeParallelMultiHashMap<int, int>(sizeVox * sizeVox, Allocator.Persistent),
                LabelsConnectedToTheGround = new NativeParallelHashSet<int>(sizeVox * sizeVox, Allocator.Persistent)
            };
            return job;
        }

        private static NativeArray<int> BuildChunkLabelNativeArray(int sizeVox, VoxelChunk chunk)
        {
            if (!ReferenceEquals(null, chunk)) {
                Debug.Assert(chunk.LabelArray != null, $"chunk.LabelArray is null ({chunk.ChunkPosition})");
            }

            return ReferenceEquals(null, chunk) ? new NativeArray<int>(sizeVox * sizeVox * sizeVox, Allocator.Persistent) : new NativeArray<int>(chunk.LabelArray, Allocator.Persistent);
        }

        public static NativeParallelHashSet<int> HashSetToNativeHashSet(int capacity, HashSet<int> hashSet)
        {
            var nativeHashSet = new NativeParallelHashSet<int>(capacity, Allocator.Persistent);
            if (hashSet != null) {
                foreach (var value in hashSet) {
                    nativeHashSet.Add(value);
                }
            }

            return nativeHashSet;
        }

        public static void NativeHashSetToHashSet(NativeParallelHashSet<int> nativeHashSet, HashSet<int> hashSet)
        {
            hashSet.Clear();
            var enumerator = nativeHashSet.GetEnumerator();
            while (enumerator.MoveNext()) {
                hashSet.Add(enumerator.Current);
            }

            enumerator.Dispose();
        }

        public static void NativeAABBHashMapToDictionary(NativeParallelHashMap<int, ConnectedComponentLabeling.AABB> nativeHashMap, Dictionary<int, ConnectedComponentLabeling.AABB> dictionary)
        {
            dictionary.Clear();
            foreach (var entry in nativeHashMap) {
                dictionary.Add(entry.Key, entry.Value);
            }
        }

        private static void NativeHashMapToDictionary(NativeParallelMultiHashMap<int, int> nativeHashMap, Dictionary<int, HashSet<int>> dictionary)
        {
            dictionary.Clear();
            var enumerator = nativeHashMap.GetEnumerator();
            while (enumerator.MoveNext()) {
                if (!dictionary.TryGetValue(enumerator.Current.Key, out var hashSet)) {
                    hashSet = new HashSet<int>();
                    dictionary.Add(enumerator.Current.Key, hashSet);
                }

                hashSet.Add(enumerator.Current.Value);
            }

            enumerator.Dispose();
        }

        public static void CompleteX(LinkLabelOfNeighborChunksXJob job, VoxelChunk chunk1, VoxelChunk chunk2)
        {
            if (!ReferenceEquals(null, chunk1) && !ReferenceEquals(null, chunk2)) {
                NativeHashMapToDictionary(job.LinksFrom1To2, chunk1.LinksToRight);
                NativeHashMapToDictionary(job.LinksFrom2To1, chunk2.LinksToLeft);
            } else if (!ReferenceEquals(null, chunk1)) {
                chunk1.LabelsConnectedToTheGround.Clear();
                NativeHashSetToHashSet(job.LabelsConnectedToTheGround, chunk1.LabelsConnectedToTheGround);
            } else {
                chunk2.LabelsConnectedToTheGround.Clear();
                NativeHashSetToHashSet(job.LabelsConnectedToTheGround, chunk2.LabelsConnectedToTheGround);
            }

            job.Labels1.Dispose();
            job.Labels2.Dispose();
            job.LinksFrom1To2.Dispose();
            job.LinksFrom2To1.Dispose();
            job.LabelsConnectedToTheGround.Dispose();
        }

        public static void CompleteY(LinkLabelOfNeighborChunksYJob job, VoxelChunk chunk1, VoxelChunk chunk2)
        {
            if (!ReferenceEquals(null, chunk1) && !ReferenceEquals(null, chunk2)) {
                NativeHashMapToDictionary(job.LinksFrom1To2, chunk1.LinksToTop);
                NativeHashMapToDictionary(job.LinksFrom2To1, chunk2.LinksToBottom);
            } else if (!ReferenceEquals(null, chunk1)) {
                chunk1.LabelsConnectedToTheGround.Clear();
                NativeHashSetToHashSet(job.LabelsConnectedToTheGround, chunk1.LabelsConnectedToTheGround);
            } else {
                chunk2.LabelsConnectedToTheGround.Clear();
                NativeHashSetToHashSet(job.LabelsConnectedToTheGround, chunk2.LabelsConnectedToTheGround);
            }

            job.Labels1.Dispose();
            job.Labels2.Dispose();
            job.LinksFrom1To2.Dispose();
            job.LinksFrom2To1.Dispose();
            job.LabelsConnectedToTheGround.Dispose();
        }

        public static void CompleteZ(LinkLabelOfNeighborChunksZJob job, VoxelChunk chunk1, VoxelChunk chunk2)
        {
            if (!ReferenceEquals(null, chunk1) && !ReferenceEquals(null, chunk2)) {
                NativeHashMapToDictionary(job.LinksFrom1To2, chunk1.LinksToBack);
                NativeHashMapToDictionary(job.LinksFrom2To1, chunk2.LinksToFront);
            } else if (!ReferenceEquals(null, chunk1)) {
                chunk1.LabelsConnectedToTheGround.Clear();
                NativeHashSetToHashSet(job.LabelsConnectedToTheGround, chunk1.LabelsConnectedToTheGround);
            } else {
                chunk2.LabelsConnectedToTheGround.Clear();
                NativeHashSetToHashSet(job.LabelsConnectedToTheGround, chunk2.LabelsConnectedToTheGround);
            }

            job.Labels1.Dispose();
            job.Labels2.Dispose();
            job.LinksFrom1To2.Dispose();
            job.LinksFrom2To1.Dispose();
            job.LabelsConnectedToTheGround.Dispose();
        }
    }
}