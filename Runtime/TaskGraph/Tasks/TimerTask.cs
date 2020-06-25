using Unity.Burst;

namespace Unity.Kinematica
{
    /// <summary>
    /// The timer task returns a running status for a specified
    /// amount of time in seconds and a success status afterwards.
    /// </summary>
    [Data("Timer", "#2A3756"), BurstCompile]
    public struct TimerTask : Task, GenericTask<TimerTask>
    {
        public Identifier<TimerTask> self { get; set; }

        internal MemoryRef<MotionSynthesizer> synthesizer;

        [Property]
        float timeInSeconds;

        /// <summary>
        /// Execute method for the timer task.
        /// </summary>
        /// <remarks>
        /// The timer task returns a running status for a specified
        /// amount of time in seconds and a success status afterwards.
        /// </remarks>
        /// <returns>Returns running for the duration of the specified time; success upon completion.</returns>
        public Result Execute()
        {
            ref var synthesizer = ref this.synthesizer.Ref;

            if (timeInSeconds > 0.0f)
            {
                timeInSeconds -= synthesizer.deltaTime;

                if (timeInSeconds <= 0.0f)
                {
                    timeInSeconds = 0.0f;

                    return Result.Success;
                }
            }

            return Result.Running;
        }

        /// <summary>
        /// Surrogate method for automatic task execution.
        /// </summary>
        /// <param name="self">Task reference that is supposed to be executed.</param>
        /// <returns>Result of the task execution.</returns>
        [BurstCompile]
        public static Result ExecuteSelf(ref TaskPointer self)
        {
            return self.Cast<TimerTask>().Execute();
        }

        internal static TimerTask Create(ref MotionSynthesizer synthesizer, float timeInSeconds)
        {
            return new TimerTask
            {
                synthesizer = synthesizer.self,
                timeInSeconds = timeInSeconds
            };
        }
    }
}
