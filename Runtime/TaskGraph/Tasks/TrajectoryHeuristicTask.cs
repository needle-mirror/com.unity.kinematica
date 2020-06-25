using UnityEngine;

using Unity.Burst;
using Unity.Mathematics;

using CodeBookIndex = Unity.Kinematica.Binary.CodeBookIndex;

namespace Unity.Kinematica
{
    /// <summary>
    /// The trajectory heuristic task compares the trajectories of a current
    /// and candidate pose to a desired trajectory.
    /// </summary>
    [Data("TrajectoryHeuristic", "#2A3756"), BurstCompile]
    public struct TrajectoryHeuristicTask : Task, GenericTask<TrajectoryHeuristicTask>
    {
        public Identifier<TrajectoryHeuristicTask> self { get; set; }

        internal MemoryRef<MotionSynthesizer> synthesizer;

        /// <summary>
        /// Denotes the candidate time index to be used for the heuristic evaluation.
        /// </summary>
        [Input("Candidate Time")]
        public Identifier<TimeIndex> candidate;

        /// <summary>
        /// Denotes the desired trajectory to be used for the heuristic evaluation.
        /// </summary>
        [Input("Desired trajectory")]
        public Identifier<Trajectory> desiredTrajectory;

        /// <summary>
        /// Denotes the current time index to be used for the heuristic evaluation.
        /// </summary>
        [Output("Time Index")]
        public Identifier<TimeIndex> timeIndex;

        /// <summary>
        /// Denotes the relative threshold to be used for the heuristic evaluation.
        /// </summary>
        public float threshold;

        /// <summary>
        /// Execute method for the trajectory heuristic task.
        /// </summary>
        /// <remarks>
        /// Animation poses implicitely define a future trajectory by looking at
        /// the subsequent root transforms given a reference pose.
        /// The trajectory heuristic task compares the trajectories of a current
        /// and candidate pose to a desired trajectory. It returns Success if
        /// the candidate trajectory performs better than the current trajectory
        /// subject to a user defined threshold. The purpose of this heuristic
        /// is to avoid pushing a new sampling time to the motion synthesizer too
        /// frequently. Ideally the goal is to only switch to a new sampling time
        /// if a potential candidate performs much better than the sampling time
        /// that is currently active.
        /// </remarks>
        /// <returns>Result success if the candidate pose performs better the current pose subject to the desired trajectory; Failure otherwise.</returns>
        public Result Execute()
        {
            ref var synthesizer = ref this.synthesizer.Ref;

            var candidate =
                synthesizer.GetRef<TimeIndex>(
                    this.candidate).Ref;

            var trajectory =
                synthesizer.GetArray<AffineTransform>(
                    desiredTrajectory);

            var result = synthesizer.GetRef<TimeIndex>(timeIndex);

            if (RelativeDeviation(ref synthesizer, candidate, trajectory) >= threshold)
            {
                result.Ref = candidate;

                return Result.Success;
            }
            else
            {
                result.Ref = TimeIndex.Invalid;

                return Result.Failure;
            }
        }

        float RelativeDeviation(ref MotionSynthesizer synthesizer, TimeIndex candidate, MemoryArray<AffineTransform> trajectory)
        {
            ref var binary = ref synthesizer.Binary;

            var codeBookIndex = GetCodeBookIndex(ref binary, candidate);

            ref var codeBook = ref binary.GetCodeBook(codeBookIndex);

            var metricIndex = codeBook.metricIndex;

            var candidateFragment =
                binary.CreateTrajectoryFragment(
                    metricIndex, SamplingTime.Create(candidate));

            var currentFragment =
                binary.CreateTrajectoryFragment(
                    metricIndex, synthesizer.Time);

            var desiredFragment =
                binary.CreateTrajectoryFragment(
                    metricIndex, trajectory);

            codeBook.trajectories.Normalize(candidateFragment.array);
            codeBook.trajectories.Normalize(currentFragment.array);
            codeBook.trajectories.Normalize(desiredFragment.array);

            var current2Desired =
                codeBook.trajectories.FeatureDeviation(
                    currentFragment.array, desiredFragment.array);

            var candidate2Desired =
                codeBook.trajectories.FeatureDeviation(
                    candidateFragment.array, desiredFragment.array);

            desiredFragment.Dispose();
            currentFragment.Dispose();
            candidateFragment.Dispose();

            return current2Desired - candidate2Desired;
        }

        CodeBookIndex GetCodeBookIndex(ref Binary binary, TimeIndex timeIndex)
        {
            int numCodeBooks = binary.numCodeBooks;

            for (int i = 0; i < numCodeBooks; ++i)
            {
                ref var codeBook = ref binary.GetCodeBook(i);

                int numIntervals = codeBook.intervals.Length;

                for (int j = 0; j < numIntervals; ++j)
                {
                    var intervalIndex = codeBook.intervals[j];

                    ref var interval = ref binary.GetInterval(intervalIndex);

                    if (interval.segmentIndex == timeIndex.segmentIndex)
                    {
                        return i;
                    }
                }
            }

            return CodeBookIndex.Invalid;
        }

        /// <summary>
        /// Surrogate method for automatic task execution.
        /// </summary>
        /// <param name="self">Task reference that is supposed to be executed.</param>
        /// <returns>Result of the task execution.</returns>
        [BurstCompile]
        public static Result ExecuteSelf(ref TaskPointer self)
        {
            return self.Cast<TrajectoryHeuristicTask>().Execute();
        }

        internal static TrajectoryHeuristicTask Create(ref MotionSynthesizer synthesizer, Identifier<TimeIndex> candidate, Identifier<Trajectory> trajectory, Identifier<TimeIndex> timeIndex)
        {
            return new TrajectoryHeuristicTask
            {
                self = Identifier<TrajectoryHeuristicTask>.Invalid,
                synthesizer = synthesizer.self,
                candidate = candidate,
                desiredTrajectory = trajectory,
                timeIndex = timeIndex,
                threshold = 0.03f
            };
        }

        /// <summary>
        /// Implicit cast operator that allows to convert a task reference into an typed identifier.
        /// </summary>
        public static implicit operator Identifier<TrajectoryHeuristicTask>(TrajectoryHeuristicTask task)
        {
            return task.self;
        }
    }
}
