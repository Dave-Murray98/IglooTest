using UnityEditor;

namespace Digger.Modules.Core.Editor.Generators
{
    [VoxelGeneratorAttr("Simple", 0)]
    public class SimpleVoxelGeneratorEditor : AVoxelGeneratorEditor
    {
        public override void OnEnable()
        {
        }

        public override void OnDisable()
        {
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "Simple Voxel Generator\n\n" +
                "This is the default voxel generator. It generates voxels based on the terrain heightmap with no additional processing.\n\n" +
                "The voxel's signed distance field (SDF) value is calculated as the difference between the voxel's altitude and the terrain height at that position.",
                MessageType.Info);
        }
    }
}

