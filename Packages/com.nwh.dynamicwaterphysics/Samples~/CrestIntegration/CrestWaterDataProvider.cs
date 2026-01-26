// // ╔════════════════════════════════════════════════════════════════╗
// // ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// // ║    Licensed under Unity Asset Store Terms of Service:          ║
// // ║        https://unity.com/legal/as-terms                        ║
// // ║    Use permitted only in compliance with the License.          ║
// // ║    Distributed "AS IS", without warranty of any kind.          ║
// // ╚════════════════════════════════════════════════════════════════╝

using System.Collections.Generic;
using UnityEngine;
using NWH.DWP2.WaterObjects;

#if DWP_CREST_LEGACY
using Crest;
#else
using WaveHarmonic.Crest;
using OceanRenderer = WaveHarmonic.Crest.WaterRenderer;
#endif

namespace NWH.DWP2.WaterData
{
    [DefaultExecutionOrder(-50)]
    public class CrestWaterDataProvider : WaterDataProvider
    {
        private OceanRenderer _oceanRenderer;


        public override bool SupportsWaterHeightQueries()
        {
            return true;
        }


        public override bool SupportsWaterNormalQueries()
        {
            return true;
        }


        public override bool SupportsWaterFlowQueries()
        {
            return true;
        }


        public override void Awake()
        {
            base.Awake();

            _oceanRenderer = FindAnyObjectByType<OceanRenderer>();
            if (_oceanRenderer == null)
            {
                Debug.LogError($"{typeof(OceanRenderer)} not found in scene. " +
                               $"{GetType()} requires a {typeof(OceanRenderer)} to be present in the scene.");
            }
        }


        public override void GetWaterHeights(WaterObject waterObject, ref Vector3[] points, ref float[] waterHeights)
        {
            if (_oceanRenderer == null) return;

            var provider = _oceanRenderer.CollisionProvider;
            int status = provider.Query(waterObject.instanceID, 0, points, waterHeights, null, null);

            // If retrieval failed, Crest did not modify waterHeights - fill with SeaLevel as fallback
            if (!provider.RetrieveSucceeded(status))
            {
                float seaLevel = _oceanRenderer.SeaLevel;
                for (int i = 0; i < waterHeights.Length; i++)
                {
                    waterHeights[i] = seaLevel;
                }
            }
        }


        public override void GetWaterNormals(WaterObject waterObject, ref Vector3[] points, ref Vector3[] waterNormals)
        {
            if (_oceanRenderer == null) return;

            var provider = _oceanRenderer.CollisionProvider;
            // Flip the instance sign to not get overlapping queries for same provider. InstanceID is always negative so
            // no duplicates should ever happen.
            int status = provider.Query(-waterObject.instanceID, 0, points, (float[])null, waterNormals, null);

            // If retrieval failed, fill with up vector as fallback
            if (!provider.RetrieveSucceeded(status))
            {
                for (int i = 0; i < waterNormals.Length; i++)
                {
                    waterNormals[i] = Vector3.up;
                }
            }
        }


        public override void GetWaterFlows(WaterObject waterObject, ref Vector3[] points, ref Vector3[] waterFlows)
        {
            if (_oceanRenderer == null) return;

            var provider = _oceanRenderer.FlowProvider;
            int status = provider.Query(waterObject.instanceID, 0, points, waterFlows);

            // If retrieval failed, fill with zero flow as fallback
            if (!provider.RetrieveSucceeded(status))
            {
                for (int i = 0; i < waterFlows.Length; i++)
                {
                    waterFlows[i] = Vector3.zero;
                }
            }
        }
    }
}


#if UNITY_EDITOR

namespace NWH.DWP2.WaterData
{
    using UnityEditor;

    [CustomEditor(typeof(CrestWaterDataProvider))]
    [CanEditMultipleObjects]
    public class CrestWaterDataProviderEditor : WaterDataProviderEditor
    {
        protected override void DrawStatus(WaterDataProvider provider)
        {
            drawer.BeginSubsection("Status");

#if DWP_CREST_LEGACY
            drawer.Info("Using Legacy Crest (DWP_CREST_LEGACY defined)");
#else
            drawer.Info("Using Crest 5+ (WaveHarmonic.Crest)");
#endif

            var oceanRenderer = FindAnyObjectByType<OceanRenderer>();
            if (oceanRenderer != null)
            {
                drawer.Info($"Crest Water Renderer found in scene: {oceanRenderer.name}");
            }
            else
            {
                drawer.Info("Crest Water Renderer (OceanRenderer) not found in scene.", MessageType.Error);
            }
            drawer.EndSubsection();
        }
    }
}

#endif