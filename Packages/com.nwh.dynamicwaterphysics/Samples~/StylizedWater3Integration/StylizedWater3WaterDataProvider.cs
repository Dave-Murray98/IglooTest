// ╔════════════════════════════════════════════════════════════════╗
// ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// ║    Licensed under Unity Asset Store Terms of Service:          ║
// ║        https://unity.com/legal/as-terms                        ║
// ║    Use permitted only in compliance with the License.          ║
// ║    Distributed "AS IS", without warranty of any kind.          ║
// ╚════════════════════════════════════════════════════════════════╝

#region

using StylizedWater3;
using UnityEngine;

#endregion

namespace NWH.DWP2.WaterData
{
    // Requires assembly reference to SW3 asmdef, as well as unity.matehmatics asmdef
    public class StylizedWater3WaterDataProvider : WaterDataProvider
    {
        public WaveProfile waveProfile;

        [Tooltip("Reference to the Stylized Water 3 water surface object to query heights from.")]
        public StylizedWater3.WaterObject stylizedWater3Surface;

        private HeightQuerySystem.Interface heightInterface;
        private HeightQuerySystem.Sampler   heightSampler;


        private void Reset()
        {
            // Auto-find StylizedWater3 surface
            if (stylizedWater3Surface == null)
            {
                stylizedWater3Surface = FindAnyObjectByType<StylizedWater3.WaterObject>();
            }
        }


        public override void Awake()
        {
            base.Awake();

            if (stylizedWater3Surface is null)
            {
                Debug.LogError($"Stylized Water 3 surface not assigned. " +
                               $"Please assign a reference to the SW3 water surface in the inspector.");
                return;
            }

            if (waveProfile is null)
            {
                Debug.LogError($"{typeof(WaveProfile)} not set. {GetType()} needs to have a set {typeof(WaveProfile)}");
                return;
            }

            heightSampler = new HeightQuerySystem.Sampler();
            heightSampler.SetSampleCount(4);

            heightInterface             = new HeightQuerySystem.Interface();
            heightInterface.method      = HeightQuerySystem.Interface.Method.CPU;
            heightInterface.waterObject = stylizedWater3Surface;
            heightInterface.waveProfile = waveProfile;
            heightInterface.autoFind    = false;
        }


        public override bool SupportsWaterFlowQueries()
        {
            return false;
        }


        public override bool SupportsWaterHeightQueries()
        {
            return true;
        }


        public override bool SupportsWaterNormalQueries()
        {
            return false;
        }


        public override void GetWaterHeights(WaterObjects.WaterObject waterObject, ref Vector3[] points,
            ref float[]                                               waterHeights)
        {
            if (heightInterface.HasMissingReferences())
            {
                return;
            }

            heightSampler.SetSampleCount(points.Length, true);
            for (int i = 0; i < points.Length; i++)
            {
                heightSampler.SetSamplePosition(i, points[i]);
            }

            Gerstner.ComputeHeight(heightSampler, heightInterface);

            for (int i = 0; i < points.Length; i++)
            {
                waterHeights[i] = heightSampler.heightValues[i];
            }
        }


        private void OnDestroy()
        {
            heightSampler.Dispose();
        }
    }
}


#if UNITY_EDITOR

namespace NWH.DWP2.WaterData
{
    using UnityEditor;

    [CustomEditor(typeof(StylizedWater3WaterDataProvider))]
    [CanEditMultipleObjects]
    public class StylizedWater3WaterDataProviderEditor : WaterDataProviderEditor
    {
        protected override void DrawStatus(WaterDataProvider provider)
        {
            var sw3Provider = (StylizedWater3WaterDataProvider)provider;

            drawer.BeginSubsection("Status");
            if (sw3Provider.stylizedWater3Surface != null)
            {
                drawer.Info($"SW3 Water Surface: {sw3Provider.stylizedWater3Surface.name}");
                drawer.Info($"Water base height: {sw3Provider.stylizedWater3Surface.transform.position.y:F2}m");
            }
            else
            {
                drawer.Info("Stylized Water 3 surface not assigned.", MessageType.Error);
            }

            if (sw3Provider.waveProfile != null)
            {
                drawer.Info($"Wave Profile: {sw3Provider.waveProfile.name}");
            }
            else
            {
                drawer.Info("Wave Profile not assigned.", MessageType.Error);
            }
            drawer.EndSubsection();
        }

        protected override void DrawSettings(WaterDataProvider provider)
        {
            drawer.BeginSubsection("Settings");
            drawer.Field("stylizedWater3Surface");
            drawer.Field("waveProfile");
            drawer.EndSubsection();
        }
    }
}

#endif