using Unity.Burst;
using Unity.Mathematics;

namespace Unity.Kinematica
{
    /// <summary>
    /// The trajectory prediction task generates a desired future trajectoy
    /// subject to a desired displacement direction, desired forward direction
    /// and desired linear speed.
    /// </summary>
    [Data("TrajectoryPrediction", "#2A3756"), BurstCompile]
    public struct TrajectoryPredictionTask : Task
    {
        internal Identifier<TrajectoryPredictionTask> self;

        internal MemoryRef<MotionSynthesizer> synthesizer;

        /// <summary>
        /// Denotes the desired movement direction to be used for trajectory prediction.
        /// </summary>
        /// <remarks>
        /// The movement direction is expressed in character space, i.e. relative to the
        /// current orientation of the character. It indicates the desired future root
        /// displacement that is to be reached at the end of the predicted trajectory.
        /// </remarks>
        public float3 movementDirection;

        /// <summary>
        /// Denotes the desired forward direction to be used for trajectory prediction.
        /// </summary>
        /// <remarks>
        /// The forward direction is expressed in character space, i.e. relative to the
        /// current orientation of the character. It indicates the desired facing direction
        /// that is to be reached at the end of the predicted trajectory.
        /// </remarks>
        public float3 forwardDirection;

        /// <summary>
        /// Denotes the desired linear speed to be used for trajectory prediction.
        /// </summary>
        /// <remarks>
        /// The linear speed is expressed in meters per second and indicates the desired
        /// root speed that is to be reached at the end of the predicted trajectory.
        /// </remarks>
        public float linearSpeed;

        /// <summary>
        /// Denotes a canonical value that determines how fast the the prediction reaches the desired velocity.
        /// </summary>
        public float velocityFactor;

        /// <summary>
        /// Denotes a canonical value that determines how fast the the prediction reaches the desired orientation.
        /// </summary>
        public float rotationFactor;

        /// <summary>
        /// Denotes a reference to the trajectory to be generated.
        /// </summary>
        [Output("Trajectory")]
        public Identifier<Trajectory> trajectory;

        /// <summary>
        /// Execute method for the trajectory prediction task.
        /// </summary>
        /// <remarks>
        /// The trajectory prediction task generates a desired future trajectoy
        /// subject to a desired displacement direction, desired forward direction
        /// and desired linear speed. The better the predicted trajectory matches
        /// the poses (and therefore the implicitly contained trajectories for each
        /// pose) in a given input set, the better result can be expected from
        /// a pose reduction (pose matching).
        /// </remarks>
        /// <returns>Always returns a success status.</returns>
        /// <seealso cref="ReduceTask"/>
        public unsafe Result Execute()
        {
            ref var synthesizer = ref this.synthesizer.Ref;

            var desiredVelocity = movementDirection * linearSpeed;

            var desiredRotation =
                Missing.forRotation(Missing.forward, forwardDirection);

            var trajectory =
                synthesizer.GetArray<AffineTransform>(
                    this.trajectory);

            synthesizer.trajectory.Array.CopyTo(ref trajectory);

            var prediction = TrajectoryPrediction.Create(
                ref synthesizer, desiredVelocity, desiredRotation,
                trajectory, velocityFactor, rotationFactor);

            prediction.Generate();

            return Result.Success;
        }

        /// <summary>
        /// Surrogate method for automatic task execution.
        /// </summary>
        /// <param name="self">Task reference that is supposed to be executed.</param>
        /// <returns>Result of the task execution.</returns>
        [BurstCompile]
        public static Result ExecuteSelf(ref TaskRef self)
        {
            return self.Cast<TrajectoryPredictionTask>().Execute();
        }

        internal TrajectoryPredictionTask(ref MotionSynthesizer synthesizer, Identifier<Trajectory> trajectory)
        {
            this.synthesizer = synthesizer.self;

            this.trajectory = trajectory;

            self = Identifier<TrajectoryPredictionTask>.Invalid;

            movementDirection = Missing.forward;
            forwardDirection = Missing.forward;

            linearSpeed = 0.0f;

            velocityFactor = 1.0f;
            rotationFactor = 1.0f;
        }

        internal static TrajectoryPredictionTask Create(ref MotionSynthesizer synthesizer, Identifier<Trajectory> trajectory)
        {
            return new TrajectoryPredictionTask(ref synthesizer, trajectory);
        }

        /// <summary>
        /// Implicit cast operator that allows to convert a trajectory prediction task into a typed identifier.
        /// </summary>
        public static implicit operator Identifier<TrajectoryPredictionTask>(TrajectoryPredictionTask task)
        {
            return task.self;
        }
    }
}
