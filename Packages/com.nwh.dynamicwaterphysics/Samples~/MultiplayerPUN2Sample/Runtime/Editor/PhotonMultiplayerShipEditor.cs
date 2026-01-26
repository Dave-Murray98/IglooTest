// ╔════════════════════════════════════════════════════════════════╗
// ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// ║    Licensed under Unity Asset Store Terms of Service:          ║
// ║        https://unity.com/legal/as-terms                        ║
// ║    Use permitted only in compliance with the License.          ║
// ║    Distributed "AS IS", without warranty of any kind.          ║
// ╚════════════════════════════════════════════════════════════════╝

#if UNITY_EDITOR

#region

using NWH.NUI;
using UnityEditor;

#endregion

namespace NWH.DWP2.Multiplayer.PUN2
{
    [CustomEditor(typeof(PhotonMultiplayerShip))]
    [CanEditMultipleObjects]
    public class PhotonMultiplayerShipEditor : NUIEditor
    {
        public override bool OnInspectorNUI()
        {
            if (!base.OnInspectorNUI())
            {
                return false;
            }

            PhotonMultiplayerShip pmv = target as PhotonMultiplayerShip;

            if (pmv == null)
            {
                drawer.EndEditor();
                return false;
            }

            drawer.Field("simulateWaterObjectsOnClient");
            drawer.Info(
                "'Observe option' field of Photon View is not settable through scripting so make sure it is not set to 'Off'.",
                MessageType.Warning);
            drawer.Info(
                "Configure PhotonNetwork.SendRate and SerializationRate globally in your NetworkManager, not per-ship.",
                MessageType.Info);

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