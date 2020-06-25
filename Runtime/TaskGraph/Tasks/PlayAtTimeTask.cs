using UnityEngine;

using Unity.Burst;
using UnityEngine.Assertions;

namespace Unity.Kinematica
{
    /// <summary>
    /// The task injects a referenced time index into the
    /// pose stream of the motion synthesizer.
    /// </summary>
    [Data("PlayAtTime", "#2A3756"), BurstCompile]
    public struct PlayAtTimeTask : Task, GenericTask<PlayAtTimeTask>
    {
        public Identifier<PlayAtTimeTask> self { get; set; }

        internal MemoryRef<MotionSynthesizer> synthesizer;

        [Input("Sampling Time")]
        Identifier<TimeIndex> timeIndex;

        /// <summary>
        /// Execute method for the task.
        /// </summary>
        /// <remarks>
        /// The push task injects a referenced time index into the
        /// pose stream of the motion synthesizer.
        /// </remarks>
        /// <returns>Returns true if the time index is valid; false otherwise.</returns>
        public Result Execute()
        {
            ref var synthesizer = ref this.synthesizer.Ref;

            var timeIndex =
                synthesizer.GetRef<TimeIndex>(
                    this.timeIndex);

            Assert.IsTrue(timeIndex.IsValid);

            if (timeIndex.Ref.IsValid)
            {
                synthesizer.PlayAtTime(timeIndex.Ref);

                return Result.Success;
            }

            return Result.Failure;
        }

        /// <summary>
        /// Surrogate method for automatic task execution.
        /// </summary>
        /// <param name="self">Task reference that is supposed to be executed.</param>
        /// <returns>Result of the task execution.</returns>
        [BurstCompile]
        public static Result ExecuteSelf(ref TaskPointer self)
        {
            return self.Cast<PlayAtTimeTask>().Execute();
        }

        internal static PlayAtTimeTask Create(ref MotionSynthesizer synthesizer, Identifier<TimeIndex> timeIndex)
        {
            return new PlayAtTimeTask
            {
                synthesizer = synthesizer.self,
                timeIndex = timeIndex
            };
        }
    }
}
