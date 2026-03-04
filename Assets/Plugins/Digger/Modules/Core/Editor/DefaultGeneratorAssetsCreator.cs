using Digger.Modules.Core.Sources.Generators;
using UnityEditor;
using UnityEngine;

namespace Digger.Modules.Core.Editor
{
    /// <summary>
    /// Utility to create default voxel generator assets
    /// </summary>
    public static class DefaultGeneratorAssetsCreator
    {
        private const string DefaultGeneratorsFolder = "Assets/Digger/Modules/Core/DefaultGenerators";
        private const string SimpleGeneratorPath = DefaultGeneratorsFolder + "/DefaultSimpleVoxelGenerator.asset";
        private const string AdvancedGeneratorPath = DefaultGeneratorsFolder + "/DefaultAdvancedVoxelGenerator.asset";

        [MenuItem("Tools/Digger/Create Default Voxel Generator Assets")]
        public static void CreateDefaultGeneratorAssets()
        {
            // Create folder if it doesn't exist
            if (!AssetDatabase.IsValidFolder(DefaultGeneratorsFolder))
            {
                var parentFolder = "Assets/Digger/Modules/Core";
                var folderName = "DefaultGenerators";
                AssetDatabase.CreateFolder(parentFolder, folderName);
            }

            // Create Simple Generator if it doesn't exist
            if (!AssetExists(SimpleGeneratorPath))
            {
                var simpleGenerator = ScriptableObject.CreateInstance<SimpleVoxelGenerator>();
                AssetDatabase.CreateAsset(simpleGenerator, SimpleGeneratorPath);
                Debug.Log($"Created default Simple Voxel Generator at: {SimpleGeneratorPath}");
            }
            else
            {
                Debug.Log($"Simple Voxel Generator already exists at: {SimpleGeneratorPath}");
            }

            // Create Advanced Generator if it doesn't exist
            if (!AssetExists(AdvancedGeneratorPath))
            {
                var advancedGenerator = ScriptableObject.CreateInstance<AdvancedVoxelGenerator>();
                
                // Set up default depth layers with strength
                advancedGenerator.depthLayers.Clear();
                advancedGenerator.depthLayers.Add(new AdvancedVoxelGenerator.DepthLayer 
                { 
                    minDepth = 0f, 
                    textureIndex = 0,
                    destructible = true  // Surface: softer
                });
                advancedGenerator.depthLayers.Add(new AdvancedVoxelGenerator.DepthLayer 
                { 
                    minDepth = 10f, 
                    textureIndex = 1,
                    destructible = true  // Mid-depth: medium
                });
                advancedGenerator.depthLayers.Add(new AdvancedVoxelGenerator.DepthLayer 
                { 
                    minDepth = 20f, 
                    textureIndex = 2,
                    destructible = false  // Deep: harder
                });
                
                // Set up default noise layers
                advancedGenerator.noiseLayers.Clear();
                
                // Layer 1: Large-scale variation
                advancedGenerator.noiseLayers.Add(new AdvancedVoxelGenerator.NoiseLayer
                {
                    scale = 20f,
                    octaves = 2,
                    persistence = 0.5f,
                    destructible = false,
                    textureIndex = -1,  // No texture override
                    blendMode = AdvancedVoxelGenerator.NoiseBlendMode.Add,
                    threshold = 0f
                });
                
                AssetDatabase.CreateAsset(advancedGenerator, AdvancedGeneratorPath);
                Debug.Log($"Created default Advanced Voxel Generator at: {AdvancedGeneratorPath}");
            }
            else
            {
                Debug.Log($"Advanced Voxel Generator already exists at: {AdvancedGeneratorPath}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            Debug.Log("Default voxel generator assets created successfully!");
        }

        private static bool AssetExists(string path)
        {
            return AssetDatabase.LoadAssetAtPath<ScriptableObject>(path) != null;
        }

        /// <summary>
        /// Get or create the default simple generator
        /// </summary>
        public static SimpleVoxelGenerator GetOrCreateDefaultSimpleGenerator()
        {
            var generator = AssetDatabase.LoadAssetAtPath<SimpleVoxelGenerator>(SimpleGeneratorPath);
            if (generator == null)
            {
                CreateDefaultGeneratorAssets();
                generator = AssetDatabase.LoadAssetAtPath<SimpleVoxelGenerator>(SimpleGeneratorPath);
            }
            return generator;
        }

        /// <summary>
        /// Get or create the default advanced generator
        /// </summary>
        public static AdvancedVoxelGenerator GetOrCreateDefaultAdvancedGenerator()
        {
            var generator = AssetDatabase.LoadAssetAtPath<AdvancedVoxelGenerator>(AdvancedGeneratorPath);
            if (generator == null)
            {
                CreateDefaultGeneratorAssets();
                generator = AssetDatabase.LoadAssetAtPath<AdvancedVoxelGenerator>(AdvancedGeneratorPath);
            }
            return generator;
        }
    }
}

