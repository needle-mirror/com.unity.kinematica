using UnityEngine;

using Unity.Burst;
using UnityEngine.Assertions;

namespace Unity.Kinematica
{
    /// <summary>
    /// The push task injects a referenced time index into the
    /// pose stream of the motion synthesizer.
    /// </summary>
    [Data("Push", "#2A3756"), BurstCompile]
    public struct PushTask : Task
    {
        internal MemoryRef<MotionSynthesizer> synthesizer;

        [Input("Sampling Time")]
        Identifier<TimeIndex> timeIndex;

        /// <summary>
        /// Execute method for the push task.
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
                synthesizer.Push(timeIndex.Ref);

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
        public static Result ExecuteSelf(ref TaskRef self)
        {
            return self.Cast<PushTask>().Execute();
        }

        internal static PushTask Create(ref MotionSynthesizer synthesizer, Identifier<TimeIndex> timeIndex)
        {
            return new PushTask
            {
                synthesizer = synthesizer.self,
                timeIndex = timeIndex
            };
        }
    }
}
