using System.Threading;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;

namespace Digger.Modules.Core.Sources.NativeCollections
{
    public static class Utils
    {
        public static void IncrementAt(NativeArray<int> bytes, int index)
        {
            unsafe {
                Interlocked.Increment(ref ((int*)bytes.GetUnsafePtr())[index]);
            }
        }

        public static void InterlockedAddDouble(NativeArray<long> array, int index, double value, long multiplier = 1000000, double safeMin = -1000000, double safeMax = 1000000)
        {
            // Clamp the value to safe bounds to prevent overflow
            double clamped = math.clamp(value, safeMin, safeMax);
            long longValue = (long)(clamped * multiplier);
            
            unsafe {
                Interlocked.Add(ref ((long*)array.GetUnsafePtr())[index], longValue);
            }
        }

        public static void SetZeroAt(NativeArray<int> bytes, int index)
        {
            unsafe {
                Interlocked.Exchange(ref ((int*)bytes.GetUnsafePtr())[index], 0);
            }
        }
    }
}