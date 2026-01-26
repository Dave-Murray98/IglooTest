// ╔════════════════════════════════════════════════════════════════╗
// ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// ║    Licensed under Unity Asset Store Terms of Service:          ║
// ║        https://unity.com/legal/as-terms                        ║
// ║    Use permitted only in compliance with the License.          ║
// ║    Distributed "AS IS", without warranty of any kind.          ║
// ╚════════════════════════════════════════════════════════════════╝

#region

using NWH.Common.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using NWH.NUI;
using UnityEditor;
#endif

#endregion

namespace NWH.Common.Cameras
{
    /// <summary>
    /// Camera that can be dragged with the mouse. Supports Forza-style dynamics.
    /// </summary>
    public class CameraMouseDrag : VehicleCamera
    {
        public enum POVType
        {
            FirstPerson,
            ThirdPerson,
        }

        // ═══════════════════════════════════════════════════════════════
        // POV & Basic Settings
        // ═══════════════════════════════════════════════════════════════

        [Tooltip("Camera POV type. First person camera will invert controls.\r\nZoom is not available in 1st person.")]
        public POVType povType = POVType.ThirdPerson;

        [Tooltip("Can the camera be rotated by the user?")]
        public bool allowRotation = true;

        [Tooltip("Can the camera be panned by the user?")]
        public bool allowPanning = true;

        // ═══════════════════════════════════════════════════════════════
        // Distance & Position
        // ═══════════════════════════════════════════════════════════════

        [Range(0, 100f)]
        [Tooltip("Base distance from target at which camera will be positioned.")]
        public float distance = 6f;

        [Range(0, 100f)]
        [Tooltip("Minimum distance that will be reached when zooming in.")]
        public float minDistance = 3.0f;

        [Range(0, 100f)]
        [Tooltip("Maximum distance that will be reached when zooming out.")]
        public float maxDistance = 13.0f;

        [Range(0, 15)]
        [Tooltip("Sensitivity of the middle mouse button / wheel.")]
        public float zoomSensitivity = 1f;

        [Tooltip("Look position offset from the target center.")]
        public Vector3 targetPositionOffset = Vector3.zero;

        // ═══════════════════════════════════════════════════════════════
        // Rotation
        // ═══════════════════════════════════════════════════════════════

        [FormerlySerializedAs("followTargetsRotation")]
        [Tooltip("If true the camera will rotate with the vehicle along the X and Y axis.")]
        public bool followTargetPitchAndYaw = true;

        [Tooltip("If true the camera will rotate with the vehicle along the Z axis.")]
        public bool followTargetRoll;

        [Tooltip("Sensitivity of rotation input.")]
        public Vector2 rotationSensitivity = new(3f, 3f);

        [Range(-90, 90)]
        [Tooltip("Maximum vertical angle the camera can achieve.")]
        public float verticalMaxAngle = 80.0f;

        [Range(-90, 90)]
        [Tooltip("Minimum vertical angle the camera can achieve.")]
        public float verticalMinAngle = -20.0f;

        [Tooltip("Initial rotation around the X axis (up/down)")]
        public float initXRotation = 10f;

        [Tooltip("Initial rotation around the Y axis (left/right)")]
        public float initYRotation;

        [Range(0, 1)]
        [Tooltip("Smoothing of the camera rotation.")]
        public float rotationSmoothing = 0.08f;

        // ═══════════════════════════════════════════════════════════════
        // Panning
        // ═══════════════════════════════════════════════════════════════

        [Tooltip("Sensitivity of panning input.")]
        public Vector2 panningSensitivity = new(0.06f, 0.06f);

        // ═══════════════════════════════════════════════════════════════
        // Auto-Centering (Third-Person Only)
        // ═══════════════════════════════════════════════════════════════

        [Tooltip("Camera gradually returns behind vehicle after manual rotation stops.")]
        public bool useAutoCenter = true;

        [Range(0f, 5f)]
        [Tooltip("Seconds of no rotation input before auto-centering starts.")]
        public float autoCenterDelay = 1.5f;

        [Range(0.5f, 10f)]
        [Tooltip("Speed at which camera returns to center. Higher = faster.")]
        public float autoCenterSpeed = 2f;

        [Range(0f, 10f)]
        [Tooltip("Minimum vehicle speed (m/s) required for auto-centering. Prevents centering when stationary.")]
        public float autoCenterMinSpeed = 2f;

        // ═══════════════════════════════════════════════════════════════
        // Speed-Based FOV (Third-Person Only)
        // ═══════════════════════════════════════════════════════════════

        [Tooltip("Dynamically adjust FOV based on vehicle speed.")]
        public bool useSpeedFOV = true;

        [Range(30f, 90f)]
        [Tooltip("Field of view when stationary.")]
        public float baseFOV = 60f;

        [Range(30f, 120f)]
        [Tooltip("Maximum field of view at high speed.")]
        public float maxFOV = 75f;

        [Range(10f, 200f)]
        [Tooltip("Speed (m/s) at which maximum FOV is reached.")]
        public float fovSpeedRange = 50f;

        [Range(0f, 1f)]
        [Tooltip("Smoothing applied to FOV transitions.")]
        public float fovSmoothing = 0.3f;

        // ═══════════════════════════════════════════════════════════════
        // Speed-Based Distance (Third-Person Only)
        // ═══════════════════════════════════════════════════════════════

        [Tooltip("Camera pulls back at higher speeds.")]
        public bool useSpeedDistance = true;

        [Range(0f, 0.2f)]
        [Tooltip("Extra distance added per m/s of speed.")]
        public float speedDistanceMultiplier = 0.05f;

        [Range(0f, 10f)]
        [Tooltip("Maximum additional distance from speed.")]
        public float maxSpeedDistance = 3f;

        // ═══════════════════════════════════════════════════════════════
        // Speed-Based Height (Third-Person Only)
        // ═══════════════════════════════════════════════════════════════

        [Tooltip("Camera rises at higher speeds.")]
        public bool useSpeedHeight = true;

        [Range(0f, 0.1f)]
        [Tooltip("Extra height added per m/s of speed.")]
        public float speedHeightMultiplier = 0.02f;

        [Range(0f, 5f)]
        [Tooltip("Maximum additional height from speed.")]
        public float maxSpeedHeight = 1f;

        // ═══════════════════════════════════════════════════════════════
        // Look-Ahead (Third-Person Only)
        // ═══════════════════════════════════════════════════════════════

        [Tooltip("Camera anticipates turns by rotating into the direction of steering.")]
        public bool useLookAhead = true;

        [Range(0f, 30f)]
        [Tooltip("Maximum degrees of yaw offset when turning.")]
        public float lookAheadIntensity = 15f;

        [Range(0f, 1f)]
        [Tooltip("Smoothing applied to look-ahead. Higher = slower response.")]
        public float lookAheadSmoothing = 0.2f;

        // ═══════════════════════════════════════════════════════════════
        // Camera Shake
        // ═══════════════════════════════════════════════════════════════

        [Tooltip("Should camera movement on acceleration be used?")]
        public bool useShake = true;

        [Range(0f, 1f)]
        [Tooltip("Maximum head movement from the initial position.")]
        public float shakeMaxOffset = 0.2f;

        [Range(0f, 1f)]
        [Tooltip("How much will the head move around for the given g-force.")]
        public float shakeIntensity = 0.125f;

        [Range(0f, 1f)]
        [Tooltip("Smoothing of the head movement.")]
        public float shakeSmoothing = 0.3f;

        [Tooltip("Movement intensity per axis. Set to 0 to disable movement on that axis or negative to reverse it.")]
        public Vector3 shakeAxisIntensity = new(1f, 0.5f, 1f);

        // ═══════════════════════════════════════════════════════════════
        // Private Fields
        // ═══════════════════════════════════════════════════════════════

        // Core
        private Vector3 _initialPosition;
        private bool    _isFirstFrame;
        private Rigidbody _rigidbody;
        private Camera _camera;

        // Rotation
        private Vector2   _rot;
        private Vector3   _lookDir;
        private Vector3   _lookDirVel;
        private Vector3   _newLookDir;
        private Vector3   _lookAtPosition;
        private Vector3   _pan;

        // Velocity tracking
        private Vector3   _rbLocalVelocity;
        private Vector3   _rbPrevLocalVelocity;
        private Vector3   _rbLocalAcceleration;
        private float     _rbSpeed;

        // Shake
        private Vector3 _acceleration;
        private Vector3 _accelerationChangeVelocity;
        private Vector3 _localAcceleration;
        private Vector3   _newPositionOffset;
        private Vector3   _offsetChangeVelocity;
        private Vector3   _positionOffset;
        private Vector3   _prevAcceleration;

        // Auto-center
        private float _timeSinceLastRotationInput;

        // Speed dynamics
        private float _currentFOV;
        private float _fovVelocity;
        private float _speedDistanceOffset;
        private float _speedDistanceVelocity;
        private float _speedHeightOffset;
        private float _speedHeightVelocity;

        // Look-ahead
        private float _lookAheadOffset;
        private float _lookAheadVelocity;

        private bool PointerOverUI
        {
            get
            {
                return EventSystem.current != null &&
                       EventSystem.current.IsPointerOverGameObject();
            }
        }


        private void Start()
        {
            _initialPosition = transform.localPosition;
            _rigidbody       = target?.GetComponent<Rigidbody>();
            _camera          = GetComponent<Camera>();

            distance = Mathf.Clamp(distance, minDistance, maxDistance);

            _rot.x        = initXRotation;
            _rot.y        = initYRotation;
            _isFirstFrame = true;

            // Initialize FOV
            _currentFOV = baseFOV;
            if (_camera != null)
            {
                _camera.fieldOfView = baseFOV;
            }
        }


        private void FixedUpdate()
        {
            if (_rigidbody == null)
            {
                return;
            }

            _rbPrevLocalVelocity = _rbLocalVelocity;
            _rbLocalVelocity     = transform.InverseTransformDirection(_rigidbody.linearVelocity);
            if (Time.fixedDeltaTime > 0f)
            {
                _rbLocalAcceleration = (_rbLocalVelocity - _rbPrevLocalVelocity) / Time.fixedDeltaTime;
            }
            _rbSpeed             = _rbLocalVelocity.z < 0 ? -_rbLocalVelocity.z : _rbLocalVelocity.z;
        }


        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            bool isThirdPerson = povType == POVType.ThirdPerson;
            bool hadRotationInput = false;

            // ═══════════════════════════════════════════════════════════════
            // Input Phase
            // ═══════════════════════════════════════════════════════════════
            bool pointerOverUI = PointerOverUI;
            if (!pointerOverUI)
            {
                Vector2 rotationInput = InputProvider.CombinedInput<SceneInputProviderBase>(i => i.CameraRotation());
                Vector2 panningInput  = InputProvider.CombinedInput<SceneInputProviderBase>(i => i.CameraPanning());
                float zoomInput     = InputProvider.CombinedInput<SceneInputProviderBase>(i => i.CameraZoom());
                bool rotationModifier =
                    InputProvider.CombinedInput<SceneInputProviderBase>(i => i.CameraRotationModifier());
                bool panningModifier = InputProvider.CombinedInput<SceneInputProviderBase>(i => i.CameraPanningModifier());

                if (allowRotation && rotationModifier)
                {
                    float rotMagnitude = rotationInput.sqrMagnitude;
                    if (rotMagnitude > 0.001f)
                    {
                        _rot.y += rotationInput.x * rotationSensitivity.x;
                        _rot.x -= rotationInput.y * rotationSensitivity.y;
                        hadRotationInput = true;
                        _timeSinceLastRotationInput = 0f;
                    }
                }

                if (allowPanning && panningModifier)
                {
                    float pX = panningInput.x * panningSensitivity.x;
                    float pY = panningInput.y * panningSensitivity.y;
                    _pan -= target.InverseTransformDirection(transform.right * pX);
                    _pan -= target.InverseTransformDirection(transform.up * pY);
                }

                _rot.x = ClampAngle(_rot.x, verticalMinAngle, verticalMaxAngle);

                if (isThirdPerson && (zoomInput > 0.0001f || zoomInput < -0.0001f))
                {
                    distance -= zoomInput * zoomSensitivity;
                }
            }

            // ═══════════════════════════════════════════════════════════════
            // Auto-Center Phase (Third-Person Only)
            // ═══════════════════════════════════════════════════════════════
            if (isThirdPerson && useAutoCenter && !hadRotationInput)
            {
                _timeSinceLastRotationInput += Time.deltaTime;

                if (_timeSinceLastRotationInput > autoCenterDelay && _rbSpeed > autoCenterMinSpeed)
                {
                    float centerLerp = autoCenterSpeed * Time.deltaTime;
                    _rot.x = Mathf.Lerp(_rot.x, initXRotation, centerLerp);
                    _rot.y = Mathf.Lerp(_rot.y, initYRotation, centerLerp);
                }
            }

            // ═══════════════════════════════════════════════════════════════
            // Look-Ahead Phase (Third-Person Only)
            // ═══════════════════════════════════════════════════════════════
            float effectiveLookAheadOffset = 0f;
            if (isThirdPerson && useLookAhead && _rigidbody != null)
            {
                // Use angular velocity for turn anticipation
                float angularVelY = _rigidbody.angularVelocity.y;
                // Also factor in lateral velocity for drifts
                float lateralVel = Vector3.Dot(_rigidbody.linearVelocity, target.right);
                float turnFactor = angularVelY * 2f + lateralVel * 0.1f;

                float targetLookAhead = Mathf.Clamp(turnFactor * lookAheadIntensity, -lookAheadIntensity, lookAheadIntensity);
                _lookAheadOffset = Mathf.SmoothDamp(_lookAheadOffset, targetLookAhead, ref _lookAheadVelocity, lookAheadSmoothing);
                effectiveLookAheadOffset = _lookAheadOffset;
            }

            // ═══════════════════════════════════════════════════════════════
            // Speed Dynamics Phase (Third-Person Only)
            // ═══════════════════════════════════════════════════════════════
            float effectiveDistance = distance;
            Vector3 effectiveTargetOffset = targetPositionOffset;

            if (isThirdPerson)
            {
                // Speed-based FOV
                if (useSpeedFOV && _camera != null)
                {
                    float speedNormalized = Mathf.Clamp01(_rbSpeed / fovSpeedRange);
                    float targetFOV = Mathf.Lerp(baseFOV, maxFOV, speedNormalized);
                    _currentFOV = Mathf.SmoothDamp(_currentFOV, targetFOV, ref _fovVelocity, fovSmoothing);
                    _camera.fieldOfView = _currentFOV;
                }

                // Speed-based distance
                if (useSpeedDistance)
                {
                    float targetSpeedDistance = Mathf.Min(_rbSpeed * speedDistanceMultiplier, maxSpeedDistance);
                    _speedDistanceOffset = Mathf.SmoothDamp(_speedDistanceOffset, targetSpeedDistance, ref _speedDistanceVelocity, 0.2f);
                    effectiveDistance = distance + _speedDistanceOffset;
                }

                // Speed-based height
                if (useSpeedHeight)
                {
                    float targetSpeedHeight = Mathf.Min(_rbSpeed * speedHeightMultiplier, maxSpeedHeight);
                    _speedHeightOffset = Mathf.SmoothDamp(_speedHeightOffset, targetSpeedHeight, ref _speedHeightVelocity, 0.2f);
                    effectiveTargetOffset.y += _speedHeightOffset;
                }
            }

            // ═══════════════════════════════════════════════════════════════
            // Position/Rotation Calculation
            // ═══════════════════════════════════════════════════════════════
            Vector3 forwardVector = followTargetPitchAndYaw ? target.forward : Vector3.forward;
            Vector3 rightVector   = followTargetPitchAndYaw ? target.right : Vector3.right;
            Vector3 upVector      = followTargetPitchAndYaw ? target.up : Vector3.up;

            _lookAtPosition = target.position +
                              target.TransformDirection(effectiveTargetOffset + _pan);

            // Apply look-ahead to yaw
            float effectiveYaw = _rot.y + effectiveLookAheadOffset;

            _newLookDir = Quaternion.AngleAxis(_rot.x, rightVector) * forwardVector;
            _newLookDir = Quaternion.AngleAxis(effectiveYaw, upVector) * _newLookDir;

            _lookDir = _isFirstFrame
                           ? _newLookDir
                           : Vector3.SmoothDamp(_lookDir, _newLookDir, ref _lookDirVel, rotationSmoothing);
            _lookDir = Vector3.Normalize(_lookDir);

            if (isThirdPerson)
            {
                effectiveDistance = Mathf.Clamp(effectiveDistance, minDistance, maxDistance + maxSpeedDistance);

                Vector3 targetPosition = _lookAtPosition - _lookDir * effectiveDistance;
                transform.position = targetPosition;
                transform.forward  = _lookDir;

                // Check for ground
                if (Physics.Raycast(transform.position, -Vector3.up, out RaycastHit hit, 0.5f))
                {
                    transform.position = hit.point + Vector3.up * 0.5f;
                }

                transform.rotation =
                    Quaternion.LookRotation(_lookDir, followTargetRoll ? target.up : Vector3.up);
            }
            else
            {
                transform.localPosition = _initialPosition + _pan;
                transform.rotation =
                    Quaternion.LookRotation(_lookDir, followTargetRoll ? target.up : Vector3.up);
            }

            // ═══════════════════════════════════════════════════════════════
            // Camera Shake Phase
            // ═══════════════════════════════════════════════════════════════
            if (useShake)
            {
                _prevAcceleration  = _acceleration;
                _acceleration      = _rbLocalAcceleration;
                _localAcceleration = Vector3.zero;
                if (target != null)
                {
                    _localAcceleration = target.TransformDirection(_acceleration);
                }

                if (!_isFirstFrame)
                {
                    _newPositionOffset = Vector3.SmoothDamp(_prevAcceleration, _localAcceleration,
                                                            ref _accelerationChangeVelocity,
                                                            shakeSmoothing) / 100f * shakeIntensity;
                    _newPositionOffset = Vector3.Scale(_newPositionOffset, shakeAxisIntensity);
                    _positionOffset = Vector3.SmoothDamp(_positionOffset, _newPositionOffset, ref _offsetChangeVelocity,
                                                         shakeSmoothing);
                    _positionOffset = Vector3.ClampMagnitude(_positionOffset, shakeMaxOffset);
                    transform.position -= target.TransformDirection(_positionOffset) *
                                          Mathf.Clamp01(_rbSpeed * 0.5f);
                }
            }

            _isFirstFrame = false;
        }


        public void OnDrawGizmosSelected()
        {
            Gizmos.DrawWireSphere(_lookAtPosition, 0.1f);
            Gizmos.DrawRay(_lookAtPosition, _lookDir);
        }


        private void OnEnable()
        {
            _isFirstFrame = true;
        }


        public float ClampAngle(float angle, float min, float max)
        {
            angle = Mathf.Repeat(angle + 180f, 360f) - 180f;
            return Mathf.Clamp(angle, min, max);
        }
    }
}

#if UNITY_EDITOR

namespace NWH.Common.Cameras
{
    [CustomEditor(typeof(CameraMouseDrag))]
    [CanEditMultipleObjects]
    public class CameraMouseDragEditor : NUIEditor
    {
        public override bool OnInspectorNUI()
        {
            if (!base.OnInspectorNUI())
            {
                return false;
            }

            CameraMouseDrag cam = (CameraMouseDrag)target;
            bool isThirdPerson = cam.povType == CameraMouseDrag.POVType.ThirdPerson;

            drawer.Field("target");

            drawer.BeginSubsection("POV");
            drawer.Field("povType");
            drawer.EndSubsection();

            if (isThirdPerson)
            {
                drawer.BeginSubsection("Distance & Position");
                drawer.Field("distance");
                drawer.Field("minDistance");
                drawer.Field("maxDistance");
                drawer.Field("zoomSensitivity");
                drawer.Field("targetPositionOffset");
                drawer.EndSubsection();
            }

            drawer.BeginSubsection("Rotation");
            drawer.Field("allowRotation");
            drawer.Field("followTargetPitchAndYaw");
            drawer.Field("followTargetRoll");
            drawer.Field("rotationSensitivity");
            drawer.Field("verticalMaxAngle");
            drawer.Field("verticalMinAngle");
            drawer.Field("initXRotation");
            drawer.Field("initYRotation");
            drawer.Field("rotationSmoothing");
            drawer.EndSubsection();

            drawer.BeginSubsection("Panning");
            if (drawer.Field("allowPanning").boolValue)
            {
                drawer.Field("panningSensitivity");
            }
            drawer.EndSubsection();

            // Third-person only features
            if (isThirdPerson)
            {
                drawer.BeginSubsection("Auto-Centering");
                drawer.Info("Camera returns behind vehicle after manual rotation stops.");
                if (drawer.Field("useAutoCenter").boolValue)
                {
                    drawer.Field("autoCenterDelay");
                    drawer.Field("autoCenterSpeed");
                    drawer.Field("autoCenterMinSpeed");
                }
                drawer.EndSubsection();

                drawer.BeginSubsection("Speed-Based FOV");
                drawer.Info("FOV increases at high speeds for sense of velocity.");
                if (drawer.Field("useSpeedFOV").boolValue)
                {
                    drawer.Field("baseFOV");
                    drawer.Field("maxFOV");
                    drawer.Field("fovSpeedRange");
                    drawer.Field("fovSmoothing");
                }
                drawer.EndSubsection();

                drawer.BeginSubsection("Speed-Based Distance");
                drawer.Info("Camera pulls back at higher speeds.");
                if (drawer.Field("useSpeedDistance").boolValue)
                {
                    drawer.Field("speedDistanceMultiplier");
                    drawer.Field("maxSpeedDistance");
                }
                drawer.EndSubsection();

                drawer.BeginSubsection("Speed-Based Height");
                drawer.Info("Camera rises at higher speeds.");
                if (drawer.Field("useSpeedHeight").boolValue)
                {
                    drawer.Field("speedHeightMultiplier");
                    drawer.Field("maxSpeedHeight");
                }
                drawer.EndSubsection();

                drawer.BeginSubsection("Look-Ahead");
                drawer.Info("Camera anticipates turns by looking into corners.");
                if (drawer.Field("useLookAhead").boolValue)
                {
                    drawer.Field("lookAheadIntensity");
                    drawer.Field("lookAheadSmoothing");
                }
                drawer.EndSubsection();
            }

            drawer.BeginSubsection("Camera Shake");
            drawer.Info("Movement introduced as a result of acceleration.");
            if (drawer.Field("useShake").boolValue)
            {
                drawer.Field("shakeMaxOffset");
                drawer.Field("shakeIntensity");
                drawer.Field("shakeSmoothing");
                drawer.Field("shakeAxisIntensity");
            }
            drawer.EndSubsection();

            drawer.EndEditor(this);
            return true;
        }


        public override bool UseDefaultMargins()
        {
            return false;
        }
    }
}
#endif