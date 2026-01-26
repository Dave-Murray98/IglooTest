// ╔════════════════════════════════════════════════════════════════╗
// ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// ║    Licensed under Unity Asset Store Terms of Service:          ║
// ║        https://unity.com/legal/as-terms                        ║
// ║    Use permitted only in compliance with the License.          ║
// ║    Distributed "AS IS", without warranty of any kind.          ║
// ╚════════════════════════════════════════════════════════════════╝

#region

using NWH.DWP2.ShipController;
using NWH.DWP2.WaterObjects;
using Photon.Pun;
using UnityEngine;

#endregion

namespace NWH.DWP2.Multiplayer.PUN2
{
    /// <summary>
    /// Adds multi-player functionality to a vehicle through Photon Unity Networking 2.
    /// Uses PhotonRigidbodyView for physics synchronization (velocity) and PhotonTransformView for position/rotation.
    /// Note: Using both may cause jitter on some configurations. If issues occur, remove PhotonTransformView.
    /// </summary>
    [RequireComponent(typeof(PhotonRigidbodyView))]
    [RequireComponent(typeof(PhotonTransformView))]
    [RequireComponent(typeof(PhotonView))]
    [RequireComponent(typeof(AdvancedShipController))]
    public class PhotonMultiplayerShip : MonoBehaviour, IPunObservable
    {
        public  bool                simulateWaterObjectsOnClient = true;
        private PhotonRigidbodyView _photonRigidbodyView;
        private PhotonTransformView _photonTransformView;
        private PhotonView          _photonView;

        private AdvancedShipController _shipController;


        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            // Guard against null reference if called before initialization completes
            if (_shipController == null)
            {
                return;
            }

            if (stream.IsWriting)
            {
                // Send
                stream.SendNext(_shipController.input.Steering);
                stream.SendNext(_shipController.input.Throttle);
                stream.SendNext(_shipController.input.Throttle2);
                stream.SendNext(_shipController.input.Throttle3);
                stream.SendNext(_shipController.input.Throttle4);
                stream.SendNext(_shipController.input.BowThruster);
                stream.SendNext(_shipController.input.SternThruster);
                stream.SendNext(_shipController.input.SubmarineDepth);
                stream.SendNext(_shipController.input.RotateSail);
                stream.SendNext(_shipController.input.EngineStartStop);
                stream.SendNext(_shipController.input.Anchor);
            }
            else
            {
                // Receive
                _shipController.input.autoSetInput    = false;
                _shipController.input.Steering        = (float)stream.ReceiveNext();
                _shipController.input.Throttle        = (float)stream.ReceiveNext();
                _shipController.input.Throttle2       = (float)stream.ReceiveNext();
                _shipController.input.Throttle3       = (float)stream.ReceiveNext();
                _shipController.input.Throttle4       = (float)stream.ReceiveNext();
                _shipController.input.BowThruster     = (float)stream.ReceiveNext();
                _shipController.input.SternThruster   = (float)stream.ReceiveNext();
                _shipController.input.SubmarineDepth  = (float)stream.ReceiveNext();
                _shipController.input.RotateSail      = (float)stream.ReceiveNext();
                _shipController.input.EngineStartStop = (bool)stream.ReceiveNext();
                _shipController.input.Anchor          = (bool)stream.ReceiveNext();
            }
        }


        private void Initialize()
        {
            _shipController      = GetComponent<AdvancedShipController>();
            _photonView          = GetComponent<PhotonView>();
            _photonRigidbodyView = GetComponent<PhotonRigidbodyView>();
            _photonTransformView = GetComponent<PhotonTransformView>();

            // Note: PhotonNetwork.SendRate and SerializationRate should be configured globally
            // in your NetworkManager/connection script, not per-object. Default is 20/10.
            // Recommended: 20-30 for fast-paced gameplay, 10-15 for slower games.

            _shipController.MultiplayerIsRemote = !_photonView.IsMine;

            // Remote ships keep full physics simulation (non-kinematic) for realistic collisions
            // Both PhotonRigidbodyView and PhotonTransformView sync together to prevent desync
            // Note: May cause jitter on some setups; if so, remove PhotonTransformView component

            // Disable water objects if not local as the position is synced
            if (_shipController.MultiplayerIsRemote && !simulateWaterObjectsOnClient)
            {
                foreach (WaterObject waterObject in gameObject.GetComponentsInChildren<WaterObject>())
                {
                    waterObject.enabled = false;
                }
            }

            _photonView.ObservedComponents.Clear();
            _photonView.ObservedComponents.Add(_photonTransformView);
            _photonView.ObservedComponents.Add(_photonRigidbodyView);
            _photonView.ObservedComponents.Add(this);
        }


        private void Awake()
        {
            _shipController = GetComponent<AdvancedShipController>();
            _shipController.onShipInitialized.AddListener(Initialize);
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