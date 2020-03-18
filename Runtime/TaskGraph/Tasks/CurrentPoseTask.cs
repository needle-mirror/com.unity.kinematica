using Unity.Burst;

namespace Unity.Kinematica
{
    /// <summary>
    /// The current pose task updates a sampling time
    /// based on the current sampling time of the motion synthesizer.
    /// </summary>
    [Data("CurrentPose", "#2A3756"), BurstCompile]
    public struct CurrentPoseTask : Task
    {
        internal Identifier<CurrentPoseTask> self;

        internal MemoryRef<MotionSynthesizer> synthesizer;

        /// <summary>
        /// Denotes a reference to the sampling time to be updated.
        /// </summary>
        [Output("Time Index")]
        public Identifier<SamplingTime> samplingTime;

        /// <summary>
        /// Execute method for the current pose task.
        /// </summary>
        /// <remarks>
        /// The current pose task extracts the current sampling
        /// time from the motion sysntehsizer. This sampling time
        /// can in turn be consumed by other tasks.
        /// </remarks>
        /// <returns>Always returns a success status.</returns>
        public unsafe Result Execute()
        {
            ref var synthesizer = ref this.synthesizer.Ref;

            synthesizer.GetRef<SamplingTime>(
                samplingTime).Ref = synthesizer.Time;

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
            return self.Cast<CurrentPoseTask>().Execute();
        }

        internal CurrentPoseTask(ref MotionSynthesizer synthesizer, Identifier<SamplingTime> samplingTime)
        {
            this.synthesizer = synthesizer.self;

            this.samplingTime = samplingTime;

            self = Identifier<CurrentPoseTask>.Invalid;
        }

        internal static CurrentPoseTask Create(ref MotionSynthesizer synthesizer, Identifier<SamplingTime> samplingTime)
        {
            return new CurrentPoseTask(ref synthesizer, samplingTime);
        }

        /// <summary>
        /// Implicit cast operator that allows to convert a current pose task into a typed identifier.
        /// </summary>
        public static implicit operator Identifier<CurrentPoseTask>(CurrentPoseTask task)
        {
            return task.self;
        }
    }
}
