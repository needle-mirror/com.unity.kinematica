using Unity.Mathematics;

namespace Unity.Kinematica
{
    /// <summary>
    /// Data type to be used in a task graph that represents a trajectory.
    /// </summary>
    [Data("Trajectory")]
    public struct Trajectory
    {
        /// <summary>
        /// Denotes the root transform of this trajectory element.
        /// </summary>
        public AffineTransform transform;

        /// <summary>
        /// Creates a trajectory element.
        /// </summary>
        /// <param name="transform">The transform to be used for this trajectory element.</param>
        /// <returns>The corresponding trajectory element.</returns>
        public static Trajectory Create(AffineTransform transform)
        {
            return new Trajectory
            {
                transform = transform
            };
        }

        /// <summary>
        /// Identity trajectory.
        /// </summary>
        public static Trajectory Identity
        {
            get => Create(AffineTransform.identity);
        }
    }
}
