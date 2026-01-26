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

namespace NWH.DWP2.Multiplayer.DWP2
{
    [CustomEditor(typeof(MirrorMultiplayerShip))]
    [CanEditMultipleObjects]
    public class MirrorMultiplayerShipEditor : NUIEditor
    {
        public override bool OnInspectorNUI()
        {
            if (!base.OnInspectorNUI())
            {
                return false;
            }

            MirrorMultiplayerShip mms = target as MirrorMultiplayerShip;

            drawer.Field("simulateWaterObjectsOnClient");

            drawer.EndEditor();
            return true;
        }


        public override bool UseDefaultMargins()
        {
            return false;
        }
    }
}

#endif