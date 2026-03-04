using System;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Digger.Modules.Core.Sources.NativeCollections
{
    [StructLayout(LayoutKind.Sequential)]
    [NativeContainer]
    unsafe public struct LongNativeCounter
    {
        // The actual pointer to the allocated count needs to have restrictions relaxed so jobs can be schedled with this container
        [NativeDisableUnsafePtrRestriction] long* m_Counter;
        private long increment;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle m_Safety;

        // The dispose sentinel tracks memory leaks. It is a managed type so it is cleared to null when scheduling a job
        // The job cannot dispose the container, and no one else can dispose it until the job has run so it is ok to not pass it along
        // This attribute is required, without it this native container cannot be passed to a job since that would give the job access to a managed object
        [NativeSetClassTypeToNullOnSchedule] DisposeSentinel m_DisposeSentinel;
#endif

        // Keep track of where the memory for this was allocated
        Allocator m_AllocatorLabel;

        public LongNativeCounter(Allocator label, long inc = 1)
        {
            // This check is redundant since we always use a long which is blittable.
            // It is here as an example of how to check for type correctness for generic types.
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!UnsafeUtility.IsBlittable<long>())
                throw new ArgumentException(string.Format("{0} used in NativeQueue<{0}> must be blittable", typeof(long)));
#endif
            m_AllocatorLabel = label;
            increment = inc;

            // Allocate native memory for a single long integer
            m_Counter = (long*) UnsafeUtility.Malloc(UnsafeUtility.SizeOf<long>(), 8, label);

            // Create a dispose sentinel to track memory leaks. This also creates the AtomicSafetyHandle
#if ENABLE_UNITY_COLLECTIONS_CHECKS
#if UNITY_2018_3_OR_NEWER
            DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, 0, label);
#else
        DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, 0);
#endif
#endif
            // Initialize the count to 0 to avoid uninitialized data
            Count = 0;
        }

        public long Increment()
        {
            // Verify that the caller has write permission on this data.
            // This is the race condition protection, without these checks the AtomicSafetyHandle is useless
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            return (*m_Counter) += increment;
        }

        public long Decrement()
        {
            // Verify that the caller has write permission on this data.
            // This is the race condition protection, without these checks the AtomicSafetyHandle is useless
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            return (*m_Counter) -= increment;
        }

        public long Add(long value)
        {
            // Verify that the caller has write permission on this data.
            // This is the race condition protection, without these checks the AtomicSafetyHandle is useless
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            return (*m_Counter) += value;
        }

        public long Count {
            get {
                // Verify that the caller has read permission on this data.
                // This is the race condition protection, without these checks the AtomicSafetyHandle is useless
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
                return *m_Counter;
            }
            set {
                // Verify that the caller has write permission on this data. This is the race condition protection, without these checks the AtomicSafetyHandle is useless
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
                *m_Counter = value;
            }
        }

        public bool IsCreated {
            get { return m_Counter != null; }
        }

        public void Dispose()
        {
            // Let the dispose sentinel know that the data has been freed so it does not report any memory leaks
#if ENABLE_UNITY_COLLECTIONS_CHECKS
#if UNITY_2018_3_OR_NEWER
            DisposeSentinel.Dispose(ref m_Safety, ref m_DisposeSentinel);
#else
        DisposeSentinel.Dispose(m_Safety, ref m_DisposeSentinel);
#endif
#endif

            UnsafeUtility.Free(m_Counter, m_AllocatorLabel);
            m_Counter = null;
        }

        public Concurrent ToConcurrent()
        {
            Concurrent concurrent;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
            concurrent.m_Safety = m_Safety;
            AtomicSafetyHandle.UseSecondaryVersion(ref concurrent.m_Safety);
#endif

            concurrent.increment = increment;
            concurrent.m_Counter = m_Counter;
            return concurrent;
        }

        public ConcurrentDouble ToConcurrentDouble(long multiplier = 1000000)
        {
            if (multiplier <= 0)
                throw new ArgumentException("Multiplier must be positive", nameof(multiplier));

            ConcurrentDouble concurrent;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
            concurrent.m_Safety = m_Safety;
            AtomicSafetyHandle.UseSecondaryVersion(ref concurrent.m_Safety);
#endif

            concurrent.m_Counter = m_Counter;
            concurrent.multiplier = multiplier;
            concurrent.maxSafeDouble = (double)long.MaxValue / multiplier;
            concurrent.minSafeDouble = (double)long.MinValue / multiplier;
            return concurrent;
        }

        [NativeContainer]
        // This attribute is what makes it possible to use LongNativeCounter.Concurrent in a ParallelFor job
        [NativeContainerIsAtomicWriteOnly]
        unsafe public struct Concurrent
        {
            // Copy of the pointer from the full LongNativeCounter
            [NativeDisableUnsafePtrRestriction] internal long* m_Counter;
            internal long increment;

            // Copy of the AtomicSafetyHandle from the full LongNativeCounter. The dispose sentinel is not copied since this inner struct does not own the memory and is not responsible for freeing it
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            internal AtomicSafetyHandle m_Safety;
#endif

            public long Increment()
            {
                // Increment still needs to check for write permissions
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
                // The actual increment is implemented with an atomic since it can be incremented by multiple threads at the same time
                return Interlocked.Add(ref *m_Counter, increment);
            }

            public long Decrement()
            {
                // Increment still needs to check for write permissions
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
                // The actual decrement is implemented with an atomic since it can be decremented by multiple threads at the same time
                return Interlocked.Add(ref *m_Counter, -increment);
            }

            public long Add(long value)
            {
                // Add still needs to check for write permissions
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
                // The actual add is implemented with an atomic since it can be modified by multiple threads at the same time
                return Interlocked.Add(ref *m_Counter, value);
            }
        }

        [NativeContainer]
        [NativeContainerIsAtomicWriteOnly]
        unsafe public struct ConcurrentDouble
        {
            [NativeDisableUnsafePtrRestriction] internal long* m_Counter;
            internal long multiplier;
            internal double maxSafeDouble;
            internal double minSafeDouble;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            internal AtomicSafetyHandle m_Safety;
#endif

            public void Add(double value)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
                double clamped = math.clamp(value, minSafeDouble, maxSafeDouble);
                long longValue = (long)(clamped * multiplier);
                Interlocked.Add(ref *m_Counter, longValue);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    [NativeContainer]
    unsafe public struct LongNativePerThreadCounter
    {
        // The actual pointer to the allocated count needs to have restrictions relaxed so jobs can be schedled with this container
        [NativeDisableUnsafePtrRestriction] long* m_Counter;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle m_Safety;

        // The dispose sentinel tracks memory leaks. It is a managed type so it is cleared to null when scheduling a job
        // The job cannot dispose the container, and no one else can dispose it until the job has run so it is ok to not pass it along
        // This attribute is required, without it this native container cannot be passed to a job since that would give the job access to a managed object
        [NativeSetClassTypeToNullOnSchedule] DisposeSentinel m_DisposeSentinel;
#endif

        // Keep track of where the memory for this was allocated
        Allocator m_AllocatorLabel;

        public const int LongsPerCacheLine = JobsUtility.CacheLineSize / sizeof(long);

        public LongNativePerThreadCounter(Allocator label)
        {
            // This check is redundant since we always use a long which is blittable.
            // It is here as an example of how to check for type correctness for generic types.
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!UnsafeUtility.IsBlittable<long>())
                throw new ArgumentException(string.Format("{0} used in NativeQueue<{0}> must be blittable", typeof(long)));
#endif
            m_AllocatorLabel = label;

            // One full cache line (longs per cacheline * size of long) for each potential worker index, JobsUtility.MaxJobThreadCount
            m_Counter = (long*) UnsafeUtility.Malloc(UnsafeUtility.SizeOf<long>() * LongsPerCacheLine * JobsUtility.MaxJobThreadCount, 8, label);

            // Create a dispose sentinel to track memory leaks. This also creates the AtomicSafetyHandle
#if ENABLE_UNITY_COLLECTIONS_CHECKS
#if UNITY_2018_3_OR_NEWER
            DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, 0, label);
#else
        DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, 0);
#endif
#endif
            // Initialize the count to 0 to avoid uninitialized data
            Count = 0;
        }

        public void Increment()
        {
            // Verify that the caller has write permission on this data.
            // This is the race condition protection, without these checks the AtomicSafetyHandle is useless
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            (*m_Counter)++;
        }

        public void Add(long value)
        {
            // Verify that the caller has write permission on this data.
            // This is the race condition protection, without these checks the AtomicSafetyHandle is useless
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            (*m_Counter) += value;
        }

        public long Count {
            get {
                // Verify that the caller has read permission on this data.
                // This is the race condition protection, without these checks the AtomicSafetyHandle is useless
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
                long count = 0;
                for (int i = 0; i < JobsUtility.MaxJobThreadCount; ++i)
                    count += m_Counter[LongsPerCacheLine * i];
                return count;
            }
            set {
                // Verify that the caller has write permission on this data.
                // This is the race condition protection, without these checks the AtomicSafetyHandle is useless
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
                // Clear all locally cached counts,
                // set the first one to the required value
                for (int i = 1; i < JobsUtility.MaxJobThreadCount; ++i)
                    m_Counter[LongsPerCacheLine * i] = 0;
                *m_Counter = value;
            }
        }

        public bool IsCreated {
            get { return m_Counter != null; }
        }

        public void Dispose()
        {
            // Let the dispose sentinel know that the data has been freed so it does not report any memory leaks
#if ENABLE_UNITY_COLLECTIONS_CHECKS
#if UNITY_2018_3_OR_NEWER
            DisposeSentinel.Dispose(ref m_Safety, ref m_DisposeSentinel);
#else
        DisposeSentinel.Dispose(m_Safety, ref m_DisposeSentinel);
#endif
#endif

            UnsafeUtility.Free(m_Counter, m_AllocatorLabel);
            m_Counter = null;
        }

        public Concurrent ToConcurrent()
        {
            Concurrent concurrent;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
            concurrent.m_Safety = m_Safety;
            AtomicSafetyHandle.UseSecondaryVersion(ref concurrent.m_Safety);
#endif

            concurrent.m_Counter = m_Counter;
            concurrent.m_ThreadIndex = 0;
            return concurrent;
        }

        [NativeContainer]
        [NativeContainerIsAtomicWriteOnly]
        // Let the JobSystem know that it should inject the current worker index into this container
        unsafe public struct Concurrent
        {
            [NativeDisableUnsafePtrRestriction] internal long* m_Counter;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            internal AtomicSafetyHandle m_Safety;
#endif

            // The current worker thread index, it must use this exact name since it is injected
            [NativeSetThreadIndex] internal int m_ThreadIndex;

            public void Increment()
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
                // No need for atomics any more since we are just incrementing the local count
                ++m_Counter[LongsPerCacheLine * m_ThreadIndex];
            }

            public void Add(long value)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
                // No need for atomics any more since we are just modifying the local count
                m_Counter[LongsPerCacheLine * m_ThreadIndex] += value;
            }
        }
    }
}

