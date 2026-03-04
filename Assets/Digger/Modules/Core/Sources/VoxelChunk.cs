using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Digger.Modules.Core.Sources.Jobs;
using Digger.Modules.Core.Sources.Polygonizers;
using Digger.Modules.Core.Sources.TerrainInterface;
using Digger.Modules.Core.Sources.VoxelPhysics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Digger.Modules.Core.Sources
{
    public class VoxelChunk : MonoBehaviour
    {
        [SerializeField] private DiggerSystem digger;
        [SerializeField] private int sizeVox;
        [SerializeField] private int sizeOfMesh;
        [SerializeField] private Vector3i chunkPosition;
        [SerializeField] private Vector3i voxelPosition;
        [SerializeField] private Vector3 worldPosition;

        [NonSerialized] private Voxel[] voxelArray;
        [NonSerialized] private float[] heightArray;
        [NonSerialized] private float3[] normalArray;
        [NonSerialized] private float[] alphamapArray;
        [NonSerialized] private int[] labelArray;
        [NonSerialized] private readonly Dictionary<int, ConnectedComponentLabeling.AABB> labelMap = new Dictionary<int, ConnectedComponentLabeling.AABB>();
        [NonSerialized] private readonly HashSet<int> labelsConnectedToTheGround = new HashSet<int>();
        [NonSerialized] private readonly HashSet<int> labelsConnectedToTheGroundThroughNeighbors = new HashSet<int>();
        [NonSerialized] private readonly Dictionary<int, HashSet<int>> linksToRight = new Dictionary<int, HashSet<int>>();
        [NonSerialized] private readonly Dictionary<int, HashSet<int>> linksToLeft = new Dictionary<int, HashSet<int>>();
        [NonSerialized] private readonly Dictionary<int, HashSet<int>> linksToTop = new Dictionary<int, HashSet<int>>();
        [NonSerialized] private readonly Dictionary<int, HashSet<int>> linksToBottom = new Dictionary<int, HashSet<int>>();
        [NonSerialized] private readonly Dictionary<int, HashSet<int>> linksToBack = new Dictionary<int, HashSet<int>>();
        [NonSerialized] private readonly Dictionary<int, HashSet<int>> linksToFront = new Dictionary<int, HashSet<int>>();
        [NonSerialized] private int3 alphamapArraySize;
        [NonSerialized] private int2 alphamapArrayOrigin;

        [NonSerialized] private Voxel[] voxelArrayBeforeOperation;

        [NonSerialized] private JobHandle? currentJobHandle;
        [NonSerialized] private IJobParallelFor currentJob;
        [NonSerialized] private int currentJobStartFrame;
        [NonSerialized] private ConnectedComponentLabelingJob? currentLabelizationJob;
        [NonSerialized] private NativeArray<Voxel> voxels;
        [NonSerialized] private NativeArray<float> heights;
        [NonSerialized] private NativeArray<int> holes;
        [NonSerialized] private NativeParallelHashSet<int> chunkOnSurfaceY;
        [NonSerialized] private readonly Dictionary<int, IPolygonizer> polygonizersPerLod = new();
        [NonSerialized] private int needToBakePhysicMeshInstanceID;
        [NonSerialized] private ModificationResult lastOperationResult;

        private IPolygonizer GetPolygonizer(int lod)
        {
            if (polygonizersPerLod.TryGetValue(lod, out var polygonizer)) return polygonizer;
            polygonizer = digger.PolygonizerProvider ? digger.PolygonizerProvider.NewPolygonizer(digger) : new MarchingCubesPolygonizer();
            polygonizersPerLod.Add(lod, polygonizer);
            return polygonizer;
        }
        
        
        public bool IsLoaded => VoxelArray is { Length: > 0 };
        public Vector3i ChunkPosition => chunkPosition;
        public Vector3i VoxelPosition => voxelPosition;
        private float Altitude => voxelPosition.y * digger.HeightmapScale.y;
        public float3 WorldPosition => worldPosition;
        public float3 AbsoluteWorldPosition => digger.transform.TransformPoint(worldPosition);
        public int3 AbsoluteVoxelPosition => Utils.UnityToVoxelPosition(digger.transform.TransformPoint(worldPosition), HeightmapScale);
        public int SizeVox => sizeVox;
        public int SizeOfMesh => sizeOfMesh;
        public float3 HeightmapScale => digger.HeightmapScale;
        public Voxel[] VoxelArray => voxelArray;
        public float[] HeightArray => heightArray;
        public float3[] NormalArray => normalArray;
        public float[] AlphamapArray => alphamapArray;
        public int[] LabelArray => labelArray;

        public int[] HolesArray => digger.Cutter.GetHoles(chunkPosition, voxelPosition);
        public float3 CutMargin => digger.CutMargin;
        public TerrainCutter Cutter => digger.Cutter;
        public DiggerSystem Digger => digger;
        public int3 AlphamapArraySize => alphamapArraySize;
        public int2 AlphamapArrayOrigin => alphamapArrayOrigin;

        public HashSet<int> LabelsConnectedToTheGround => labelsConnectedToTheGround;

        public Dictionary<int, HashSet<int>> LinksToRight => linksToRight;

        public Dictionary<int, HashSet<int>> LinksToLeft => linksToLeft;

        public Dictionary<int, HashSet<int>> LinksToTop => linksToTop;

        public Dictionary<int, HashSet<int>> LinksToBottom => linksToBottom;

        public Dictionary<int, HashSet<int>> LinksToBack => linksToBack;

        public Dictionary<int, HashSet<int>> LinksToFront => linksToFront;

        public HashSet<int> LabelsConnectedToTheGroundThroughNeighbors => labelsConnectedToTheGroundThroughNeighbors;

        public Dictionary<int, ConnectedComponentLabeling.AABB> LabelMap => labelMap;

        internal static VoxelChunk Create(DiggerSystem digger, Chunk chunk)
        {
            Utils.Profiler.BeginSample("VoxelChunk.Create");
            var go = new GameObject("VoxelChunk")
            {
                hideFlags = HideFlags.DontSaveInBuild,
                transform =
                {
                    parent = chunk.transform,
                    localPosition = Vector3.zero,
                    localRotation = Quaternion.identity,
                    localScale = Vector3.one
                }
            };
            var voxelChunk = go.AddComponent<VoxelChunk>();
            voxelChunk.digger = digger;
            voxelChunk.sizeVox = digger.SizeVox;
            voxelChunk.sizeOfMesh = digger.SizeOfMesh;
            voxelChunk.chunkPosition = chunk.ChunkPosition;
            voxelChunk.voxelPosition = chunk.VoxelPosition;
            voxelChunk.worldPosition = chunk.WorldPosition;
            voxelChunk.Load();

            Utils.Profiler.EndSample();
            return voxelChunk;
        }

        private static void GenerateVoxels(DiggerSystem digger, float[] heightArray, Vector3i chunkVoxelPosition, ref Voxel[] voxelArray, bool refreshOnly)
        {
            Utils.Profiler.BeginSample("[Dig] VoxelChunk.GenerateVoxels");
            var sizeVox = digger.SizeVox;
            voxelArray ??= new Voxel[sizeVox * sizeVox * sizeVox];

            var heights = new NativeArray<float>(heightArray, Allocator.TempJob);
            var voxels = refreshOnly ? new NativeArray<Voxel>(voxelArray, Allocator.TempJob) 
                                     : new NativeArray<Voxel>(sizeVox * sizeVox * sizeVox, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            // Use the configured voxel generator
            var generator = digger.VoxelGenerator;
            var handle = generator.GenerateVoxels(
                heightArray,
                chunkVoxelPosition.ToInt3(),
                sizeVox,
                digger.HeightmapScale,
                heights,
                voxels,
                refreshOnly);

            // Wait for the job to complete
            handle.Complete();

            voxels.CopyTo(voxelArray);
            heights.Dispose();
            voxels.Dispose();

            Utils.Profiler.EndSample();
        }

        public void RefreshVoxels()
        {
            GenerateVoxels(digger, HeightArray, voxelPosition, ref voxelArray, refreshOnly: true);
        }

        public void PrepareOperationJob<T>(IOperation<T> operation) where T : struct, IJobParallelFor
        {
            var job = operation.Do(this);
            currentJob = job;
        }
        
        public void ScheduleOperationJob<T>() where T : struct, IJobParallelFor
        {
            currentJobHandle = ((T)currentJob).Schedule(VoxelArray.Length, digger.SizeVox);
        }

        public void CompleteOperation<T>(IOperation<T> operation) where T : struct, IJobParallelFor
        {
            CompleteBackgroundJob();
            lastOperationResult = operation.Complete((T)currentJob, this);
        }

        public ModificationResult GetAndClearOperationResult()
        {
            var result = lastOperationResult;
            lastOperationResult = ModificationResult.Empty;
            return result;
        }

        public void LabelizeVoxels()
        {
            var job = ConnectedComponentLabeling.Do(this);
            currentLabelizationJob = job;
            currentJobHandle = job.Schedule();
        }

        public void CompleteLabelizeVoxels()
        {
            CompleteBackgroundJob();
            Debug.Assert(currentLabelizationJob != null, nameof(currentLabelizationJob) + " != null");
            ConnectedComponentLabeling.Complete(currentLabelizationJob.Value, this);
            currentLabelizationJob = null;
        }

        public void HandleFloatingVoxels()
        {
            var job = RemoveFloatingVoxels.Do(this);
            currentJob = job;
            currentJobHandle = job.Schedule(VoxelArray.Length, 512);
        }

        public void CompleteHandleFloatingVoxels()
        {
            CompleteBackgroundJob();
            RemoveFloatingVoxels.Complete((RemoveFloatingVoxelsJob)currentJob, this);
            currentJob = null;
        }

        public void UpdateVoxelsOnSurface()
        {
            if (VoxelArray == null)
                return;

            heights = new NativeArray<float>(HeightArray, Allocator.Persistent);
            voxels = new NativeArray<Voxel>(VoxelArray, Allocator.Persistent);
            var cutter = digger.Cutter;
            holes = new NativeArray<int>(cutter.GetHoles(chunkPosition, voxelPosition), Allocator.Persistent);

            // Set up the job data
            var jobData = new VoxelFillSurfaceJob()
            {
                ChunkAltitude = Altitude,
                Heights = heights,
                Voxels = voxels,
                Holes = holes,
                SizeVox = SizeVox,
                SizeVox2 = SizeVox * SizeVox,
                HeightmapScale = digger.HeightmapScale,
            };

            // Schedule the job
            currentJobHandle = jobData.Schedule(voxels.Length, 64);
        }

        public void CompleteUpdateVoxelsOnSurface()
        {
            CompleteBackgroundJob();
            voxels.CopyTo(VoxelArray);
            heights.Dispose();
            voxels.Dispose();
            holes.Dispose();
        }
        
        public void GetSurfaceChunksOnHoles()
        {
            if (VoxelArray == null)
                return;

            heights = new NativeArray<float>(HeightArray, Allocator.Persistent);
            chunkOnSurfaceY = new NativeParallelHashSet<int>(100, Allocator.Persistent);
            var cutter = digger.Cutter;
            holes = new NativeArray<int>(cutter.GetHoles(chunkPosition, voxelPosition), Allocator.Persistent);

            // Set up the job data
            var jobData = new GetSurfaceChunksJob()
            {
                ChunkOnSurfaceY = chunkOnSurfaceY.AsParallelWriter(),
                Heights = heights,
                Holes = holes,
                SizeVox = SizeVox,
                SizeOfMesh = SizeOfMesh,
                HeightmapScaleY = digger.HeightmapScale.y,
            };

            // Schedule the job
            currentJobHandle = jobData.Schedule(holes.Length, 64);
        }
        
        private void CompleteBackgroundJob()
        {
            if (!currentJobHandle.HasValue)
                return;
            currentJobHandle.Value.Complete();
            currentJobHandle = null;
        }
        
        private void CompleteJobSync()
        {
            if (!currentJobHandle.HasValue)
                return;
            currentJobHandle.Value.Complete();
            currentJobHandle = null;
        }

        public HashSet<int3> CompleteGetSurfaceChunksOnHoles()
        {
            CompleteBackgroundJob();
            var result = new HashSet<int3>();
            foreach (var chunkY in chunkOnSurfaceY)
            {
                result.Add(new int3(chunkPosition.x, chunkY, chunkPosition.z));
            }
            chunkOnSurfaceY.Dispose();
            heights.Dispose();
            holes.Dispose();
            return result;
        }

        public bool HasAlteredVoxels()
        {
            return VoxelArray != null && VoxelArray.Any(voxel => voxel.Alteration != Voxel.Unaltered);
        }

        public void BuildMesh(int lod)
        {
            currentJobHandle = GetPolygonizer(lod).BuildMesh(this, lod);
        }
        
        public bool BuildMeshSync(int lod, Mesh mesh)
        {
            currentJobHandle = GetPolygonizer(lod).BuildMesh(this, lod);
            CompleteJobSync();
            return GetPolygonizer(lod).CompleteBuildMesh(mesh, Digger.GetChunkBounds());
        }
        
        public void CompleteBuildMeshJob()
        {
            CompleteBackgroundJob();
        }
        
        public void CompleteBuildMesh(Mesh mesh, int lod)
        {
            needToBakePhysicMeshInstanceID = GetPolygonizer(lod).CompleteBuildMesh(mesh, Digger.GetChunkBounds()) ? 
                mesh.GetInstanceID() : 0;
        }

        public void BakePhysicMesh()
        {
            if (needToBakePhysicMeshInstanceID == 0)
                return;
            var job = new PhysicsBakeMeshJob
            {
                MeshInstanceId = needToBakePhysicMeshInstanceID
            };
            currentJobHandle = job.Schedule();
        }
        
        public void CompleteBakePhysicMesh()
        {
            CompleteBackgroundJob();
        }

        public void RecordUndoIfNeeded()
        {
#if UNITY_EDITOR
            if (VoxelArray == null || VoxelArray.Length == 0) {
                Debug.LogError("Voxel array should not be null when recording undo");
                return;
            }

            Utils.Profiler.BeginSample("[Dig] VoxelChunk.RecordUndoIfNeeded");
            var path = digger.GetEditorOnlyPathVoxelFile(chunkPosition);
            var savePath = digger.GetPathVersionedVoxelFile(chunkPosition, digger.PreviousVersion);
            if (File.Exists(path) && !File.Exists(savePath)) {
                File.Copy(path, savePath);
            }

            var labelPath = digger.GetEditorOnlyPathLabelFile(chunkPosition);
            var saveLabelPath = digger.GetPathVersionedLabelFile(chunkPosition, digger.PreviousVersion);
            if (File.Exists(labelPath) && !File.Exists(saveLabelPath)) {
                File.Copy(labelPath, saveLabelPath);
            }

            var metadataPath = digger.GetEditorOnlyPathVoxelMetadataFile(chunkPosition);
            var saveMetadataPath = digger.GetPathVersionedVoxelMetadataFile(chunkPosition, digger.PreviousVersion);
            if (File.Exists(metadataPath) && !File.Exists(saveMetadataPath)) {
                File.Copy(metadataPath, saveMetadataPath);
            }

            Utils.Profiler.EndSample();
            digger.EnsureChunkWillBePersisted(this);
#endif
        }

        public void Persist()
        {
            if (VoxelArray == null || VoxelArray.Length == 0) {
                Debug.LogError("Voxel array should not be null in saving");
                return;
            }

            Utils.Profiler.BeginSample("[Dig] VoxelChunk.Persist");

            var path = digger.GetPathVoxelFile(chunkPosition, true);
            var voxelsToPersist = new NativeArray<Voxel>(VoxelArray, Allocator.Temp);
            var bytes = new NativeSlice<Voxel>(voxelsToPersist).SliceConvert<byte>();
            File.WriteAllBytes(path, bytes.ToArray());
            voxelsToPersist.Dispose();

            var labelPath = digger.GetPathLabelFile(chunkPosition, true);
            var labelsToPersist = new NativeArray<int>(LabelArray, Allocator.Temp);
            var labelBytes = new NativeSlice<int>(labelsToPersist).SliceConvert<byte>();
            File.WriteAllBytes(labelPath, labelBytes.ToArray());
            labelsToPersist.Dispose();

            var metadataPath = digger.GetPathVoxelMetadataFile(chunkPosition, true);
            using (var stream = new FileStream(metadataPath, FileMode.Create, FileAccess.Write, FileShare.Write, 4096,
                       FileOptions.Asynchronous)) {
                using (var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII)) {
                    PersistHashSet(writer, labelsConnectedToTheGround);
                    PersistHashSet(writer, labelsConnectedToTheGroundThroughNeighbors);
                    PersistDictionary(writer, linksToRight);
                    PersistDictionary(writer, linksToLeft);
                    PersistDictionary(writer, linksToTop);
                    PersistDictionary(writer, linksToBottom);
                    PersistDictionary(writer, linksToBack);
                    PersistDictionary(writer, linksToFront);
                }
            }

#if UNITY_EDITOR
            var savePath = digger.GetPathVersionedVoxelFile(chunkPosition, digger.Version);
            File.Copy(path, savePath, true);
            var saveLabelPath = digger.GetPathVersionedLabelFile(chunkPosition, digger.Version);
            File.Copy(labelPath, saveLabelPath, true);
            var saveMetadataPath = digger.GetPathVersionedVoxelMetadataFile(chunkPosition, digger.Version);
            File.Copy(metadataPath, saveMetadataPath, true);
#endif
            Utils.Profiler.EndSample();
        }

        private void PersistDictionary(BinaryWriter writer, Dictionary<int, HashSet<int>> dico)
        {
            writer.Write(dico.Count);
            foreach (var keyValuePair in dico) {
                writer.Write(keyValuePair.Key);
                PersistHashSet(writer, keyValuePair.Value);
            }
        }

        private void ReadDictionary(BinaryReader reader, Dictionary<int, HashSet<int>> dico)
        {
            dico.Clear();
            var count = reader.ReadInt32();
            for (var i = 0; i < count; i++) {
                var key = reader.ReadInt32();
                var set = new HashSet<int>();
                ReadHashSet(reader, set);
                dico.Add(key, set);
            }
        }

        private void PersistHashSet(BinaryWriter writer, HashSet<int> set)
        {
            writer.Write(set.Count);
            foreach (var value in set) {
                writer.Write(value);
            }
        }

        private void ReadHashSet(BinaryReader reader, HashSet<int> set)
        {
            set.Clear();
            var count = reader.ReadInt32();
            for (var i = 0; i < count; i++) {
                set.Add(reader.ReadInt32());
            }
        }

        public void Load()
        {
            Utils.Profiler.BeginSample("VoxelChunk.Load");
            // Feed heights again in case they have been modified
            heightArray = digger.HeightsFeeder.GetHeights(chunkPosition, voxelPosition);
            normalArray = digger.NormalsFeeder.GetNormals(chunkPosition, voxelPosition);
            var alphamapsInfo = digger.AlphamapsFeeder.GetAlphamaps(chunkPosition, worldPosition, SizeOfMesh);
            alphamapArray = alphamapsInfo.AlphamapArray;
            alphamapArraySize = alphamapsInfo.AlphamapArraySize;
            alphamapArrayOrigin = alphamapsInfo.AlphamapArrayOrigin;

            // Load holes
            var cutter = digger.Cutter;
            cutter.GetHoles(chunkPosition, voxelPosition);

            var path = digger.GetPathVoxelFile(chunkPosition, false);
            var rawBytes = Utils.GetBytes(path);

            if (rawBytes == null) {
                if (VoxelArray == null) {
                    // If there is no persisted voxels but voxel array is null, then we fall back and (re)generate them.
                    GenerateVoxels(digger, heightArray, voxelPosition, ref voxelArray, refreshOnly: false);
                    labelArray = new int[voxelArray.Length];
                    labelMap.Clear();
                    labelsConnectedToTheGround.Clear();
                    labelsConnectedToTheGroundThroughNeighbors.Clear();
                    linksToRight.Clear();
                    linksToLeft.Clear();
                    linksToTop.Clear();
                    linksToBottom.Clear();
                    linksToBack.Clear();
                    linksToFront.Clear();
                    digger.EnsureChunkWillBePersisted(this);
                }

                Utils.Profiler.EndSample();
                return;
            }

            ReadVoxelFile(SizeVox, rawBytes, ref voxelArray);
            
            var labelPath = digger.GetPathLabelFile(chunkPosition, false);
            var labelBytes = Utils.GetBytes(labelPath);
            if (labelBytes == null) {
                Debug.LogError($"Could not read label file of chunk {chunkPosition}");
                Utils.Profiler.EndSample();
                return;
            }
            ReadLabelFile(SizeVox, labelBytes, ref labelArray);

            var hScale = digger.HeightmapScale;
            var metadataPath = digger.GetPathVoxelMetadataFile(chunkPosition, false);
            rawBytes = Utils.GetBytes(metadataPath);
            if (rawBytes == null) {
                Debug.LogError($"Could not read metadata file of chunk {chunkPosition}");
                Utils.Profiler.EndSample();
                return;
            }

            using (Stream stream = new MemoryStream(rawBytes)) {
                using (var reader = new BinaryReader(stream, System.Text.Encoding.ASCII)) {
                    ReadHashSet(reader, labelsConnectedToTheGround);
                    ReadHashSet(reader, labelsConnectedToTheGroundThroughNeighbors);
                    ReadDictionary(reader, linksToRight);
                    ReadDictionary(reader, linksToLeft);
                    ReadDictionary(reader, linksToTop);
                    ReadDictionary(reader, linksToBottom);
                    ReadDictionary(reader, linksToBack);
                    ReadDictionary(reader, linksToFront);
                }
            }

            Utils.Profiler.EndSample();
        }

        public void InitVoxelArrayBeforeOperation()
        {
            voxelArrayBeforeOperation = new Voxel[VoxelArray.Length];
            Array.Copy(VoxelArray, voxelArrayBeforeOperation, VoxelArray.Length);
        }

        internal void ResetVoxelArrayBeforeOperation()
        {
            voxelArrayBeforeOperation = null;
        }

        private static void ReadVoxelFile(int sizeVox, byte[] rawBytes, ref Voxel[] voxelArray)
        {
            voxelArray ??= new Voxel[sizeVox * sizeVox * sizeVox];

            var voxelBytes = new NativeArray<byte>(rawBytes, Allocator.Temp);
            var bytes = new NativeSlice<byte>(voxelBytes);
            var voxelSlice = bytes.SliceConvert<Voxel>();
            DirectNativeCollectionsAccess.CopyTo(voxelSlice, voxelArray);
            voxelBytes.Dispose();
        }

        private void ReadLabelFile(int sizeVox, byte[] rawBytes, ref int[] labelArray)
        {
            if (labelArray == null)
                labelArray = new int[sizeVox * sizeVox * sizeVox];

            var labelBytes = new NativeArray<byte>(rawBytes, Allocator.Temp);
            var bytes = new NativeSlice<byte>(labelBytes);
            var labelSlice = bytes.SliceConvert<int>();
            DirectNativeCollectionsAccess.CopyTo(labelSlice, labelArray);
            labelBytes.Dispose();
        }

        public static NativeArray<Voxel> LoadVoxels(DiggerSystem digger, Vector3i chunkPosition)
        {
            Utils.Profiler.BeginSample("[Dig] VoxelChunk.LoadVoxels");

            if (!digger.IsChunkBelongingToMe(chunkPosition)) {
                var neighbor = digger.GetNeighborAt(chunkPosition);
                if (neighbor) {
                    var neighborChunkPosition = neighbor.ToChunkPosition(digger.ToWorldPosition(chunkPosition));
                    if (!neighbor.IsChunkBelongingToMe(neighborChunkPosition)) {
                        Debug.LogError(
                            $"neighborChunkPosition {neighborChunkPosition} should always belong to neighbor");
                        return new NativeArray<Voxel>(1, Allocator.Persistent);
                    }

                    return LoadVoxels(neighbor, neighborChunkPosition);
                }
            }

            if (digger.GetChunk(chunkPosition, out var chunk)) {
                if (chunk.VoxelChunk.voxelArrayBeforeOperation != null) {
                    return new NativeArray<Voxel>(chunk.VoxelChunk.voxelArrayBeforeOperation, Allocator.Persistent);
                }

                chunk.LazyLoad();
                return new NativeArray<Voxel>(chunk.VoxelChunk.VoxelArray, Allocator.Persistent);
            }

            return new NativeArray<Voxel>(1, Allocator.Persistent);
        }
    }
}