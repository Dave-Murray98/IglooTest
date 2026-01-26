// ╔════════════════════════════════════════════════════════════════╗
// ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// ║    Licensed under Unity Asset Store Terms of Service:          ║
// ║        https://unity.com/legal/as-terms                        ║
// ║    Use permitted only in compliance with the License.          ║
// ║    Distributed "AS IS", without warranty of any kind.          ║
// ╚════════════════════════════════════════════════════════════════╝

#region

using NWH.DWP2.WaterObjects;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

#endregion

namespace NWH.DWP2.WaterData
{
    [DefaultExecutionOrder(-50)]
    public class UnityHDRPWaterDataProvider : WaterDataProvider
    {
        private const int                 START_BUFFER_SIZE = 64;
        private       int                 bufferSize;
        private       NativeArray<float3> candidatePositionBuffer;
        private       NativeArray<float>  errorBuffer;

        private NativeArray<float3> projectedPositionBuffer;

        private WaterSearchParameters searchParams;
        private WaterSearchResult     searchResult;
        private NativeArray<int>      stepCountBuffer;

        private NativeArray<float3> targetPositionBuffer;
        private WaterSurface        targetSurface;


        public override void Awake()
        {
            base.Awake();

            targetSurface = FindAnyObjectByType<WaterSurface>();
            if (targetSurface is null)
            {
                Debug.LogError($"{typeof(WaterSurface)} not found in scene. " +
                               $"{GetType()} requires a {typeof(WaterSurface)} to be present in the scene.");
                return;
            }

            bufferSize = START_BUFFER_SIZE;

            targetPositionBuffer    = new NativeArray<float3>(bufferSize, Allocator.Persistent);
            projectedPositionBuffer = new NativeArray<float3>(bufferSize, Allocator.Persistent);
            errorBuffer             = new NativeArray<float>(bufferSize, Allocator.Persistent);
            candidatePositionBuffer = new NativeArray<float3>(bufferSize, Allocator.Persistent);
            stepCountBuffer         = new NativeArray<int>(bufferSize, Allocator.Persistent);
        }


        private void CheckAndResizeBuffers(int newSize)
        {
            if (newSize <= bufferSize)
            {
                return;
            }

            bufferSize = newSize >= 2 * bufferSize ? newSize : bufferSize * 2;

            targetPositionBuffer.Dispose();
            targetPositionBuffer = new NativeArray<float3>(bufferSize, Allocator.Persistent);

            projectedPositionBuffer.Dispose();
            projectedPositionBuffer = new NativeArray<float3>(bufferSize, Allocator.Persistent);

            errorBuffer.Dispose();
            errorBuffer = new NativeArray<float>(bufferSize, Allocator.Persistent);

            candidatePositionBuffer.Dispose();
            candidatePositionBuffer = new NativeArray<float3>(bufferSize, Allocator.Persistent);

            stepCountBuffer.Dispose();
            stepCountBuffer = new NativeArray<int>(bufferSize, Allocator.Persistent);
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


        public override float GetWaterHeightSingle(WaterObject waterObject, Vector3 point)
        {
            searchParams.startPositionWS  = searchResult.candidateLocationWS;
            searchParams.targetPositionWS = waterObject.transform.position;
            searchParams.error            = 0.01f;
            searchParams.maxIterations    = 8;

            if (targetSurface.ProjectPointOnWaterSurface(searchParams, out searchResult))
            {
                return searchResult.projectedPositionWS.y;
            }

            Debug.LogError("Cannot find height");
            return 0f;
        }


        public override void GetWaterHeights(WaterObject waterObject, ref Vector3[] points, ref float[] waterHeights)
        {
            WaterSimSearchData simData = new();
            if (!targetSurface.FillWaterSearchData(ref simData))
            {
                Debug.LogError("Cannot retrieve water search data!");
                return;
            }

            int nPoints = points.Length;
            CheckAndResizeBuffers(nPoints);

            for (int i = 0; i < nPoints; i++)
            {
                targetPositionBuffer[i] = points[i];
            }

            WaterSimulationSearchJob job = new()
            {
                simSearchData          = simData,
                targetPositionWSBuffer = targetPositionBuffer,
                startPositionWSBuffer  = targetPositionBuffer,
                error                  = 0.01f,
                maxIterations          = 8,
            };

            job.projectedPositionWSBuffer = projectedPositionBuffer;
            job.errorBuffer               = errorBuffer;
            job.candidateLocationWSBuffer = candidatePositionBuffer;
            job.stepCountBuffer           = stepCountBuffer;

            JobHandle handle = job.Schedule(nPoints, 1);
            handle.Complete();

            for (int i = 0; i < nPoints; i++)
            {
                waterHeights[i] = projectedPositionBuffer[i].y;
            }
        }


        private void OnDestroy()
        {
            targetPositionBuffer.Dispose();
            projectedPositionBuffer.Dispose();
            errorBuffer.Dispose();
            candidatePositionBuffer.Dispose();
            stepCountBuffer.Dispose();
        }
    }
}


#if UNITY_EDITOR

namespace NWH.DWP2.WaterData
{
    using UnityEditor;
    using UnityEngine.Rendering.HighDefinition;

    [CustomEditor(typeof(UnityHDRPWaterDataProvider))]
    [CanEditMultipleObjects]
    public class UnityHDRPWaterDataProviderEditor : WaterDataProviderEditor
    {
        protected override void DrawStatus(WaterDataProvider provider)
        {
            drawer.BeginSubsection("Status");
            var waterSurface = FindAnyObjectByType<WaterSurface>();
            if (waterSurface != null)
            {
                drawer.Info($"HDRP Water Surface found in scene: {waterSurface.name}");
            }
            else
            {
                drawer.Info("HDRP WaterSurface not found in scene.", MessageType.Error);
            }
            drawer.EndSubsection();
        }
    }
}

#endif