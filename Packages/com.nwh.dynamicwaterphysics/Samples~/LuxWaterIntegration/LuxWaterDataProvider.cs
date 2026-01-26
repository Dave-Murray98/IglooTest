// ╔════════════════════════════════════════════════════════════════╗
// ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// ║    Licensed under Unity Asset Store Terms of Service:          ║
// ║        https://unity.com/legal/as-terms                        ║
// ║    Use permitted only in compliance with the License.          ║
// ║    Distributed "AS IS", without warranty of any kind.          ║
// ╚════════════════════════════════════════════════════════════════╝

#region

using LuxWater;
using NWH.DWP2.WaterObjects;
using UnityEngine;

#endregion

namespace NWH.DWP2.WaterData
{
    public class LuxWaterDataProvider : WaterDataProvider
    {
        public  float                                 timeOffset = 0.06f;
        private LuxWaterUtils.GersterWavesDescription _description;
        private float                                 _waterHeightOffset;

        private Material             _waterMaterial;
        private LuxWater_WaterVolume _waterObject;


        public override void Awake()
        {
            base.Awake();

            _waterObject = FindAnyObjectByType<LuxWater_WaterVolume>();
            if (_waterObject == null)
            {
                Debug.LogError($"{typeof(LuxWater_WaterVolume)} not found in scene. " +
                               $"{GetType()} requires a {typeof(LuxWater_WaterVolume)} to be present in the scene.");
                return;
            }

            _waterMaterial = _waterObject.GetComponent<MeshRenderer>()?.sharedMaterial;
            if (_waterMaterial == null)
            {
                Debug.LogError("Lux water object does not contain a mesh renderer or material.");
                return;
            }

            LuxWaterUtils.GetGersterWavesDescription(ref _description, _waterMaterial);
            _waterHeightOffset = _waterObject.transform.position.y;
        }


        public override void GetWaterHeights(WaterObject waterObject, ref Vector3[] points, ref float[] waterHeights)
        {
            // Update wave description
            LuxWaterUtils.GetGersterWavesDescription(ref _description, _waterMaterial);
            for (int i = 0; i < points.Length; i++)
            {
                waterHeights[i] = LuxWaterUtils.GetGestnerDisplacement(points[i], _description, timeOffset).y
                                  + _waterHeightOffset;
            }
        }


        public override bool SupportsWaterHeightQueries()
        {
            return true;
        }


        public override bool SupportsWaterNormalQueries()
        {
            return false;
        }


        public override bool SupportsWaterFlowQueries()
        {
            return false;
        }
    }
}


#if UNITY_EDITOR

namespace NWH.DWP2.WaterData
{
    using UnityEditor;

    [CustomEditor(typeof(LuxWaterDataProvider))]
    [CanEditMultipleObjects]
    public class LuxWaterDataProviderEditor : WaterDataProviderEditor
    {
        protected override void DrawStatus(WaterDataProvider provider)
        {
            drawer.BeginSubsection("Status");
            var waterVolume = FindAnyObjectByType<LuxWater.LuxWater_WaterVolume>();
            if (waterVolume != null)
            {
                drawer.Info($"Lux Water Volume found in scene: {waterVolume.name}");
                var meshRenderer = waterVolume.GetComponent<UnityEngine.MeshRenderer>();
                if (meshRenderer != null && meshRenderer.sharedMaterial != null)
                {
                    drawer.Info($"Water Material: {meshRenderer.sharedMaterial.name}");
                }
                else
                {
                    drawer.Info("MeshRenderer or Material not found.", MessageType.Warning);
                }
            }
            else
            {
                drawer.Info("LuxWater_WaterVolume not found in scene.", MessageType.Error);
            }
            drawer.EndSubsection();
        }

        protected override void DrawSettings(WaterDataProvider provider)
        {
            drawer.BeginSubsection("Settings");
            drawer.Field("timeOffset", true, "s");
            drawer.EndSubsection();
        }
    }
}

#endif