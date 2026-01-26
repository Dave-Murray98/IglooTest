using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class BakedSkinnedMesh : MonoBehaviour
{
    [SerializeField] private SkinnedMeshRenderer skinnedMeshRenderer;
    [SerializeField] private string savePath = "Assets/Resources/Art/Models/Environment/Corpses/";

    [Button]
    private void BakeMesh()
    {
#if UNITY_EDITOR
        // Find the skinned mesh renderer if not assigned
        if (skinnedMeshRenderer == null)
        {
            skinnedMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
        }

        if (skinnedMeshRenderer == null)
        {
            Debug.LogError("No SkinnedMeshRenderer found!");
            return;
        }

        // Create a new mesh and bake the current pose into it
        Mesh bakedMesh = new Mesh();
        skinnedMeshRenderer.BakeMesh(bakedMesh);
        bakedMesh.name = gameObject.name + "_BakedCorpse";

        // Make sure the save directory exists
        if (!AssetDatabase.IsValidFolder(savePath.TrimEnd('/')))
        {
            // Create the folder if it doesn't exist
            string[] folders = savePath.Split('/');
            string currentPath = folders[0];
            for (int i = 1; i < folders.Length; i++)
            {
                if (!string.IsNullOrEmpty(folders[i]))
                {
                    if (!AssetDatabase.IsValidFolder(currentPath + "/" + folders[i]))
                    {
                        AssetDatabase.CreateFolder(currentPath, folders[i]);
                    }
                    currentPath += "/" + folders[i];
                }
            }
        }

        // Save the mesh as an asset
        string fullPath = savePath + bakedMesh.name + ".asset";
        // Make sure we don't overwrite an existing mesh
        fullPath = AssetDatabase.GenerateUniqueAssetPath(fullPath);

        AssetDatabase.CreateAsset(bakedMesh, fullPath);
        AssetDatabase.SaveAssets();

        // Get or add a MeshFilter component
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = gameObject.AddComponent<MeshFilter>();
        }

        // Get or add a MeshRenderer component
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
        }

        // Assign the baked mesh and copy the materials
        meshFilter.sharedMesh = bakedMesh;
        meshRenderer.sharedMaterials = skinnedMeshRenderer.sharedMaterials;

        Debug.Log($"Mesh baked and saved to: {fullPath}");

        // Ping the asset in the project window so you can see it
        EditorGUIUtility.PingObject(bakedMesh);
#endif
    }
}