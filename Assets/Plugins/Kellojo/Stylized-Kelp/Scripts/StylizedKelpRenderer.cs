using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.VFX;
using Random = UnityEngine.Random;

namespace Kellojo.StylizedKelp
{

    [DefaultExecutionOrder(1), ExecuteInEditMode]
    public class StylizedKelpRenderer : MonoBehaviour
    {

        ProfilerMarker _constraintMarker = new ProfilerMarker("Kelp Constraints");
        ProfilerMarker _simulationMarker = new ProfilerMarker("Kelp Simulation");

        ProfilerMarker _leafSimulationMarker = new ProfilerMarker("Kelp Leaf Simulation");
        ProfilerMarker _leafConstraintMarker = new ProfilerMarker("Kelp Leaf Constraints");
        ProfilerMarker _leafPointsMarker = new ProfilerMarker("Kelp Leaf Points");

        private List<KelpVerletSegment> _kelpStemSegments = new();
        private GraphicsBuffer _kelpSegmentBuffer;

        private List<KelpVerletSegment> _kelpLeafSegments = new();
        private GraphicsBuffer _kelpLeafBuffer;

        private List<KelpSettings> _kelpSettings = new();
        private List<KelpSettingsGpuData> _kelpSettingsGpuDatas = new();
        private GraphicsBuffer _kelpSettingsBuffer;

        [Header("Wind Settings")]
        [Range(0.001f, 0.03f)] public float windStrength = 0.01f;
        public float windSpeed = 1f;
        public Vector3 windScale = new Vector3(0.1f, 0.25f, 0.1f);

        [Header("Textures")]
        public Texture2D leafTexture;

        [Header("Subsurface Scattering")]
        [Tooltip("How much to boost the color, when the main light shines through the kelp. 1 is equivalent to no boost.")] public float scatteringColorBoost = 2f;
        [Range(0, 1)] public float scatteringAmount = 0.75f;

        [Header("Simulation Settings")]
        [Tooltip("How many stem constraint iterations should be performed per segment? A good default is 5. Lower is more performant.")]
        public int stemConstraintIterations = 5;
        [Tooltip("How many leaf constraint iterations should be performed per segment? A good default is 2. Lower is more performant.")]
        public int leafConstraintIterations = 2;
        [Range(0, 1)] public float dampingFactor = 0.22f;

        [Space]
        public EKelpUpdateMode updateMode = EKelpUpdateMode.FixedUpdate;
        [Min(0), Tooltip("The updates to skip, before the next update is scheduled.")] public int skipUpdates = 0;
        private int _skipUpdateCounter = 0;

        [Header("Collision Settings")]
        public bool enableCollision = true;
        public LayerMask collisionMask;
        public float segmentCollisionRadius = 0.25f;

        [Header("Performance Settings")]
        [Tooltip("Up to what distance from the player should kelp be simulated? Higher values increase performance cost.")]
        public float simulationDistance = 75f;
        [Tooltip("The transform representing the player. If left empty, the main camera will be used.")]
        public Transform playerTransform;
        private Vector3 _playerPosition;

        [Header("References")]
        public VisualEffect kelpStemVisualEffect;
        public VisualEffect kelpLeafVisualEffect;
        public ComputeShader verletKelpComputeShader;
        readonly int _segmentShaderId = Shader.PropertyToID("segments");
        readonly int _stemSegmentsId = Shader.PropertyToID("stemSegments");
        readonly int _settingsShaderId = Shader.PropertyToID("settings");
        readonly int _isLeafShaderId = Shader.PropertyToID("isLeaf");
        readonly int _constraintIterationsId = Shader.PropertyToID("constraintIterations");
        readonly int _simulationDistanceId = Shader.PropertyToID("simulationDistance");
        readonly int _playerPositionId = Shader.PropertyToID("playerPosition");
        readonly int _fixedDeltaTimeId = Shader.PropertyToID("fixedDeltaTime");
        readonly int _dampingFactorId = Shader.PropertyToID("dampingFactor");
        readonly int _windScaleId = Shader.PropertyToID("WindScale");
        readonly int _windStrengthId = Shader.PropertyToID("WindStrength");
        readonly int _windSpeedId = Shader.PropertyToID("WindSpeed");

        [Header("Debug")]
        public bool drawGizmos = false;
        public int activeKelp { get; private set; } = 0;
        public int activeKelpStems => _kelpStemSegments.Count;
        public int activeKelpLeaves => _kelpLeafSegments.Count / 3;
        public bool isInitialized { get; private set; } = false;
        private bool willInitialize { get; set; } = true;
        private Collider[] _otherColliders;
        private Transform rendererTransform;

        private void OnValidate()
        {
            if (stemConstraintIterations < 1) stemConstraintIterations = 1;
            if (leafConstraintIterations < 1) leafConstraintIterations = 1;
            if (segmentCollisionRadius < 0.01f) segmentCollisionRadius = 0.01f;
            if (simulationDistance < 1f) simulationDistance = 1f;

            ApplyVisualEffectSettings();
        }

        private void OnEnable()
        {
            StartCoroutine(Initialize());
        }

        public IEnumerator Initialize()
        {
            willInitialize = false;
            if (!SystemInfo.supportsComputeShaders)
            {
                Debug.LogWarning("Compute shaders are not supported on this system. Stylized Kelp will not be rendered.", gameObject);
                enabled = false;
                yield break;
            }
            if (kelpStemVisualEffect == null || kelpLeafVisualEffect == null)
            {
                Debug.LogError("Kelp Visual Effect or Kelp Leaf Visual Effect is not assigned!", this);
                yield break;
            }
            if (verletKelpComputeShader == null)
            {
                Debug.LogError("Verlet Kelp Compute Shader is not assigned!", this);
                yield break;
            }

            // wait a frame in edit mode to allow the editor to startup fully
            if (Application.isEditor && !Application.isPlaying && Time.frameCount <= 1)
            {
                yield return null;
            }



            if (_kelpSettings.Count == 0 || _kelpStemSegments.Count == 0) yield break; // do not initialize if there are no kelp are registered

            if (isInitialized) yield break;
            isInitialized = true;

            _otherColliders = new Collider[5];
            var mainCamera = Camera.main;
            if (playerTransform == null && Application.isPlaying && mainCamera != null)
            {
                playerTransform = mainCamera.transform;
            }

            rendererTransform = transform;

            ApplyVisualEffectSettings();

            _kelpSegmentBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _kelpStemSegments.Count, KelpVerletSegment.Stride());
            _kelpSegmentBuffer.SetData(_kelpStemSegments);
            kelpStemVisualEffect.SetGraphicsBuffer("Kelp Stem Segments", _kelpSegmentBuffer);

            _kelpLeafBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _kelpLeafSegments.Count, KelpVerletSegment.Stride());
            _kelpLeafBuffer.SetData(_kelpLeafSegments);
            kelpLeafVisualEffect.SetGraphicsBuffer("Kelp Leaf Segments", _kelpLeafBuffer);

            OnKelpSettingsChange();
            _kelpSettingsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _kelpSettingsGpuDatas.Count, KelpSettingsGpuData.Stride());
            _kelpSettingsBuffer.SetData(_kelpSettingsGpuDatas);
            kelpStemVisualEffect.SetGraphicsBuffer("Kelp Settings", _kelpSettingsBuffer);
            kelpLeafVisualEffect.SetGraphicsBuffer("Kelp Settings", _kelpSettingsBuffer);

            kelpStemVisualEffect.Play();
            kelpLeafVisualEffect.Play();
        }

        private void OnDisable()
        {
            Deinitialize();
        }

        void Deinitialize()
        {
            DisposeBufferAndStoreData(ref _kelpSegmentBuffer, ref _kelpStemSegments);
            DisposeBufferAndStoreData(ref _kelpLeafBuffer, ref _kelpLeafSegments);
            DisposeBufferAndStoreData(ref _kelpSettingsBuffer, ref _kelpSettingsGpuDatas);

            _kelpSettings.ForEach(setting => setting.OnChange -= OnKelpSettingsChange);
            isInitialized = false;
            willInitialize = true;

            if (kelpStemVisualEffect != null) kelpStemVisualEffect.Stop();
            if (kelpLeafVisualEffect != null) kelpLeafVisualEffect.Stop();
        }
        public void Reinitialize()
        {
            Deinitialize();

            _kelpLeafSegments.Clear();
            _kelpStemSegments.Clear();
            activeKelp = 0;

            FindObjectsByType<Kelp>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).ToList().ForEach(kelp =>
            {
                if (!kelp.enabled) return;
                if (kelp.KelpRenderer != this) return;
                kelp.ReinitializeKelp();
            });

            if (kelpStemVisualEffect != null) kelpStemVisualEffect.Reinit();
            if (kelpLeafVisualEffect != null) kelpLeafVisualEffect.Reinit();

            if (isActiveAndEnabled)
                StartCoroutine(Initialize());
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;
            if (_kelpStemSegments == null) return;

            foreach (var kelpStemSegment in _kelpStemSegments)
            {
                Gizmos.color = Color.green;
                if (IsCulled(kelpStemSegment)) Gizmos.color = Color.gray;
                if (kelpStemSegment.visible == 0) Gizmos.color = Color.red;

                Gizmos.DrawSphere(transform.TransformPoint(kelpStemSegment.position), 0.2f);
            }
        }

        void OnKelpSettingsChange()
        {
            if (_kelpSettingsGpuDatas == null) return;

            _kelpSettingsGpuDatas.Clear();
            foreach (var setting in _kelpSettings)
            {
                _kelpSettingsGpuDatas.Add(setting.GetGpuRepresentation());
            }

            if (_kelpSettingsBuffer == null) return;
            _kelpSettingsBuffer?.SetData(_kelpSettingsGpuDatas);
        }

        private void FixedUpdate()
        {
            if (updateMode == EKelpUpdateMode.FixedUpdate) Simulate();
        }
        private void Update()
        {
            if (updateMode == EKelpUpdateMode.Update ||
                (updateMode == EKelpUpdateMode.FixedUpdate && Application.isEditor && !Application.isPlaying))
                Simulate();
        }

        public void Simulate()
        {
            if (!isInitialized) return;

            if (_skipUpdateCounter < skipUpdates)
            {
                _skipUpdateCounter++;
                return;
            }
            _skipUpdateCounter = 0;

            verletKelpComputeShader.SetFloat(_fixedDeltaTimeId, Time.fixedDeltaTime);
            verletKelpComputeShader.SetFloat(_dampingFactorId, dampingFactor);
            verletKelpComputeShader.SetVector(_windScaleId, windScale);
            verletKelpComputeShader.SetFloat(_windStrengthId, windStrength);
            verletKelpComputeShader.SetFloat(_windSpeedId, windSpeed);
            verletKelpComputeShader.SetFloat(_simulationDistanceId, simulationDistance);

            if (playerTransform != null) _playerPosition = playerTransform.position;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                var sceneView = UnityEditor.SceneView.lastActiveSceneView;
                if (sceneView == null) return;

                var sceneCam = sceneView.camera;
                if (sceneCam != null)
                {
                    _playerPosition = sceneCam.transform.position;
                }
            }
#endif

            verletKelpComputeShader.SetVector(_playerPositionId, rendererTransform.InverseTransformPoint(_playerPosition));

            SimulateStems();
            SimulateLeaves();
        }
        void SimulateStems()
        {
            _simulationMarker.Begin();
            var simulateInd = verletKelpComputeShader.FindKernel("VerletSimulate");
            verletKelpComputeShader.SetBuffer(simulateInd, _segmentShaderId, _kelpSegmentBuffer);
            verletKelpComputeShader.SetBuffer(simulateInd, _stemSegmentsId, _kelpSegmentBuffer);
            verletKelpComputeShader.SetBuffer(simulateInd, _settingsShaderId, _kelpSettingsBuffer);
            verletKelpComputeShader.SetBool(_isLeafShaderId, false);

            verletKelpComputeShader.Dispatch(simulateInd, Mathf.CeilToInt(_kelpStemSegments.Count / 64f), 1, 1);
            var result = new KelpVerletSegment[_kelpStemSegments.Count];
            _kelpSegmentBuffer.GetData(result);
            _kelpStemSegments = new List<KelpVerletSegment>(result);
            _simulationMarker.End();

            _constraintMarker.Begin();
            Constrain(_kelpStemSegments, stemConstraintIterations, segmentCollisionRadius);
            _constraintMarker.End();

            _kelpSegmentBuffer.SetData(_kelpStemSegments);
        }
        void SimulateLeaves()
        {
            verletKelpComputeShader.SetBool(_isLeafShaderId, true);
            verletKelpComputeShader.SetInt(_constraintIterationsId, leafConstraintIterations);

            _leafSimulationMarker.Begin();
            var simulateInd = verletKelpComputeShader.FindKernel("VerletSimulate");
            verletKelpComputeShader.SetBuffer(simulateInd, _segmentShaderId, _kelpLeafBuffer);
            verletKelpComputeShader.SetBuffer(simulateInd, _stemSegmentsId, _kelpSegmentBuffer);
            verletKelpComputeShader.SetBuffer(simulateInd, _settingsShaderId, _kelpSettingsBuffer);
            verletKelpComputeShader.Dispatch(simulateInd, Mathf.CeilToInt(_kelpLeafSegments.Count / 64f), 1, 1);
            _leafSimulationMarker.End();

            var attachLeafToStemInd = verletKelpComputeShader.FindKernel("AttachLeafToStem");
            verletKelpComputeShader.SetBuffer(attachLeafToStemInd, _stemSegmentsId, _kelpSegmentBuffer);
            verletKelpComputeShader.SetBuffer(attachLeafToStemInd, _segmentShaderId, _kelpLeafBuffer);
            verletKelpComputeShader.Dispatch(attachLeafToStemInd, Mathf.CeilToInt(_kelpLeafSegments.Count / 64f), 1, 1);

            _leafConstraintMarker.Begin();
            var constrainInd = verletKelpComputeShader.FindKernel("VerletConstrain");
            verletKelpComputeShader.SetBuffer(constrainInd, _segmentShaderId, _kelpLeafBuffer);
            verletKelpComputeShader.SetBuffer(constrainInd, _stemSegmentsId, _kelpSegmentBuffer);
            verletKelpComputeShader.SetBuffer(constrainInd, _settingsShaderId, _kelpSettingsBuffer);
            var kelpLeafCount = _kelpLeafSegments.Count / 3;
            verletKelpComputeShader.Dispatch(constrainInd, Mathf.CeilToInt(kelpLeafCount / 64f), 1, 1);
            _leafConstraintMarker.End();

            _leafPointsMarker.Begin();
            var calcLeafPointsInd = verletKelpComputeShader.FindKernel("ComputeLeafPoints");
            verletKelpComputeShader.SetBuffer(calcLeafPointsInd, _segmentShaderId, _kelpLeafBuffer);
            verletKelpComputeShader.SetBuffer(calcLeafPointsInd, _stemSegmentsId, _kelpSegmentBuffer);
            verletKelpComputeShader.SetBuffer(calcLeafPointsInd, _settingsShaderId, _kelpSettingsBuffer);
            verletKelpComputeShader.Dispatch(calcLeafPointsInd, Mathf.CeilToInt(_kelpLeafSegments.Count / 64f), 1, 1);
            _leafPointsMarker.End();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Constrain(List<KelpVerletSegment> segments, int constraintIterations, float _segmentCollisionRadius)
        {
            for (int count = 0; count < constraintIterations; count++)
            {
                // constraint pass
                for (int i = 0; i < segments.Count; i++)
                {
                    var currentSeg = segments[i];
                    if (currentSeg.isEndSegment > 0) continue;
                    if (IsCulled(currentSeg)) continue;

                    var kelpSettings = _kelpSettings[currentSeg.kelpSettingsIndex];
                    var nextSeg = segments[i + 1];

                    var dist = (currentSeg.position - nextSeg.position).magnitude;
                    var segmentLength = kelpSettings.StemSegmentLength;
                    var diff = dist - segmentLength;

                    var changeDir = (currentSeg.position - nextSeg.position).normalized;
                    var change = changeDir * diff;

                    if (currentSeg.isFixed > 0)
                    {
                        nextSeg.position += change;
                    }
                    else
                    {
                        currentSeg.position -= change * 0.5f;
                        nextSeg.position += change * 0.5f;
                    }

                    segments[i] = currentSeg;
                    segments[i + 1] = nextSeg;
                }

                // collision pass
                if (!enableCollision) continue;
                if (count % 5 != 0) continue;
                for (int i = 0; i < segments.Count; i++)
                {
                    var segment = segments[i];
                    if (segment.isFixed > 0) continue;
                    if (IsCulled(segment)) continue;

                    var velocity = segment.position - segment.previousPosition;

                    var segmentPositionWorld = transform.TransformPoint(segment.position);
                    var colliderCount = Physics.OverlapSphereNonAlloc(segmentPositionWorld, _segmentCollisionRadius, _otherColliders, collisionMask);
                    for (int ci = 0; ci < colliderCount; ci++)
                    {
                        var coll = _otherColliders[ci];
                        var closestPoint = coll.ClosestPoint(segmentPositionWorld);
                        var isInsideCollider = segmentPositionWorld == closestPoint;
                        var distance = Vector3.Distance(segmentPositionWorld, closestPoint);

                        if (distance < _segmentCollisionRadius || isInsideCollider)
                        {
                            var normal = (segmentPositionWorld - closestPoint).normalized;

                            // if we're inside the collider
                            if (normal == Vector3.zero)
                            {
                                normal = (segmentPositionWorld - coll.transform.position).normalized;
                            }

                            var intrusion = _segmentCollisionRadius - distance;
                            segment.position += normal * intrusion;

                            velocity = Vector3.Reflect(velocity, normal);

                            segment.previousPosition = segment.position - velocity;
                            segments[i] = segment;
                            break;
                        }
                    }
                }
            }
        }

        public KelpInstance AddKelp(Vector3 position, int length, KelpSettings kelpSettings)
        {
            if (Application.isEditor && !Application.isPlaying)
            {
                if (!willInitialize || isInitialized)
                {
                    Reinitialize();
                    return null;
                }
            }

            if (isInitialized)
            {
                Debug.LogError("Kelp cannot be added, while the renderer is enabled. Please add all kelp before enabling the renderer (i.e. during Awake) or disable the renderer.", this);
                return null;
            }

            RegisterKelpSettings(kelpSettings);
            activeKelp++;

            if (length < 2) length = 2;

            var instance = new KelpInstance
            {
                RootStemSegmentIndex = _kelpStemSegments.Count,
                StemSegmentCount = length
            };

            var localPosition = transform.InverseTransformPoint(position);
            var rootSegmentIndex = _kelpStemSegments.Count;
            var kelpSettingsIndex = _kelpSettings.IndexOf(kelpSettings);
            for (int i = 0; i < length; i++)
            {
                var initialPos = localPosition + Vector3.up * (i * kelpSettings.StemSegmentLength);
                _kelpStemSegments.Add(new KelpVerletSegment()
                {
                    position = initialPos,
                    previousPosition = initialPos,
                    isFixed = i == 0 ? 1 : 0,
                    isEndSegment = i == length - 1 ? 1 : 0,
                    controllingIndex = rootSegmentIndex,
                    kelpSettingsIndex = kelpSettingsIndex,
                    visible = 1,
                });
            }

            instance.LeafSegmentStartIndex = _kelpLeafSegments.Count;

            var stemSegCount = _kelpStemSegments.Count;
            for (int i = 1; i < length; i++)
            {
                var index = stemSegCount - i;
                var leafPercentage = 1f / kelpSettings.LeavesPerStemSegment;
                for (int lc = 0; lc < kelpSettings.LeavesPerStemSegment; lc++)
                {
                    var verticalPos = leafPercentage * lc;
                    var angle = kelpSettings.RandomizeLeafRotation
                        ? Random.value * 360f
                        : verticalPos * kelpSettings.LeafWindings * 360f;
                    AddLeaf(index, angle, verticalPos, kelpSettings);

                    instance.LeafSegmentCount += 3;
                }
            }

            return instance;
        }
        public void MoveKelp(KelpInstance instance, Vector3 newPosition)
        {
            if (instance.RootStemSegmentIndex < 0 || instance.RootStemSegmentIndex >= _kelpStemSegments.Count) return;
            if (!isInitialized) return;

            var localNewPos = transform.InverseTransformPoint(newPosition);

            for (int i = 0; i < instance.StemSegmentCount; i++)
            {
                var segIndex = instance.RootStemSegmentIndex + i;
                if (segIndex >= _kelpStemSegments.Count) break;

                var segment = _kelpStemSegments[segIndex];
                if (i == 0)
                {
                    segment.position = localNewPos;
                    segment.previousPosition = localNewPos;
                }
                else
                {
                    var prevSegment = _kelpStemSegments[segIndex - 1];
                    var targetPos = prevSegment.position + Vector3.up * (_kelpSettings[segment.kelpSettingsIndex].StemSegmentLength);
                    segment.position = targetPos;
                    segment.previousPosition = targetPos;
                }
                _kelpStemSegments[segIndex] = segment;
            }

            _kelpSegmentBuffer.SetData(_kelpStemSegments);
        }
        private void AddLeaf(int controllingStemIndex, float angle, float t, KelpSettings kelpSettings)
        {
            int kelpSettingsIndex = _kelpSettings.IndexOf(kelpSettings);
            var position = GetStemSegmentVerticalPositionAt(controllingStemIndex, t);

            _kelpLeafSegments.Add(new KelpVerletSegment()
            {
                controllingIndex = controllingStemIndex,
                position = position,
                previousPosition = position,
                isFixed = 1,
                angle = angle,
                kelpSettingsIndex = kelpSettingsIndex,
                stemSegmentVerticalPos = t,
                visible = 1
            });

            var middlePos = position + Vector3.forward * (0.5f * kelpSettings.LeafLength);
            _kelpLeafSegments.Add(new KelpVerletSegment()
            {
                controllingIndex = controllingStemIndex,
                position = middlePos,
                previousPosition = middlePos,
                isFixed = 0,
                kelpSettingsIndex = kelpSettingsIndex,
                visible = 1
            });

            var tipPos = position + Vector3.forward * kelpSettings.LeafLength;
            _kelpLeafSegments.Add(new KelpVerletSegment()
            {
                controllingIndex = controllingStemIndex,
                position = tipPos,
                previousPosition = tipPos,
                isFixed = 0,
                isEndSegment = 1,
                kelpSettingsIndex = kelpSettingsIndex,
                visible = 1
            });
        }

        bool IsCulled(KelpVerletSegment segment)
        {
            KelpVerletSegment stemSegment = _kelpStemSegments[segment.controllingIndex];
            KelpVerletSegment rootStemSegment = _kelpStemSegments[stemSegment.controllingIndex];

            var p = transform.TransformPoint(rootStemSegment.position);
            float distToPlayer = Vector3.Distance(_playerPosition, p);
            return distToPlayer > simulationDistance;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        Vector3 GetStemSegmentVerticalPositionAt(int stemSegmentIndex, float t)
        {
            var stemSegment = _kelpStemSegments[stemSegmentIndex];

            if (stemSegment.isEndSegment == 0)
            {
                var nextStemSeg = _kelpStemSegments[stemSegmentIndex + 1];
                return Vector3.Lerp(stemSegment.position, nextStemSeg.position, t);
            }

            return stemSegment.position;
        }

        void RegisterKelpSettings(KelpSettings settings)
        {
            if (settings == null) return;
            if (_kelpSettings.Contains(settings)) return;

            _kelpSettings.Add(settings);
            _kelpSettingsGpuDatas.Add(settings.GetGpuRepresentation());
            _kelpSettingsBuffer?.SetData(_kelpSettingsGpuDatas);

            settings.OnChange += OnKelpSettingsChange;
        }

        void DisposeBufferAndStoreData<T>(ref GraphicsBuffer buffer, ref List<T> segments)
        {
            if (buffer != null)
            {
                var data = new T[segments.Count];
                buffer.GetData(data);
                segments = new List<T>(data);

                buffer.Dispose();
                buffer = null;
            }
        }

        void ApplyVisualEffectSettings()
        {
            kelpLeafVisualEffect?.SetTexture("Leaf Texture", leafTexture);
            kelpLeafVisualEffect?.SetFloat("Scattering Color Boost", scatteringColorBoost);
            kelpLeafVisualEffect?.SetFloat("Scattering Amount", scatteringAmount);

            kelpStemVisualEffect?.SetFloat("Scattering Color Boost", scatteringColorBoost);
            kelpStemVisualEffect?.SetFloat("Scattering Amount", scatteringAmount);
        }
    }

    [Serializable]
    [VFXType(VFXTypeAttribute.Usage.GraphicsBuffer)]
    public struct KelpVerletSegment
    {
        public Vector3 position;
        public Vector3 previousPosition;
        public int isFixed;
        public int isEndSegment;

        public Vector3 leftPosition;
        public Vector3 rightPosition;
        public float stemSegmentVerticalPos;

        public int controllingIndex;
        public float angle;

        public int kelpSettingsIndex;

        public int visible;

        public static int Stride()
        {
            return sizeof(float) * 3
                   + sizeof(float) * 3
                   + sizeof(float) * 3
                   + sizeof(float) * 3
                   + sizeof(float)
                   + sizeof(int)
                   + sizeof(int)
                   + sizeof(int)
                   + sizeof(int)
                   + sizeof(int)
                   + sizeof(float);
        }
    }

    [Serializable]
    public class KelpInstance
    {
        public int RootStemSegmentIndex;
        public int StemSegmentCount;

        public int LeafSegmentStartIndex;
        public int LeafSegmentCount;
    }

    [Serializable]
    public enum EKelpUpdateMode
    {
        FixedUpdate,
        Update,
        Manual
    }

}