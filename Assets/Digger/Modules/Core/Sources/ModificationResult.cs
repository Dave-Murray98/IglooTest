namespace Digger.Modules.Core.Sources
{
    /// <summary>
    /// Contains statistics about a terrain modification operation.
    /// </summary>
    public struct ModificationResult
    {
        /// <summary>
        /// Sum of voxel values that went from negative to positive (matter removed during digging).
        /// </summary>
        public double RemovedMatterQuantity;

        /// <summary>
        /// Sum of voxel values that went from positive to negative (matter added during building).
        /// </summary>
        public double AddedMatterQuantity;

        /// <summary>
        /// Total number of voxels that were modified.
        /// </summary>
        public int TotalModifiedVoxels;

        /// <summary>
        /// Average magnitude of change per modified voxel.
        /// </summary>
        public double AverageChangePerVoxel => TotalModifiedVoxels > 0
            ? (RemovedMatterQuantity + AddedMatterQuantity) / TotalModifiedVoxels
            : 0.0;

        /// <summary>
        /// An empty result with no modifications.
        /// </summary>
        public static ModificationResult Empty => new ModificationResult
        {
            RemovedMatterQuantity = 0,
            AddedMatterQuantity = 0,
            TotalModifiedVoxels = 0
        };

        /// <summary>
        /// Aggregates multiple modification results into a single result.
        /// </summary>
        public static ModificationResult Aggregate(ModificationResult a, ModificationResult b)
        {
            return new ModificationResult
            {
                RemovedMatterQuantity = a.RemovedMatterQuantity + b.RemovedMatterQuantity,
                AddedMatterQuantity = a.AddedMatterQuantity + b.AddedMatterQuantity,
                TotalModifiedVoxels = a.TotalModifiedVoxels + b.TotalModifiedVoxels
            };
        }

        /// <summary>
        /// Adds another result to this one.
        /// </summary>
        public void Add(ModificationResult other)
        {
            RemovedMatterQuantity += other.RemovedMatterQuantity;
            AddedMatterQuantity += other.AddedMatterQuantity;
            TotalModifiedVoxels += other.TotalModifiedVoxels;
        }
    }
}

