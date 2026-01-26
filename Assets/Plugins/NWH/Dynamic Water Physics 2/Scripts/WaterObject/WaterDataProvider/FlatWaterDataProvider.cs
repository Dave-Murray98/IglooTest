// ╔════════════════════════════════════════════════════════════════╗
// ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// ║    Licensed under Unity Asset Store Terms of Service:          ║
// ║        https://unity.com/legal/as-terms                        ║
// ║    Use permitted only in compliance with the License.          ║
// ║    Distributed "AS IS", without warranty of any kind.          ║
// ╚════════════════════════════════════════════════════════════════╝

#region

using NWH.Common.Utility;
using NWH.DWP2.WaterObjects;
using UnityEngine;

#endregion

namespace NWH.DWP2.WaterData
{
    /// <summary>
    /// Simple water data provider for flat, static water surfaces.
    /// Uses a constant water height based on the transform's Y position.
    /// Does not support water height queries, waves, normals, or flow.
    /// </summary>
    public class FlatWaterDataProvider : WaterDataProvider
    {
        public override bool SupportsWaterHeightQueries()
        {
            return false;
        }


        public override bool SupportsWaterNormalQueries()
        {
            return false;
        }


        public override bool SupportsWaterFlowQueries()
        {
            return false;
        }


        public override void GetWaterHeights(WaterObject waterObject, ref Vector3[] points, ref float[] waterHeights)
        {
            float waterHeight = transform.position.y;
            waterHeights.Fill(waterHeight);
        }
    }
}


#if UNITY_EDITOR

namespace NWH.DWP2.WaterData
{
    using UnityEditor;

    [CustomEditor(typeof(FlatWaterDataProvider))]
    [CanEditMultipleObjects]
    public class FlatWaterDataProviderEditor : WaterDataProviderEditor
    {
        protected override void DrawStatus(WaterDataProvider provider)
        {
            drawer.BeginSubsection("Status");
            drawer.Info($"Water height: {provider.transform.position.y:F2}m (from transform Y position)");
            drawer.EndSubsection();
        }
    }
}

#endif