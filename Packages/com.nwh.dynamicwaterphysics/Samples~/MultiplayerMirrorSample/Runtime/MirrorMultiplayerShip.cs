// ╔════════════════════════════════════════════════════════════════╗
// ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// ║    Licensed under Unity Asset Store Terms of Service:          ║
// ║        https://unity.com/legal/as-terms                        ║
// ║    Use permitted only in compliance with the License.          ║
// ║    Distributed "AS IS", without warranty of any kind.          ║
// ╚════════════════════════════════════════════════════════════════╝

#region

using Mirror;
using NWH.DWP2.ShipController;
using NWH.DWP2.WaterObjects;
using UnityEngine;

#endregion

namespace NWH.DWP2.Multiplayer.DWP2
{
    /// <summary>
    /// Simple Mirror adapter for AdvancedShipController.
    /// Synchronizes ship inputs only.
    /// </summary>
    /// <remarks>
    /// IMPORTANT: This script does NOT synchronize physics.
    /// Add ONE of these components separately:
    /// - Mirror.NetworkTransformReliable (position/rotation)
    /// - Mirror.NetworkRigidbodyReliable (full physics)
    ///
    /// Choose based on your authority model:
    /// - Server authority: Server controls physics, clients send inputs
    /// - Client authority: Owner controls physics, synced via NetworkRigidbody
    /// </remarks>
    [RequireComponent(typeof(NetworkIdentity))]
    [RequireComponent(typeof(AdvancedShipController))]
    public class MirrorMultiplayerShip : NetworkBehaviour
    {
        /// <summary>
        /// Simulate water physics on remote ships? Costs CPU but looks better.
        /// </summary>
        [Tooltip("Enable water simulation on remote ships? Better visuals but higher CPU cost.")]
        public bool simulateWaterObjectsOnClient = false;

        private const int INPUT_COUNT = 10;
        private const float INPUT_THRESHOLD = 0.01f;
        private const float SYNC_INTERVAL = 0.05f;  // Max 20 Hz

        private bool                   _initialized;
        private NetworkIdentity        _networkIdentity;
        private AdvancedShipController _shipController;

        private float[] _inputs = new float[INPUT_COUNT];
        private float[] _lastInputs = new float[INPUT_COUNT];
        private float _nextSyncTime;

        [SyncVar(hook = nameof(OnAnchorStateChanged))]
        private bool _anchorDropped;


        private void Initialize()
        {
            _shipController.MultiplayerIsRemote = !_networkIdentity.isLocalPlayer;

            // Disable water objects on remote clients if configured
            if (_shipController.MultiplayerIsRemote && !simulateWaterObjectsOnClient)
            {
                foreach (WaterObject waterObject in gameObject.GetComponentsInChildren<WaterObject>())
                {
                    waterObject.enabled = false;
                }
            }

            _initialized = true;
        }


        private void Awake()
        {
            _networkIdentity = GetComponent<NetworkIdentity>();
            _shipController  = GetComponent<AdvancedShipController>();
            _shipController.onShipInitialized.AddListener(Initialize);

            System.Array.Clear(_inputs, 0, INPUT_COUNT);
            System.Array.Clear(_lastInputs, 0, INPUT_COUNT);
        }


        private void FixedUpdate()
        {
            // Server: Synchronize anchor state changes
            if (isServer && _initialized)
            {
                bool currentAnchorState = _shipController.Anchor != null && _shipController.Anchor.Dropped;
                if (_anchorDropped != currentAnchorState)
                {
                    _anchorDropped = currentAnchorState;
                }
            }

            // Guard: Only send inputs if initialized and we own this ship
            if (!_initialized || !isOwned)
            {
                return;
            }

            if (Time.time < _nextSyncTime)
            {
                return;
            }

            GatherInputs();

            if (!InputsChanged(_inputs, _lastInputs))
            {
                return;
            }

            _nextSyncTime = Time.time + SYNC_INTERVAL;
            System.Array.Copy(_inputs, _lastInputs, INPUT_COUNT);

            if (isServer)
            {
                RpcSendInput(_inputs);
            }
            else
            {
                CmdSendInput(_inputs);
            }
        }


        private void GatherInputs()
        {
            _inputs[0] = _shipController.input.Steering;
            _inputs[1] = _shipController.input.Throttle;
            _inputs[2] = _shipController.input.Throttle2;
            _inputs[3] = _shipController.input.Throttle3;
            _inputs[4] = _shipController.input.Throttle4;
            _inputs[5] = _shipController.input.BowThruster;
            _inputs[6] = _shipController.input.SternThruster;
            _inputs[7] = _shipController.input.SubmarineDepth;
            _inputs[8] = _shipController.input.RotateSail;
            _inputs[9] = _shipController.input.EngineStartStop ? 1f : 0f;
        }


        private bool InputsChanged(float[] a, float[] b)
        {
            for (int i = 0; i < INPUT_COUNT; i++)
            {
                if (Mathf.Abs(a[i] - b[i]) > INPUT_THRESHOLD)
                {
                    return true;
                }
            }
            return false;
        }


        [Command]
        private void CmdSendInput(float[] inputs)
        {
            ValidateInputs(inputs);
            ApplyInputs(inputs);
            RpcSendInput(inputs);
        }


        [ClientRpc]
        private void RpcSendInput(float[] inputs)
        {
            if (isOwned)
            {
                return;
            }
            ApplyInputs(inputs);
        }


        private void ApplyInputs(float[] inputs)
        {
            _shipController.input.Steering        = inputs[0];
            _shipController.input.Throttle        = inputs[1];
            _shipController.input.Throttle2       = inputs[2];
            _shipController.input.Throttle3       = inputs[3];
            _shipController.input.Throttle4       = inputs[4];
            _shipController.input.BowThruster     = inputs[5];
            _shipController.input.SternThruster   = inputs[6];
            _shipController.input.SubmarineDepth  = inputs[7];
            _shipController.input.RotateSail      = inputs[8];
            _shipController.input.EngineStartStop = inputs[9] > 0.5f;
        }


        private void ValidateInputs(float[] inputs)
        {
            inputs[0] = Mathf.Clamp(inputs[0], -1f, 1f);
            inputs[1] = Mathf.Clamp01(inputs[1]);
            inputs[2] = Mathf.Clamp01(inputs[2]);
            inputs[3] = Mathf.Clamp01(inputs[3]);
            inputs[4] = Mathf.Clamp01(inputs[4]);
            inputs[5] = Mathf.Clamp(inputs[5], -1f, 1f);
            inputs[6] = Mathf.Clamp(inputs[6], -1f, 1f);
            inputs[7] = Mathf.Clamp(inputs[7], -1f, 1f);
            inputs[8] = Mathf.Clamp(inputs[8], -1f, 1f);
            inputs[9] = Mathf.Clamp01(inputs[9]);
        }


        /// <summary>
        /// SyncVar hook: Called on clients when anchor state changes on server.
        /// </summary>
        private void OnAnchorStateChanged(bool oldValue, bool newValue)
        {
            if (_shipController == null || _shipController.Anchor == null)
            {
                return;
            }

            // Apply anchor state on remote clients
            if (newValue && !_shipController.Anchor.Dropped)
            {
                _shipController.Anchor.Drop();
            }
            else if (!newValue && _shipController.Anchor.Dropped)
            {
                _shipController.Anchor.Weigh();
            }
        }


        private void OnDestroy()
        {
            if (_shipController != null)
            {
                _shipController.onShipInitialized.RemoveListener(Initialize);
            }
        }
    }
}