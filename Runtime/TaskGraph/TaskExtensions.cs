using TimeIndexData = Unity.Kinematica.TimeIndex;
using SamplingTimeData = Unity.Kinematica.SamplingTime;
using QueryResultData = Unity.Kinematica.QueryResult;
using TrajectoryData = Unity.Kinematica.Trajectory;
using Unity.Mathematics;
using UnityEngine.Assertions;

namespace Unity.Kinematica
{
    /// <summary>
    /// Static class that contains various extension methods that allow
    /// tasks to be created. Tasks in turn offer similar methods that
    /// allow for a concise notion when creation arbitrary complex task graphs.
    /// </summary>
    public static class TaskExtensions
    {
        /// <summary>
        /// Creates a new action task as a child of the root task.
        /// </summary>
        /// <returns>Reference to the newly created action task.</returns>
        /// <seealso cref="RootTask"/>
        public static ref ActionTask Action(this MotionSynthesizer synthesizer)
        {
            return ref synthesizer.Action(synthesizer.Root);
        }

        /// <summary>
        /// Creates a new action task as a child of the parent task passed as argument.
        /// </summary>
        /// <param name="parent">Identifier of the parent task.</param>
        /// <returns>Reference to the newly created action task.</returns>
        public static ref ActionTask Action(this MotionSynthesizer synthesizer, MemoryIdentifier parent)
        {
            var identifier =
                synthesizer.Allocate(
                    ActionTask.Create(ref synthesizer), parent);

            ref var task = ref synthesizer.GetRef<ActionTask>(identifier).Ref;

            task.self = Identifier<ActionTask>.Create(identifier);

            return ref task;
        }

        /// <summary>
        /// Creates a new parallel task as a child of the root task.
        /// </summary>
        /// <returns>Reference to the newly created parallel task.</returns>
        /// <seealso cref="RootTask"/>
        public static ref ParallelTask Parallel(this MotionSynthesizer synthesizer)
        {
            return ref synthesizer.Parallel(synthesizer.Root);
        }

        /// <summary>
        /// Creates a new parallel task as a child of the parent task passed as argument.
        /// </summary>
        /// <param name="parent">Identifier of the parent task.</param>
        /// <returns>Reference to the newly created parallel task.</returns>
        public static ref ParallelTask Parallel(this MotionSynthesizer synthesizer, MemoryIdentifier parent)
        {
            var identifier =
                synthesizer.Allocate(
                    ParallelTask.Create(ref synthesizer), parent);

            ref var task = ref synthesizer.GetRef<ParallelTask>(identifier).Ref;

            task.self = Identifier<ParallelTask>.Create(identifier);

            return ref task;
        }

        /// <summary>
        /// Creates a new sequence task as a child of the root task.
        /// </summary>
        /// <param name="loop">If false, once the sequence has finished executing all its children, it will do nothing and just return success. If true, sequence will reexecute all its children tasks indefinitely.</param>
        /// <param name="resetWhenNotExecuted">If true, and if the sequence isn't executed during one task graph pass, next time the sequence will be executed again, it will restart execution from its first child.</param>
        /// <returns>Reference to the newly created sequence task.</returns>
        /// <seealso cref="RootTask"/>
        public static ref SequenceTask Sequence(this MotionSynthesizer synthesizer, bool loop = false, bool resetWhenNotExecuted = true)
        {
            return ref synthesizer.Sequence(synthesizer.Root, loop, resetWhenNotExecuted);
        }

        /// <summary>
        /// Creates a new sequence task as a child of the parent task passed as argument.
        /// </summary>
        /// <param name="parent">Identifier of the parent task.</param>
        /// <param name="loop">If false, once the sequence has finished executing all its children, it will do nothing and just return success. If true, sequence will reexecute all its children tasks indefinitely.</param>
        /// <param name="resetWhenNotExecuted">If true, and if the sequence isn't executed during one task graph pass, next time the sequence will be executed again, it will restart execution from its first child.</param>
        /// <returns>Reference to the newly created sequence task.</returns>
        public static ref SequenceTask Sequence(this MotionSynthesizer synthesizer, MemoryIdentifier parent, bool loop = false, bool resetWhenNotExecuted = true)
        {
            var identifier =
                synthesizer.Allocate(
                    SequenceTask.Create(ref synthesizer, loop, resetWhenNotExecuted), parent);

            ref var task = ref synthesizer.GetRef<SequenceTask>(identifier).Ref;

            task.self = identifier;

            return ref task;
        }

        /// <summary>
        /// Creates a new selector task as a child of the root task.
        /// </summary>
        /// <returns>Reference to the newly created selector task.</returns>
        /// <seealso cref="RootTask"/>
        public static ref SelectorTask Selector(this MotionSynthesizer synthesizer)
        {
            return ref synthesizer.Selector(synthesizer.Root);
        }

        /// <summary>
        /// Creates a new selector task as a child of the parent task passed as argument.
        /// </summary>
        /// <param name="parent">Identifier of the parent task.</param>
        /// <returns>Reference to the newly created selector task.</returns>
        public static ref SelectorTask Selector(this MotionSynthesizer synthesizer, MemoryIdentifier parent)
        {
            var identifier =
                synthesizer.Allocate(
                    SelectorTask.Create(ref synthesizer), parent);

            ref var task = ref synthesizer.GetRef<SelectorTask>(identifier).Ref;

            task.self = Identifier<SelectorTask>.Create(identifier);

            return ref task;
        }

        /// <summary>
        /// Creates a new condition task as a child of the root task.
        /// </summary>
        /// <returns>Reference to the newly created condition task.</returns>
        /// <seealso cref="RootTask"/>
        public static ref ConditionTask Condition(this MotionSynthesizer synthesizer)
        {
            return ref synthesizer.Condition(synthesizer.Root);
        }

        /// <summary>
        /// Creates a new condition task as a child of the parent task passed as argument.
        /// </summary>
        /// <param name="parent">Identifier of the parent task.</param>
        /// <returns>Reference to the newly created condition task.</returns>
        public static ref ConditionTask Condition(this MotionSynthesizer synthesizer, MemoryIdentifier parent)
        {
            var identifier =
                synthesizer.Allocate(
                    ConditionTask.Create(ref synthesizer), parent);

            ref var task = ref synthesizer.GetRef<ConditionTask>(identifier).Ref;

            task.self = Identifier<ConditionTask>.Create(identifier);

            return ref task;
        }

        /// <summary>
        /// Creates a new time index.
        /// </summary>
        /// <remarks>
        /// The value of the newly created time index will be set to invalid.
        /// </remarks>
        /// <returns>Reference to the newly created time index.</returns>
        public static Identifier<TimeIndexData> TimeIndex(this ActionTask action)
        {
            ref var synthesizer = ref action.synthesizer.Ref;

            var identifier =
                synthesizer.Allocate(
                    TimeIndexData.Invalid, action.self);

            return Identifier<TimeIndexData>.Create(identifier);
        }

        /// <summary>
        /// Creates a new time index.
        /// </summary>
        /// <remarks>
        /// The value of the newly created time index will be set to the time index passed as argument.
        /// </remarks>
        /// <param name="timeIndex">Time index value that is to be used as initial value.</param>
        /// <returns>Reference to the newly created time index.</returns>
        public static Identifier<TimeIndexData> TimeIndex(this ActionTask action, TimeIndexData timeIndex)
        {
            ref var synthesizer = ref action.synthesizer.Ref;

            var identifier =
                synthesizer.Allocate(
                    timeIndex, action.self);

            return Identifier<TimeIndexData>.Create(identifier);
        }

        /// <summary>
        /// Creates a new sampling time.
        /// </summary>
        /// <remarks>
        /// The value of the newly created sampling time will be set to invalid.
        /// </remarks>
        /// <returns>Reference to the newly created sampling time.</returns>
        public static Identifier<SamplingTimeData> SamplingTime(this ActionTask action)
        {
            ref var synthesizer = ref action.synthesizer.Ref;

            var identifier =
                synthesizer.Allocate(
                    SamplingTimeData.Invalid, action.self);

            return Identifier<SamplingTimeData>.Create(identifier);
        }

        /// <summary>
        /// Creates a new sampling time.
        /// </summary>
        /// <remarks>
        /// The value of the newly created sampling time will be set to the sampling time passed as argument.
        /// </remarks>
        /// <param name="samplingTime">Sampling time value that is to be used as initial value.</param>
        /// <returns>Reference to the newly created sampling time.</returns>
        public static Identifier<SamplingTimeData> SamplingTime(this ActionTask action, SamplingTimeData samplingTime)
        {
            ref var synthesizer = ref action.synthesizer.Ref;

            var identifier =
                synthesizer.Allocate(
                    samplingTime, action.self);

            return Identifier<SamplingTimeData>.Create(identifier);
        }

        /// <summary>
        /// Creates a new pose sequence.
        /// </summary>
        /// <remarks>
        /// This method allows the creation of a pose sequence that can be fed into a reduce task.
        /// The pose sequence will be initialized based on the query result passed as argument.
        /// </remarks>
        /// <param name="result">Query result that is to be used as initial value of the pose sequence.</param>
        /// <returns>Reference to the newly created pose sequence.</returns>
        /// <seealso cref="Query"/>
        /// <seealso cref="ReduceTask"/>
        public static Identifier<PoseSequence> QueryResult(this ActionTask action, QueryResultData result)
        {
            ref var synthesizer = ref action.synthesizer.Ref;

            var identifier =
                synthesizer.Allocate(
                    result.sequences, action.self);

            return Identifier<PoseSequence>.Create(identifier);
        }

        /// <summary>
        /// Creates a new trajectory.
        /// </summary>
        /// <remarks>
        /// This method allows the creation of a trajectory that can be used for any task
        /// that requires a reference to a trajectory.
        /// </remarks>
        /// <returns>Reference to the newly created trajectory.</returns>
        public unsafe static Identifier<TrajectoryData> Trajectory(this ActionTask action)
        {
            ref var synthesizer = ref action.synthesizer.Ref;

            var source = synthesizer.TrajectoryArray;

            Assert.IsTrue(sizeof(TrajectoryData) == sizeof(AffineTransform));

            var trajectory = new MemoryArray<TrajectoryData>
            {
                ptr = source.ptr,
                length = source.length
            };

            var identifier =
                synthesizer.Allocate(
                    trajectory, action.self);

            return Identifier<TrajectoryData>.Create(identifier);
        }

        /// <summary>
        /// Retrieves a reference to a task or data based on the generic type.
        /// </summary>
        /// <returns>Reference to a task or data that matches the generic type.</returns>
        public static ref T GetByType<T>(this ActionTask action) where T : struct
        {
            ref var synthesizer = ref action.synthesizer.Ref;

            var result = synthesizer.GetByType<T>(action.self);

            Assert.IsTrue(result.IsValid);

            return ref result.Ref;
        }

        /// <summary>
        /// Creates a new trajectory prediction task as a child of an action.
        /// </summary>
        /// <returns>Reference to the newly created trajectory prediction task.</returns>
        public static ref TrajectoryPredictionTask TrajectoryPrediction(this ActionTask action)
        {
            ref var synthesizer = ref action.synthesizer.Ref;

            var trajectory = action.Trajectory();

            var identifier =
                synthesizer.Allocate(
                    TrajectoryPredictionTask.Create(
                        ref synthesizer, trajectory), action.self);

            ref var task =
                ref synthesizer.GetRef<TrajectoryPredictionTask>(
                    identifier).Ref;

            task.self = Identifier<TrajectoryPredictionTask>.Create(identifier);

            return ref task;
        }

        public static ref NavigationTask Navigation(this ActionTask action)
        {
            ref var synthesizer = ref action.synthesizer.Ref;

            var trajectory = action.Trajectory();

            var identifier =
                synthesizer.Allocate(
                    NavigationTask.Create(
                        ref synthesizer, trajectory), action.self);

            ref var task =
                ref synthesizer.GetRef<NavigationTask>(
                    identifier).Ref;

            task.self = Identifier<NavigationTask>.Create(identifier);

            return ref task;
        }

        /// <summary>
        /// Creates a new reduce task as a child of an action.
        /// </summary>
        /// <param name="sequences">The pose sequence that the reduce task receives as input.</param>
        /// <param name="samplingTime">Reference animation pose that the reduce task will receive as input.</param>
        /// <param name="trajectory">The desired future trajectory that the reduce task will receive as input.</param>
        /// <param name="threshold">The threshold that the reduce task will receive as input.</param>
        /// <returns>Reference to the newly created reduce task.</returns>
        /// <seealso cref="ReduceTask"/>
        public static ref ReduceTask Reduce(this ActionTask action, Identifier<PoseSequence> sequences, Identifier<SamplingTimeData> samplingTime, Identifier<TrajectoryData> trajectory, float threshold = 0.0f)
        {
            ref var synthesizer = ref action.synthesizer.Ref;

            var closestMatch = action.TimeIndex();

            var identifier = synthesizer.Allocate(
                ReduceTask.Create(
                    ref synthesizer, sequences,
                    samplingTime, trajectory,
                    closestMatch), action.self);

            ref var task =
                ref synthesizer.GetRef<ReduceTask>(
                    identifier).Ref;

            task.threshold = threshold;

            return ref task;
        }

        /// <summary>
        /// Creates a new reduce task as a child of an action.
        /// </summary>
        /// <param name="sequences">The pose sequence that the reduce task receives as input.</param>
        /// <returns>Reference to the newly created reduce task.</returns>
        /// <seealso cref="ReduceTask"/>
        public static ref ReduceTask Reduce(this ActionTask action, Identifier<PoseSequence> sequences)
        {
            return ref action.Reduce(sequences, Identifier<SamplingTimeData>.Invalid, Identifier<TrajectoryData>.Invalid);
        }

        /// <summary>
        /// Creates a new trajectory heuristic task as a child of an action.
        /// </summary>
        /// <param name="candidate">The candidate time index that the trajectory heuristic task receives as input.</param>
        /// <param name="trajectory">The trajectory that the trajectory heuristic task receives as input.</param>
        /// <returns>Reference to the newly created trajectory heuristic task.</returns>
        /// <seealso cref="TrajectoryHeuristicTask"/>
        public static ref TrajectoryHeuristicTask TrajectoryHeuristic(this ActionTask action, Identifier<TimeIndexData> candidate, Identifier<TrajectoryData> trajectory)
        {
            ref var synthesizer = ref action.synthesizer.Ref;

            var timeIndex = action.TimeIndex();

            var identifier = synthesizer.Allocate(
                TrajectoryHeuristicTask.Create(
                    ref synthesizer, candidate,
                    trajectory, timeIndex), action.self);

            ref var task =
                ref synthesizer.GetRef<TrajectoryHeuristicTask>(
                    identifier).Ref;

            task.self = Identifier<TrajectoryHeuristicTask>.Create(identifier);

            return ref synthesizer.GetRef<TrajectoryHeuristicTask>(identifier).Ref;
        }

        /// <summary>
        /// Creates a new current pose task as a child of an action.
        /// </summary>
        /// <returns>Reference to the newly created current pose task.</returns>
        /// <seealso cref="CurrentPoseTask"/>
        public static ref CurrentPoseTask CurrentPose(this ActionTask action)
        {
            ref var synthesizer = ref action.synthesizer.Ref;

            var samplingTime = action.SamplingTime();

            var identifier = synthesizer.Allocate(
                CurrentPoseTask.Create(
                    ref synthesizer, samplingTime), action.self);

            return ref synthesizer.GetRef<CurrentPoseTask>(identifier).Ref;
        }

        /// <summary>
        /// Creates a new push task as a child of an action.
        /// </summary>
        /// <param name="samplingTime">The sampling time that the push task receives as input.</param>
        /// <returns>Reference to the newly created push task.</returns>
        /// <seealso cref="PushTask"/>
        public static ref PushTask Push(this ActionTask action, Identifier<TimeIndexData> samplingTime)
        {
            ref var synthesizer = ref action.synthesizer.Ref;

            var identifier =
                synthesizer.Allocate(
                    PushTask.Create(
                        ref synthesizer, samplingTime), action.self);

            return ref synthesizer.GetRef<PushTask>(identifier).Ref;
        }

        /// <summary>
        /// Creates a new push task as a child of an action.
        /// </summary>
        /// <param name="samplingTime">The sampling time that the push task receives as input.</param>
        /// <returns>Reference to the newly created push task.</returns>
        /// <seealso cref="PushTask"/>
        public static ref PushTask Push(this ActionTask action, TimeIndexData samplingTime)
        {
            return ref action.Push(action.TimeIndex(samplingTime));
        }

        /// <summary>
        /// Creates a new push task as a child of an action.
        /// </summary>
        /// <remarks>
        /// This is a convenience method that implicitely creates a reduce task
        /// for the query result that is passed as argument. Since in this case the
        /// reduce task will not receive any constraints it will simply select the
        /// first pose of the query result. This method allows to push the first
        /// pose of a query result to the motion synthesizer.
        /// </remarks>
        /// <param name="result">Pose sequence that will be passed to the implicitely created reduce operation.</param>
        /// <returns>Reference to the newly created push task.</returns>
        /// <seealso cref="PushTask"/>
        public static ref PushTask Push(this ActionTask action, QueryResultData result)
        {
            return ref action.Push(action.Reduce(action.QueryResult(result)).closestMatch);
        }

        /// <summary>
        /// Creates a new push task as a child of an action.
        /// </summary>
        /// <remarks>
        /// This is a convenience method that implicitely creates a reduce task
        /// for the query result that is passed as argument. The reduce task will
        /// be set up such that it selects the animation pose that is most similar to
        /// the current pose of the synthesizer.
        /// </remarks>
        /// <param name="result">Pose sequence that will be passed to the implicitely created reduce operation.</param>
        /// <param name="threshold">Threshold that will be passed to the implicitely created reduce operation.</param>
        /// <returns>Reference to the newly created push task.</returns>
        /// <seealso cref="PushTask"/>
        public static ref PushTask PushConstrained(this ActionTask action, QueryResultData result, float threshold)
        {
            ref var synthesizer = ref action.synthesizer.Ref;

            var samplingTime =
                action.CurrentPose().samplingTime;

            return ref action.Push(
                action.Reduce(action.QueryResult(result),
                    samplingTime, Identifier<TrajectoryData>.Invalid,
                    threshold).closestMatch);
        }

        /// <summary>
        /// Creates a new push task as a child of an action.
        /// </summary>
        /// <remarks>
        /// This is a convenience method that implicitely creates a reduce task
        /// for the query result that is passed as argument. The reduce task will
        /// be set up such that it selects the animation pose that is most similar to
        /// the current pose of the synthesizer and the desired trajectory that is passed as argument.
        /// </remarks>
        /// <param name="result">Pose sequence that will be passed to the implicitely created reduce operation.</param>
        /// <param name="trajectory">Trajectory that will be passed to the implicitely created reduce operation.</param>
        /// <returns>Reference to the newly created push task.</returns>
        /// <seealso cref="PushTask"/>
        public static ref PushTask PushConstrained(this ActionTask action, QueryResultData result, Identifier<TrajectoryData> trajectory)
        {
            ref var synthesizer = ref action.synthesizer.Ref;

            var samplingTime =
                action.CurrentPose().samplingTime;

            return ref action.Push(
                action.TrajectoryHeuristic(action.Reduce(
                    action.QueryResult(result), samplingTime,
                    trajectory).closestMatch, trajectory).timeIndex);
        }

        /// <summary>
        /// Creates a new timer task as a child of an action.
        /// </summary>
        /// <param name="timeInSeconds">Time in seconds that will be used as initial value for the timer task.</param>
        /// <returns>Reference to the newly created timer task.</returns>
        public static MemoryIdentifier Timer(this ActionTask action, float timeInSeconds = 0.0f)
        {
            ref var synthesizer = ref action.synthesizer.Ref;

            return synthesizer.Allocate(
                TimerTask.Create(ref synthesizer,
                    timeInSeconds), action.self);
        }
    }
}
