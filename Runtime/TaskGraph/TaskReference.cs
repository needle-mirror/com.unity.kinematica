using TimeIndexData = Unity.Kinematica.TimeIndex;
using SamplingTimeData = Unity.Kinematica.SamplingTime;
using QueryResultData = Unity.Kinematica.QueryResult;
using TrajectoryData = Unity.Kinematica.Trajectory;
using Unity.Mathematics;
using UnityEngine.Assertions;

namespace Unity.Kinematica
{
    /// <summary>
    /// Self sufficient task reference that can be used to access the task data and create children to that task
    /// without needing to pass the <code>MotionSynthesizer</code> as argument
    /// </summary>
    public struct TaskReference
    {
        MemoryIdentifier taskIdentifier;
        MemoryRef<MotionSynthesizer> synthesizer;

        ref MotionSynthesizer Synthesizer => ref synthesizer.Ref;

        public static TaskReference Create(MemoryIdentifier taskIdentifier, MemoryRef<MotionSynthesizer> synthesizer)
        {
            return new TaskReference()
            {
                taskIdentifier = taskIdentifier,
                synthesizer = synthesizer,
            };
        }

        /// <summary>
        /// Return the task object associated to this reference
        /// </summary>
        /// <typeparam name="T">Type of Task</typeparam>
        /// <returns>task object associated to this reference</returns>
        public ref T GetAs<T>() where T : struct, Task
        {
            return ref Synthesizer.GetRef<T>(taskIdentifier).Ref;
        }

        /// <summary>
        /// Searches for a child node based on its type.
        /// </summary>
        /// <remarks>
        /// Retrieves a reference to a data type that is a direct or indirect child
        /// of current task
        /// </remarks>
        /// <returns>Reference of the result.</returns>
        public ref T GetChildByType<T>() where T : struct
        {
            MemoryRef<T> result = Synthesizer.GetChildByType<T>(taskIdentifier);
            Assert.IsTrue(result.IsValid);
            return ref result.Ref;
        }

        public static implicit operator MemoryIdentifier(TaskReference taskReference) => taskReference.taskIdentifier;

        public TaskReference CreateChildTask<T>(T task) where T : struct, GenericTask<T>
        {
            MemoryIdentifier identifier = Synthesizer.Allocate(task, taskIdentifier);

            ref T childTask = ref Synthesizer.GetRef<T>(identifier).Ref;

            childTask.self = Identifier<T>.Create(identifier);

            return TaskReference.Create(childTask.self, synthesizer);
        }

        public Identifier<T> CreateChildData<T>(T data) where T : struct
        {
            MemoryIdentifier identifier = Synthesizer.Allocate(data, taskIdentifier);
            return Identifier<T>.Create(identifier);
        }

        public Identifier<T> CreateChildDataArray<T>(MemoryArray<T> data) where T : struct
        {
            MemoryIdentifier identifier = Synthesizer.Allocate(data, taskIdentifier);
            return Identifier<T>.Create(identifier);
        }

        /// <summary>
        /// Creates a new action task as a child of the current task.
        /// </summary>
        /// <returns>reference to the newly created action task.</returns>
        public TaskReference Action()
        {
            return CreateChildTask(ActionTask.Create(ref Synthesizer));
        }

        /// <summary>
        /// Creates a new parallel task as a child of the current task.
        /// </summary>
        /// <returns>reference to the newly created parallel task.</returns>
        public TaskReference Parallel()
        {
            return CreateChildTask(ParallelTask.Create(ref Synthesizer));
        }

        /// <summary>
        /// Creates a new sequence task as a child of the current task.
        /// </summary>
        /// <param name="loop">If false, once the sequence has finished executing all its children, it will do nothing and just return success. If true, sequence will reexecute all its children tasks indefinitely.</param>
        /// <param name="resetWhenNotExecuted">If true, and if the sequence isn't executed during one task graph pass, next time the sequence will be executed again, it will restart execution from its first child.</param>
        /// <returns>reference to the newly created sequence task.</returns>
        public TaskReference Sequence(bool loop = false, bool resetWhenNotExecuted = true)
        {
            return CreateChildTask(SequenceTask.Create(ref Synthesizer, loop, resetWhenNotExecuted));
        }

        /// <summary>
        /// Creates a new selector task as a child of the current task.
        /// </summary>
        /// <returns>reference to the newly created selector task.</returns>
        public TaskReference Selector()
        {
            return CreateChildTask(SelectorTask.Create(ref Synthesizer));
        }

        /// <summary>
        /// Creates a new condition task as a child of the current task.
        /// </summary>
        /// <returns>reference to the newly created condition task.</returns>
        public TaskReference Condition()
        {
            return CreateChildTask(ConditionTask.Create(ref Synthesizer));
        }

        /// <summary>
        /// Creates a new time index.
        /// </summary>
        /// <remarks>
        /// The value of the newly created time index will be set to invalid.
        /// </remarks>
        /// <returns>reference to the newly created time index.</returns>
        public Identifier<TimeIndexData> TimeIndex()
        {
            return CreateChildData(TimeIndexData.Invalid);
        }

        /// <summary>
        /// Creates a new time index.
        /// </summary>
        /// <remarks>
        /// The value of the newly created time index will be set to the time index passed as argument.
        /// </remarks>
        /// <param name="timeIndex">Time index value that is to be used as initial value.</param>
        /// <returns>reference to the newly created time index.</returns>
        public Identifier<TimeIndexData> TimeIndex(TimeIndexData timeIndex)
        {
            return CreateChildData(timeIndex);
        }

        /// <summary>
        /// Creates a new sampling time.
        /// </summary>
        /// <remarks>
        /// The value of the newly created sampling time will be set to invalid.
        /// </remarks>
        /// <returns>reference to the newly created sampling time.</returns>
        public Identifier<SamplingTimeData> SamplingTime()
        {
            return CreateChildData(SamplingTimeData.Invalid);
        }

        /// <summary>
        /// Creates a new sampling time.
        /// </summary>
        /// <remarks>
        /// The value of the newly created sampling time will be set to the sampling time passed as argument.
        /// </remarks>
        /// <param name="samplingTime">Sampling time value that is to be used as initial value.</param>
        /// <returns>reference to the newly created sampling time.</returns>
        public Identifier<SamplingTimeData> SamplingTime(SamplingTimeData samplingTime)
        {
            return CreateChildData(samplingTime);
        }

        /// <summary>
        /// Creates a new pose sequence.
        /// </summary>
        /// <remarks>
        /// This method allows the creation of a pose sequence that can be fed into a match fragment task.
        /// The pose sequence will be initialized based on the query result passed as argument.
        /// </remarks>
        /// <param name="result">Query result that is to be used as initial value of the pose sequence.</param>
        /// <returns>reference to the newly created pose sequence.</returns>
        /// <seealso cref="Query"/>
        /// <seealso cref="MatchFragmentTask"/>
        public Identifier<PoseSequence> QueryResult(QueryResultData result)
        {
            MemoryIdentifier identifier = Synthesizer.Allocate(result.sequences, taskIdentifier);
            return Identifier<PoseSequence>.Create(identifier);
        }

        /// <summary>
        /// Creates a new trajectory.
        /// </summary>
        /// <remarks>
        /// This method allows the creation of a trajectory that can be used for any task
        /// that requires a reference to a trajectory.
        /// </remarks>
        /// <returns>reference to the newly created trajectory.</returns>
        public unsafe Identifier<TrajectoryData> Trajectory()
        {
            MemoryArray<AffineTransform>  source = Synthesizer.TrajectoryArray;
            MemoryArray<TrajectoryData> trajectory = new MemoryArray<TrajectoryData>
            {
                ptr = source.ptr,
                length = source.length
            };

            Assert.IsTrue(sizeof(TrajectoryData) == sizeof(AffineTransform));

            return CreateChildDataArray(trajectory);
        }

        /// <summary>
        /// Creates a new trajectory prediction task as a child of current task.
        /// </summary>
        /// <returns>Reference to the newly created trajectory prediction task.</returns>
        public TaskReference TrajectoryPrediction()
        {
            Identifier<TrajectoryData> trajectory = Trajectory();
            return CreateChildTask(TrajectoryPredictionTask.Create(ref Synthesizer, trajectory));
        }

        /// <summary>
        /// Creates a new navigation task as child of current task
        /// </summary>
        /// <returns>Reference of navigation task</returns>
        public TaskReference Navigation()
        {
            Identifier<TrajectoryData> trajectory = Trajectory();
            return CreateChildTask(NavigationTask.Create(ref Synthesizer, trajectory));
        }

        /// <summary>
        /// Creates a new match fragment task as a child of current task.
        /// </summary>
        /// <param name="sequences">The pose sequence that the match fragment task receives as input.</param>
        /// <param name="samplingTime">Reference animation pose that the match fragment task will receive as input.</param>
        /// <param name="trajectory">The desired future trajectory that the match fragment task will receive as input.</param>
        /// <param name="threshold">The threshold that the match fragment task will receive as input.</param>
        /// <returns>Reference to the newly created match fragment task.</returns>
        /// <seealso cref="MatchFragmentTask"/>
        public TaskReference MatchFragment(Identifier<PoseSequence> sequences, Identifier<SamplingTimeData> samplingTime, Identifier<TrajectoryData> trajectory, float threshold = 0.0f)
        {
            Identifier<TimeIndexData> closestMatch = TimeIndex();
            TaskReference matchFragmentTask = CreateChildTask(MatchFragmentTask.Create(ref Synthesizer,
                sequences,
                samplingTime,
                trajectory,
                closestMatch));

            matchFragmentTask.GetAs<MatchFragmentTask>().threshold = threshold;
            return matchFragmentTask;
        }

        /// <summary>
        /// Creates a new match fragment task as a child of current task.
        /// </summary>
        /// <param name="sequences">The pose sequence that the match fragment task receives as input.</param>
        /// <returns>Reference to the newly created match fragment task.</returns>
        /// <seealso cref="MatchFragmentTask"/>
        public TaskReference MatchFragment(Identifier<PoseSequence> sequences)
        {
            return MatchFragment(sequences, Identifier<SamplingTimeData>.Invalid, Identifier<TrajectoryData>.Invalid);
        }

        /// <summary>
        /// Creates a new trajectory heuristic task as a child of current task.
        /// </summary>
        /// <param name="candidate">The candidate time index that the trajectory heuristic task receives as input.</param>
        /// <param name="trajectory">The trajectory that the trajectory heuristic task receives as input.</param>
        /// <returns>Reference to the newly created trajectory heuristic task.</returns>
        /// <seealso cref="TrajectoryHeuristicTask"/>
        public TaskReference TrajectoryHeuristic(Identifier<TimeIndexData> candidate, Identifier<TrajectoryData> trajectory)
        {
            Identifier<TimeIndexData> timeIndex = TimeIndex();
            return CreateChildTask(TrajectoryHeuristicTask.Create(ref Synthesizer, candidate, trajectory, timeIndex));
        }

        /// <summary>
        /// Creates a new current pose task as a child of current task.
        /// </summary>
        /// <returns>Reference to the newly created current pose task.</returns>
        /// <seealso cref="CurrentPoseTask"/>
        public TaskReference CurrentPose()
        {
            Identifier<SamplingTimeData> samplingTime = SamplingTime();
            return CreateChildTask(CurrentPoseTask.Create(ref Synthesizer, samplingTime));
        }

        /// <summary>
        /// Creates a new play at time task as a child of current task.
        /// </summary>
        /// <param name="samplingTime">The sampling time that the push task receives as input.</param>
        /// <returns>Reference to the newly created play at time task.</returns>
        /// <seealso cref="PlayAtTimeTask"/>
        public TaskReference PlayAtTime(Identifier<TimeIndexData> samplingTime)
        {
            return CreateChildTask(PlayAtTimeTask.Create(ref Synthesizer, samplingTime));
        }

        /// <summary>
        /// Creates a new play at time task as a child of current task.
        /// </summary>
        /// <param name="samplingTime">The sampling time that the push task receives as input.</param>
        /// <returns>Reference to the newly created play at time task.</returns>
        /// <seealso cref="PlayAtTimeTask"/>
        public TaskReference PlayAtTime(TimeIndexData samplingTime)
        {
            return PlayAtTime(TimeIndex(samplingTime));
        }

        /// <summary>
        /// Creates a new play at time task as a child of current task.
        /// </summary>
        /// <remarks>
        /// This is a convenience method that implicitely creates a match fragment task
        /// for the query result that is passed as argument. Since in this case the
        /// match fragment task will not receive any constraints it will simply select the
        /// first pose of the query result. This method allows to push the first
        /// pose of a query result to the motion synthesizer.
        /// </remarks>
        /// <param name="result">Pose sequence that will be passed to the implicitely created match fragment operation.</param>
        /// <returns>Reference to the newly created play at time task.</returns>
        /// <seealso cref="PlayAtTimeTask"/>
        public TaskReference PlayFirstSequence(QueryResultData result)
        {
            Identifier<PoseSequence> sequences = QueryResult(result);
            TaskReference matchFragmentTask = MatchFragment(sequences);
            return PlayAtTime(matchFragmentTask.GetAs<MatchFragmentTask>().closestMatch);
        }

        /// <summary>
        /// Creates a new push task as a child of current task.
        /// </summary>
        /// <remarks>
        /// This is a convenience method that implicitely creates a match fragment task
        /// for the query result that is passed as argument. The match fragment task will
        /// be set up such that it selects the animation pose that is most similar to
        /// the current pose of the synthesizer.
        /// </remarks>
        /// <param name="result">Pose sequence that will be passed to the implicitely created match fragment operation.</param>
        /// <param name="threshold">Threshold that will be passed to the implicitely created match fragment operation.</param>
        /// <returns>Reference to the newly created play at time task.</returns>
        /// <seealso cref="PlayAtTimeTask"/>
        public TaskReference MatchPose(QueryResultData result, float threshold)
        {
            Identifier<SamplingTimeData> samplingTime = CurrentPose().GetAs<CurrentPoseTask>().samplingTime;
            Identifier<PoseSequence> sequences = QueryResult(result);
            TaskReference matchFragmentTask = MatchFragment(sequences, samplingTime, Identifier<TrajectoryData>.Invalid, threshold);

            return PlayAtTime(matchFragmentTask.GetAs<MatchFragmentTask>().closestMatch);
        }

        /// <summary>
        /// Creates a new push task as a child of current task.
        /// </summary>
        /// <remarks>
        /// This is a convenience method that implicitely creates a match fragment task
        /// for the query result that is passed as argument. The match fragment task will
        /// be set up such that it selects the animation pose that is most similar to
        /// the current pose of the synthesizer and the desired trajectory that is passed as argument.
        /// </remarks>
        /// <param name="result">Pose sequence that will be passed to the implicitely created match fragment operation.</param>
        /// <param name="trajectory">Trajectory that will be passed to the implicitely created match fragment operation.</param>
        /// <returns>Reference to the newly created play at time task.</returns>
        /// <seealso cref="PushTask"/>
        public TaskReference MatchPoseAndTrajectory(QueryResultData result, Identifier<TrajectoryData> trajectory)
        {
            Identifier<SamplingTimeData> samplingTime = CurrentPose().GetAs<CurrentPoseTask>().samplingTime;
            Identifier<PoseSequence> sequences = QueryResult(result);
            TaskReference matchFragmentTask = MatchFragment(sequences, samplingTime, trajectory);
            TaskReference trajectoryHeuristicTask = TrajectoryHeuristic(matchFragmentTask.GetAs<MatchFragmentTask>().closestMatch, trajectory);

            return PlayAtTime(trajectoryHeuristicTask.GetAs<TrajectoryHeuristicTask>().timeIndex);
        }

        /// <summary>
        /// Creates a new timer task as a child of current task.
        /// </summary>
        /// <param name="timeInSeconds">Time in seconds that will be used as initial value for the timer task.</param>
        /// <returns>Reference to the newly created timer task.</returns>
        public TaskReference Timer(float timeInSeconds = 0.0f)
        {
            return CreateChildTask(TimerTask.Create(ref Synthesizer, timeInSeconds));
        }
    }
}
