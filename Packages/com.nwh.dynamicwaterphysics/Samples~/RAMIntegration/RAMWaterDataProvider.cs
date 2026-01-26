// // ╔════════════════════════════════════════════════════════════════╗
// // ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// // ║    Licensed under Unity Asset Store Terms of Service:          ║
// // ║        https://unity.com/legal/as-terms                        ║
// // ║    Use permitted only in compliance with the License.          ║
// // ║    Distributed "AS IS", without warranty of any kind.          ║
// // ╚════════════════════════════════════════════════════════════════╝

#region

using NWH.DWP2.WaterObjects;
using UnityEngine;

#endregion

namespace NWH.DWP2.WaterData
{
    /// <summary>
    /// Version of RaycastWaterDataProvider with support for R.A.M. flow data.
    /// </summary>
    public class RAMWaterDataProvider : RaycastWaterDataProvider
    {
        public  bool        lakeFlow;
        public  bool        riverFlow = true;
        private LakePolygon _prevRamPolygon;
        private RamSpline   _prevRamSpline;
        private LakePolygon _ramPolygon;

        private RamSpline _ramSpline;


        public override void Awake()
        {
            base.Awake();
            Physics.IgnoreLayerCollision(waterLayer, objectLayer);
            _rayDirection   = -Vector3.up;
            _rayStartOffset = -_rayDirection * raycastDistance * 0.5f;
            _prevDataSize   = -1;
        }


        public override bool SupportsWaterFlowQueries()
        {
            return true;
        }


        public override void GetWaterFlows(WaterObject waterObject, ref Vector3[] points, ref Vector3[] waterFlows)
        {
            _flow = Vector3.zero;

            bool queriesHitBackfaces = Physics.queriesHitBackfaces;
            Physics.queriesHitBackfaces = false;

            _ray.origin    = waterObject.transform.position + _rayStartOffset;
            _ray.direction = _rayDirection;
            if (Physics.Raycast(_ray, out _hit, raycastDistance, _layerMask, QueryTriggerInteraction.Ignore) &&
                _hit.collider != null)
            {
                _ramSpline = _hit.collider.GetComponent<RamSpline>();
                if (riverFlow && _ramSpline != null)
                {
                    if (_ramSpline != _prevRamSpline)
                    {
                        _mesh      = _ramSpline.meshfilter.sharedMesh;
                        _vertIndex = _mesh.triangles[_hit.triangleIndex * 3];
                        _vertDir   = _ramSpline.verticeDirection[_vertIndex];
                        _uv4       = _mesh.uv4[_vertIndex];
                        _tmp.x     = _vertDir.z;
                        _tmp.y     = _vertDir.y;
                        _tmp.z     = -_vertDir.x;
                        _vertDir   = _vertDir * _uv4.y - _tmp * _uv4.x;

                        _flow.x = _vertDir.x * _ramSpline.floatSpeed;
                        _flow.y = 0;
                        _flow.z = _vertDir.z * _ramSpline.floatSpeed;
                    }
                }
                else if (lakeFlow)
                {
                    _ramPolygon = _hit.collider.GetComponent<LakePolygon>();
                    if (_ramPolygon != null)
                    {
                        if (_ramPolygon != _prevRamPolygon)
                        {
                            _mesh      = _ramPolygon.meshfilter.sharedMesh;
                            _vertIndex = _mesh.triangles[_hit.triangleIndex * 3];
                            _uv4       = -_mesh.uv4[_vertIndex];
                            _vertDir.x = _uv4.x;
                            _vertDir.y = 0;
                            _vertDir.z = _uv4.y;

                            _flow.x = _vertDir.x * _ramPolygon.floatSpeed;
                            _flow.y = 0;
                            _flow.z = _vertDir.z * _ramPolygon.floatSpeed;
                        }
                    }
                }

                _prevRamPolygon = _ramPolygon;
                _prevRamSpline  = _ramSpline;

                for (int d = 0; d < waterFlows.Length; d++)
                {
                    waterFlows[d] = _flow;
                }
            }

            _prevRamPolygon = null;
            _prevRamSpline  = null;

            Physics.queriesHitBackfaces = queriesHitBackfaces;
        }
    }
}


#if UNITY_EDITOR

namespace NWH.DWP2.WaterData
{
    using UnityEditor;

    [CustomEditor(typeof(RAMWaterDataProvider))]
    [CanEditMultipleObjects]
    public class RAMWaterDataProviderEditor : WaterDataProviderEditor
    {
        protected override void DrawStatus(WaterDataProvider provider)
        {
            drawer.BeginSubsection("Status");
            drawer.Info("Uses raycasts to detect RAM water surfaces (RamSpline/LakePolygon).");
            drawer.Info("Flow data is extracted from RAM mesh vertex information.");
            drawer.EndSubsection();
        }

        protected override void DrawSettings(WaterDataProvider provider)
        {
            drawer.BeginSubsection("Flow Settings");
            drawer.Field("riverFlow");
            drawer.Field("lakeFlow");
            drawer.EndSubsection();

            drawer.BeginSubsection("Raycast Settings");
            drawer.Field("waterLayer");
            drawer.Field("objectLayer");
            drawer.Field("raycastDistance", true, "m");
            drawer.Field("commandsPerJob");
            drawer.EndSubsection();
        }
    }
}

#endif