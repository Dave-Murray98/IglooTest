using System.IO;
using Digger.Modules.Core.Sources.Generators;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Digger.Modules.Core.Sources
{
    [AddComponentMenu("Digger/Digger Master")]
    public class DiggerMaster : ADiggerMonoBehaviour
    {
        public const string ParentFolder = "DiggerData";
        public const string ScenesBaseFolder = "Scenes";


        [SerializeField] private int chunkSize = 33;
        public int SizeOfMesh => chunkSize - 1;
        public int SizeVox => chunkSize + 1;

        [SerializeField] private string sceneDataFolder;
        [SerializeField] private float screenRelativeTransitionHeightLod0 = 0.1f;
        [SerializeField] private float screenRelativeTransitionHeightLod1 = 0.05f;
        [SerializeField] private int colliderLodIndex = 0;
        [SerializeField] private bool createLODs = false;
        [SerializeField] private bool showUnderlyingObjects = false;
        [SerializeField] private int resolutionMult = 1;
        [SerializeField] private float voxelHeight = 1f;
        [SerializeField] private bool autoVoxelHeight = false;
        [SerializeField] private int layer = 0;
        [SerializeField] private string chunksTag = "Untagged";
        [SerializeField] private bool enableOcclusionCulling = true;
        [SerializeField] private bool enableContributeGI = true;
        [SerializeField] private bool forceMicroSplatMaterialAssetUpdate = false;
        [SerializeField] private bool autoSaveMeshesAsAssets = true;
        [SerializeField] private bool autoRemoveFloatingVoxels = false;
        [SerializeField] private int maxFloatingVoxelGroupSizeToRemove = 30;
        [SerializeField] private ScriptableObject voxelGenerator;

        private static string ParentPath {
            get {
                var projectDir = new DirectoryInfo(Application.dataPath);
                if (projectDir.Exists && projectDir.Parent != null && projectDir.Parent.Exists) {
                    Directory.SetCurrentDirectory(projectDir.Parent.FullName);
                }

                return Path.Combine("Assets", ParentFolder);
            }
        }

        private static string ScenesBasePath => Path.Combine(ParentPath, ScenesBaseFolder);
        public string SceneDataPath => Path.Combine(ScenesBasePath, sceneDataFolder);

        public string SceneDataFolder {
            get => sceneDataFolder;
            set => sceneDataFolder = value;
        }

        public float ScreenRelativeTransitionHeightLod0 {
            get => screenRelativeTransitionHeightLod0;
            set => screenRelativeTransitionHeightLod0 = value;
        }

        public float ScreenRelativeTransitionHeightLod1 {
            get => screenRelativeTransitionHeightLod1;
            set => screenRelativeTransitionHeightLod1 = value;
        }

        public int ColliderLodIndex {
            get => colliderLodIndex;
            set => colliderLodIndex = value;
        }

        public int ResolutionMult {
            get => resolutionMult;
            set => resolutionMult = value;
        }

        public int ChunkSize {
            get => chunkSize;
            set => chunkSize = value;
        }

        public bool CreateLODs {
            get => createLODs;
            set => createLODs = value;
        }

        public bool ShowUnderlyingObjects {
            get => showUnderlyingObjects;
            set => showUnderlyingObjects = value;
        }

        public int Layer {
            get => layer;
            set => layer = value;
        }

        public string ChunksTag {
            get => chunksTag;
            set => chunksTag = value ?? "Untagged";
        }

        public bool EnableOcclusionCulling {
            get => enableOcclusionCulling;
            set => enableOcclusionCulling = value;
        }
        
        public bool EnableContributeGI {
            get => enableContributeGI;
            set => enableContributeGI = value;
        }

        public float VoxelHeight {
            get => voxelHeight;
            set => voxelHeight = value;
        }

        public bool AutoVoxelHeight {
            get => autoVoxelHeight;
            set => autoVoxelHeight = value;
        }

        public bool ForceMicroSplatMaterialAssetUpdate {
            get => forceMicroSplatMaterialAssetUpdate;
            set => forceMicroSplatMaterialAssetUpdate = value;
        }

        public bool AutoSaveMeshesAsAssets
        {
            get => autoSaveMeshesAsAssets;
            set => autoSaveMeshesAsAssets = value;
        }

        public bool AutoRemoveFloatingVoxels {
            get => autoRemoveFloatingVoxels;
            set => autoRemoveFloatingVoxels = value;
        }

        public int MaxFloatingVoxelGroupSizeToRemove {
            get => maxFloatingVoxelGroupSizeToRemove;
            set => maxFloatingVoxelGroupSizeToRemove = value;
        }

        public IVoxelGenerator VoxelGenerator {
            get {
                // If no generator is assigned, try to find a default one
                if (voxelGenerator == null || !(voxelGenerator is IVoxelGenerator)) {
#if UNITY_EDITOR
                    // In editor, try to load the default simple generator asset
                    var defaultPath = "Assets/Digger/Modules/Core/DefaultGenerators/DefaultSimpleVoxelGenerator.asset";
                    voxelGenerator = UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObject>(defaultPath);
                    
                    // If still null, create a temporary instance (will not be saved)
                    if (voxelGenerator == null) {
                        voxelGenerator = ScriptableObject.CreateInstance<SimpleVoxelGenerator>();
                    }
#else
                    // At runtime, create a temporary instance if needed
                    if (voxelGenerator == null) {
                        voxelGenerator = ScriptableObject.CreateInstance<SimpleVoxelGenerator>();
                    }
#endif
                }
                return voxelGenerator as IVoxelGenerator;
            }
            set => voxelGenerator = value as ScriptableObject;
        }

        public void CreateDirs()
        {
#if UNITY_EDITOR
            if (!Directory.Exists(ParentPath)) {
                AssetDatabase.CreateFolder("Assets", ParentFolder);
            }

            if (!Directory.Exists(ScenesBasePath)) {
                AssetDatabase.CreateFolder(ParentPath, ScenesBaseFolder);
            }

            if (string.IsNullOrEmpty(sceneDataFolder)) {
                var sceneName = SceneManager.GetActiveScene().name;
                if (string.IsNullOrEmpty(sceneName)) {
                    sceneName = "Untitled";
                }

                sceneDataFolder = sceneName;
                var i = 0;
                while (Directory.Exists(SceneDataPath)) {
                    sceneDataFolder = $"{sceneName}-{i++}";
                }
            }

            if (!Directory.Exists(SceneDataPath)) {
                AssetDatabase.CreateFolder(ScenesBasePath, sceneDataFolder);
            }
#endif
        }
    }
}