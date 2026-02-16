using System;
using UnityEngine;
using UnityEngine.VFX;
using System.Reflection;
using System.Linq;

namespace Kellojo.StylizedKelp {
    
    [CreateAssetMenu(menuName = "Stylized Kelp/Kelp Settings")]
    public class KelpSettings : ScriptableObject {

        [Header("Stem Settings")]
        public Color StemColor = new Color(88, 113, 17, 255);
        public float StemSegmentLength = 1f;
        public float StemGravity = -2f;

        [Header("Leaf Settings")] 
        [ColorUsage(false, true)] public Color LeafStartColor = new Color(88, 113, 17, 255);
        [ColorUsage(false, true)] public Color LeafTipColor = new Color(255, 253, 0, 255);
        public int LeavesPerStemSegment = 15;
        public float LeafLength = 2f;
        public float LeafWidth = 0.4f;

        [Header("Leaf Placement")]
        [GpuIgnored] public bool RandomizeLeafRotation = true;
        [Tooltip("How often should leaves wind around a given segment to calculate their rotation? Ignored if RandomizeLeafRotation is true.")] 
        public float LeafWindings = 5f;
        
        [Header("Leaf Physics")]
        public float LeafRighteningForce = -1f;
        public float LeafGravity = 1.75f;
        public float LeafTipGravityMultiplier = 0.85f;

        public event Action OnChange;

        private void OnValidate() {
            if (LeafWidth < 0.001f) LeafWidth = 0.001f;
            if (LeafLength < 0.001f) LeafLength = 0.001f;
            if (StemSegmentLength < 0.1f) StemSegmentLength = 0.1f;
            if (LeavesPerStemSegment < 0) LeavesPerStemSegment = 0;
            
            OnChange?.Invoke();
        }

        public KelpSettingsGpuData GetGpuRepresentation() {
            var data = new KelpSettingsGpuData();
            
            var sourceFields = this.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
            var targetFields = typeof(KelpSettingsGpuData).GetFields(BindingFlags.Public | BindingFlags.Instance);
            
            object boxedData = data;
            foreach (var field in sourceFields) {
                var isIgnored = field.GetCustomAttribute<GpuIgnored>() != null;
                if (isIgnored) continue;
                
                var targetField = targetFields.FirstOrDefault(f => f.Name == field.Name && f.FieldType == field.FieldType);
                if (targetField != null) {
                    var value = field.GetValue(this);
                    targetField.SetValue(boxedData, value);
                } else {
                    Debug.LogError($"KelpSettings: Field '{field.Name}' of type '{field.FieldType}' not found in target struct or type mismatch.");
                }
            }
            
            return (KelpSettingsGpuData)boxedData;
        }
        
        
        
    }

    [VFXType(VFXTypeAttribute.Usage.GraphicsBuffer)]
    public struct KelpSettingsGpuData {

        public Color StemColor;
        public float StemSegmentLength;
        public float StemGravity;
        
        public Color LeafStartColor;
        public Color LeafTipColor;
        public int LeavesPerStemSegment;
        public float LeafLength;
        public float LeafWidth;

        public float LeafWindings;
        
        public float LeafRighteningForce;
        public float LeafGravity;
        public float LeafTipGravityMultiplier;

        public static int Stride() {
            return sizeof(float) * 4 * 3
                   + sizeof(int) * 1
                   + sizeof(float) * 8;
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class GpuIgnored : Attribute { }

}

