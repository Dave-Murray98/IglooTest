using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

namespace NWH.Common.Editor
{
    /// <summary>
    /// Converts materials in a folder from URP shaders to Built-in render pipeline shaders.
    /// </summary>
    public static class URPToBuiltInConverter
    {
        private static readonly Dictionary<string, string> ShaderMapping = new Dictionary<string, string>
        {
            // Lit shaders
            { "Universal Render Pipeline/Lit", "Standard" },
            { "Universal Render Pipeline/Simple Lit", "Mobile/Diffuse" },
            { "Universal Render Pipeline/Baked Lit", "Legacy Shaders/Lightmapped/Diffuse" },
            { "Universal Render Pipeline/Unlit", "Unlit/Texture" },

            // Particle shaders
            { "Universal Render Pipeline/Particles/Lit", "Particles/Standard Surface" },
            { "Universal Render Pipeline/Particles/Unlit", "Particles/Standard Unlit" },
            { "Universal Render Pipeline/Particles/Simple Lit", "Particles/Standard Surface" },

            // Terrain shaders
            { "Universal Render Pipeline/Terrain/Lit", "Nature/Terrain/Standard" },

            // Nature/SpeedTree shaders
            { "Universal Render Pipeline/Nature/SpeedTree7", "Nature/SpeedTree" },
            { "Universal Render Pipeline/Nature/SpeedTree7 Billboard", "Nature/SpeedTree Billboard" },
            { "Universal Render Pipeline/Nature/SpeedTree8", "Nature/SpeedTree8" },
        };

        // Property mappings from URP to Built-in
        private static readonly Dictionary<string, string> TexturePropertyMapping = new Dictionary<string, string>
        {
            { "_BaseMap", "_MainTex" },
            { "_BumpMap", "_BumpMap" },
            { "_NormalMap", "_BumpMap" },
            { "_EmissionMap", "_EmissionMap" },
            { "_OcclusionMap", "_OcclusionMap" },
            { "_MetallicGlossMap", "_MetallicGlossMap" },
            { "_SpecGlossMap", "_SpecGlossMap" },
            { "_ParallaxMap", "_ParallaxMap" },
            { "_DetailAlbedoMap", "_DetailAlbedoMap" },
            { "_DetailNormalMap", "_DetailNormalMap" },
            { "_DetailMask", "_DetailMask" },
        };

        private static readonly Dictionary<string, string> ColorPropertyMapping = new Dictionary<string, string>
        {
            { "_BaseColor", "_Color" },
            { "_EmissionColor", "_EmissionColor" },
            { "_SpecColor", "_SpecColor" },
        };

        private static readonly Dictionary<string, string> FloatPropertyMapping = new Dictionary<string, string>
        {
            { "_Smoothness", "_Glossiness" },
            { "_Metallic", "_Metallic" },
            { "_BumpScale", "_BumpScale" },
            { "_OcclusionStrength", "_OcclusionStrength" },
            { "_Cutoff", "_Cutoff" },
            { "_Parallax", "_Parallax" },
            { "_DetailNormalMapScale", "_DetailNormalMapScale" },
        };

        [MenuItem("Tools/NWH/Convert Folder URP to Built-in")]
        public static void ConvertFolder()
        {
            string folderPath = EditorUtility.OpenFolderPanel("Select Folder to Convert", "Assets", "");

            if (string.IsNullOrEmpty(folderPath))
            {
                return;
            }

            // Convert to relative path if inside project
            string projectPath = Application.dataPath;
            string relativePath;

            if (folderPath.StartsWith(projectPath))
            {
                relativePath = "Assets" + folderPath.Substring(projectPath.Length);
            }
            else if (folderPath.Contains("Packages"))
            {
                int packagesIndex = folderPath.IndexOf("Packages");
                relativePath = folderPath.Substring(packagesIndex);
            }
            else
            {
                Debug.LogError("[URPToBuiltIn] Selected folder must be inside the project.");
                return;
            }

            ConvertMaterialsInFolder(folderPath, relativePath);
        }

        public static void ConvertMaterialsInFolder(string absolutePath, string relativePath)
        {
            if (!Directory.Exists(absolutePath))
            {
                Debug.LogError($"[URPToBuiltIn] Folder not found: {absolutePath}");
                return;
            }

            string[] materialFiles = Directory.GetFiles(absolutePath, "*.mat", SearchOption.AllDirectories);

            if (materialFiles.Length == 0)
            {
                Debug.Log("[URPToBuiltIn] No material files found in the selected folder.");
                return;
            }

            int converted = 0;
            int skipped = 0;
            int failed = 0;
            List<string> warnings = new List<string>();

            try
            {
                AssetDatabase.StartAssetEditing();

                for (int i = 0; i < materialFiles.Length; i++)
                {
                    string filePath = materialFiles[i].Replace("\\", "/");
                    float progress = (float)i / materialFiles.Length;

                    if (EditorUtility.DisplayCancelableProgressBar(
                        "Converting Materials",
                        $"Processing {Path.GetFileName(filePath)} ({i + 1}/{materialFiles.Length})",
                        progress))
                    {
                        Debug.Log("[URPToBuiltIn] Conversion cancelled by user.");
                        break;
                    }

                    // Convert absolute path to asset path
                    string assetPath = ConvertToAssetPath(filePath);
                    if (string.IsNullOrEmpty(assetPath))
                    {
                        warnings.Add($"Could not resolve asset path: {filePath}");
                        failed++;
                        continue;
                    }

                    Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
                    if (material == null)
                    {
                        warnings.Add($"Could not load material: {assetPath}");
                        failed++;
                        continue;
                    }

                    if (material.shader == null)
                    {
                        warnings.Add($"Material has no shader: {assetPath}");
                        failed++;
                        continue;
                    }

                    string shaderName = material.shader.name;

                    // Check if this is a URP shader
                    if (!IsURPShader(shaderName))
                    {
                        skipped++;
                        continue;
                    }

                    // Find target shader
                    string targetShaderName = GetTargetShaderName(shaderName);
                    Shader targetShader = Shader.Find(targetShaderName);

                    if (targetShader == null)
                    {
                        warnings.Add($"Target shader not found '{targetShaderName}' for material: {assetPath}");
                        failed++;
                        continue;
                    }

                    // Convert the material
                    if (ConvertMaterial(material, targetShader))
                    {
                        EditorUtility.SetDirty(material);
                        converted++;
                    }
                    else
                    {
                        failed++;
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                EditorUtility.ClearProgressBar();
            }

            // Log summary
            Debug.Log($"[URPToBuiltIn] Conversion complete:\n" +
                     $"  Converted: {converted}\n" +
                     $"  Skipped (not URP): {skipped}\n" +
                     $"  Failed: {failed}");

            if (warnings.Count > 0)
            {
                Debug.LogWarning("[URPToBuiltIn] Warnings:\n" + string.Join("\n", warnings));
            }
        }

        private static string ConvertToAssetPath(string absolutePath)
        {
            absolutePath = absolutePath.Replace("\\", "/");

            // Check for Assets folder
            int assetsIndex = absolutePath.IndexOf("/Assets/");
            if (assetsIndex >= 0)
            {
                return absolutePath.Substring(assetsIndex + 1);
            }

            // Check for Packages folder
            int packagesIndex = absolutePath.IndexOf("/Packages/");
            if (packagesIndex >= 0)
            {
                return absolutePath.Substring(packagesIndex + 1);
            }

            return null;
        }

        private static bool IsURPShader(string shaderName)
        {
            return shaderName.StartsWith("Universal Render Pipeline") ||
                   shaderName.StartsWith("Shader Graphs/") ||
                   shaderName.Contains("URP") ||
                   shaderName.Contains("/Universal/");
        }

        private static string GetTargetShaderName(string urpShaderName)
        {
            if (ShaderMapping.TryGetValue(urpShaderName, out string targetName))
            {
                return targetName;
            }

            // Fallback to Standard for unknown URP shaders
            return "Standard";
        }

        private static bool ConvertMaterial(Material material, Shader targetShader)
        {
            try
            {
                // Cache current properties before changing shader
                var cachedTextures = new Dictionary<string, Texture>();
                var cachedColors = new Dictionary<string, Color>();
                var cachedFloats = new Dictionary<string, float>();
                var cachedKeywords = new HashSet<string>(material.shaderKeywords);

                // Cache textures
                foreach (var mapping in TexturePropertyMapping)
                {
                    if (material.HasProperty(mapping.Key))
                    {
                        Texture tex = material.GetTexture(mapping.Key);
                        if (tex != null)
                        {
                            cachedTextures[mapping.Value] = tex;
                        }
                    }
                }

                // Cache colors
                foreach (var mapping in ColorPropertyMapping)
                {
                    if (material.HasProperty(mapping.Key))
                    {
                        cachedColors[mapping.Value] = material.GetColor(mapping.Key);
                    }
                }

                // Cache floats
                foreach (var mapping in FloatPropertyMapping)
                {
                    if (material.HasProperty(mapping.Key))
                    {
                        cachedFloats[mapping.Value] = material.GetFloat(mapping.Key);
                    }
                }

                // Cache render mode settings
                float surface = material.HasProperty("_Surface") ? material.GetFloat("_Surface") : 0;
                float alphaClip = material.HasProperty("_AlphaClip") ? material.GetFloat("_AlphaClip") : 0;
                float blend = material.HasProperty("_Blend") ? material.GetFloat("_Blend") : 0;

                // Change shader
                material.shader = targetShader;

                // Apply cached textures
                foreach (var kvp in cachedTextures)
                {
                    if (material.HasProperty(kvp.Key))
                    {
                        material.SetTexture(kvp.Key, kvp.Value);
                    }
                }

                // Apply cached colors
                foreach (var kvp in cachedColors)
                {
                    if (material.HasProperty(kvp.Key))
                    {
                        material.SetColor(kvp.Key, kvp.Value);
                    }
                }

                // Apply cached floats
                foreach (var kvp in cachedFloats)
                {
                    if (material.HasProperty(kvp.Key))
                    {
                        material.SetFloat(kvp.Key, kvp.Value);
                    }
                }

                // Handle emission
                if (cachedColors.TryGetValue("_EmissionColor", out Color emissionColor))
                {
                    if (emissionColor.r > 0 || emissionColor.g > 0 || emissionColor.b > 0)
                    {
                        material.EnableKeyword("_EMISSION");
                        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    }
                }

                // Set render mode for Standard shader
                if (targetShader.name == "Standard")
                {
                    SetupStandardShaderRenderMode(material, surface, alphaClip, blend);
                }

                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[URPToBuiltIn] Error converting material {material.name}: {e.Message}");
                return false;
            }
        }

        private static void SetupStandardShaderRenderMode(Material material, float surface, float alphaClip, float blend)
        {
            // URP: _Surface 0=Opaque, 1=Transparent
            // URP: _AlphaClip 0=Off, 1=On
            // Standard modes: 0=Opaque, 1=Cutout, 2=Fade, 3=Transparent

            if (surface == 0) // Opaque
            {
                if (alphaClip > 0.5f)
                {
                    // Cutout mode
                    material.SetFloat("_Mode", 1);
                    material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                    material.SetInt("_ZWrite", 1);
                    material.EnableKeyword("_ALPHATEST_ON");
                    material.DisableKeyword("_ALPHABLEND_ON");
                    material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                }
                else
                {
                    // Opaque mode
                    material.SetFloat("_Mode", 0);
                    material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                    material.SetInt("_ZWrite", 1);
                    material.DisableKeyword("_ALPHATEST_ON");
                    material.DisableKeyword("_ALPHABLEND_ON");
                    material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    material.renderQueue = -1;
                }
            }
            else // Transparent
            {
                if (blend == 0) // Alpha blend
                {
                    // Fade mode
                    material.SetFloat("_Mode", 2);
                    material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    material.SetInt("_ZWrite", 0);
                    material.DisableKeyword("_ALPHATEST_ON");
                    material.EnableKeyword("_ALPHABLEND_ON");
                    material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                }
                else // Premultiply or Additive
                {
                    // Transparent mode
                    material.SetFloat("_Mode", 3);
                    material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    material.SetInt("_ZWrite", 0);
                    material.DisableKeyword("_ALPHATEST_ON");
                    material.DisableKeyword("_ALPHABLEND_ON");
                    material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                    material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                }
            }
        }
    }
}
