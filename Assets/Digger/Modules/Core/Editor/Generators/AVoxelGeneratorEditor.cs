using Digger.Modules.Core.Sources.Generators;
using UnityEngine;

namespace Digger.Modules.Core.Editor.Generators
{
    public abstract class AVoxelGeneratorEditor
    {
        protected IVoxelGenerator generator;

        public void SetGenerator(IVoxelGenerator voxelGenerator)
        {
            generator = voxelGenerator;
        }

        public abstract void OnEnable();
        public abstract void OnDisable();
        public abstract void OnInspectorGUI();
    }
}

