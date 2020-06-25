using TimeIndexData = Unity.Kinematica.TimeIndex;
using SamplingTimeData = Unity.Kinematica.SamplingTime;
using QueryResultData = Unity.Kinematica.QueryResult;
using TrajectoryData = Unity.Kinematica.Trajectory;
using Unity.Mathematics;
using UnityEngine.Assertions;

namespace Unity.Kinematica
{
    [System.Obsolete("TaskExtensions has been removed to get rid of duplicates, please use TaskReference instead")]
    public static class TaskExtensions
    {
        [System.Obsolete("Please use similar function from TaskReference.")]
        public static ref ActionTask Action(this MotionSynthesizer synthesizer)
        {
            return ref synthesizer.Action(synthesizer.Root);
        }

        [System.Obsolete("Please use similar function from TaskReference.")]
        public static ref ActionTask Action(this MotionSynthesizer synthesizer, MemoryIdentifier parent)
        {
            var identifier =
                synthesizer.Allocate(
                    ActionTask.Create(ref synthesizer), parent);

            ref var task = ref synthesizer.GetRef<ActionTask>(identifier).Ref;

            task.self = Identifier<ActionTask>.Create(identifier);

            return ref task;
        }

        [System.Obsolete("Please use similar function from TaskReference.")]
        public static ref ParallelTask Parallel(this MotionSynthesizer synthesizer)
        {
            return ref synthesizer.Parallel(synthesizer.Root);
        }

        [System.Obsolete("Please use similar function from TaskReference.")]
        public static ref ParallelTask Parallel(this MotionSynthesizer synthesizer, MemoryIdentifier parent)
        {
            var identifier =
                synthesizer.Allocate(
                    ParallelTask.Create(ref synthesizer), parent);

            ref var task = ref synthesizer.GetRef<ParallelTask>(identifier).Ref;

            task.self = Identifier<ParallelTask>.Create(identifier);

            return ref task;
        }

        [System.Obsolete("Please use similar function from TaskReference.")]
        public static ref SequenceTask Sequence(this MotionSynthesizer synthesizer, bool loop = false, bool resetWhenNotExecuted = true)
        {
            return ref synthesizer.Sequence(synthesizer.Root, loop, resetWhenNotExecuted);
        }

        [System.Obsolete("Please use similar function from TaskReference.")]
        public static ref SequenceTask Sequence(this MotionSynthesizer synthesizer, MemoryIdentifier parent, bool loop = false, bool resetWhenNotExecuted = true)
        {
            var identifier =
                synthesizer.Allocate(
                    SequenceTask.Create(ref synthesizer, loop, resetWhenNotExecuted), parent);

            ref var task = ref synthesizer.GetRef<SequenceTask>(identifier).Ref;

            task.self = Identifier<SequenceTask>.Create(identifier);

            return ref task;
        }

        [System.Obsolete("Please use similar function from TaskReference.")]
        public static ref SelectorTask Selector(this MotionSynthesizer synthesizer)
        {
            return ref synthesizer.Selector(synthesizer.Root);
        }

        [System.Obsolete("Please use similar function from TaskReference.")]
        public static ref SelectorTask Selector(this MotionSynthesizer synthesizer, MemoryIdentifier parent)
        {
            var identifier =
                synthesizer.Allocate(
                    SelectorTask.Create(ref synthesizer), parent);

            ref var task = ref synthesizer.GetRef<SelectorTask>(identifier).Ref;

            task.self = Identifier<SelectorTask>.Create(identifier);

            return ref task;
        }

        [System.Obsolete("Please use similar function from TaskReference.")]
        public static ref ConditionTask Condition(this MotionSynthesizer synthesizer)
        {
            return ref synthesizer.Condition(synthesizer.Root);
        }

        [System.Obsolete("Please use similar function from TaskReference.")]
        public static ref ConditionTask Condition(this MotionSynthesizer synthesizer, MemoryIdentifier parent)
        {
            var identifier =
                synthesizer.Allocate(
                    ConditionTask.Create(ref synthesizer), parent);

            ref var task = ref synthesizer.GetRef<ConditionTask>(identifier).Ref;

            task.self = Identifier<ConditionTask>.Create(identifier);

            return ref task;
        }

        [System.Obsolete("Please use similar function from TaskReference.")]
        public static Identifier<TimeIndexData> TimeIndex(this ActionTask action)
        {
            ref var synthesizer = ref action.synthesizer.Ref;

            var identifier =
                synthesizer.Allocate(
                    TimeIndexData.Invalid, action.self);

            return Identifier<TimeIndexData>.Create(identifier);
        }

        [System.Obsolete("Please use similar function from TaskReference.")]
        public static Identifier<TimeIndexData> TimeIndex(this ActionTask action, TimeIndexData timeIndex)
        {
            ref var synthesizer = ref action.synthesizer.Ref;

            var identifier =
                synthesizer.Allocate(
                    timeIndex, action.self);

            return Identifier<TimeIndexData>.Create(identifier);
        }

        [System.Obsolete("Please use similar function from TaskReference.")]
        public static Identifier<SamplingTimeData> SamplingTime(this ActionTask action)
        {
            ref var synthesizer = ref action.synthesizer.Ref;

            var identifier =
                synthesizer.Allocate(
                    SamplingTimeData.Invalid, action.self);

            return Identifier<SamplingTimeData>.Create(identifier);
        }

        [System.Obsolete("Please use similar function from TaskReference.")]
        public static Identifier<SamplingTimeData> SamplingTime(this ActionTask action, SamplingTimeData samplingTime)
        {
            ref var synthesizer = ref action.synthesizer.Ref;

            var identifier =
                synthesizer.Allocate(
                    samplingTime, action.self);

            return Identifier<SamplingTimeData>.Create(identifier);
        }

        [System.Obsolete("Please use similar function from TaskReference.")]
        public static Identifier<PoseSequence> QueryResult(this ActionTask action, QueryResultData result)
        {
            ref var synthesizer = ref action.synthesizer.Ref;

            var identifier =
                synthesizer.Allocate(
                    result.sequences, action.self);

            return Identifier<PoseSequence>.Create(identifier);
        }

        [System.Obsolete("Please use similar function from TaskReference.")]
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

        [System.Obsolete("Please use GetChildByType() function from TaskReference.")]
        public static ref T GetByType<T>(this ActionTask action) where T : struct
        {
            ref var synthesizer = ref action.synthesizer.Ref;

            var result = synthesizer.GetChildByType<T>(action.self);

            Assert.IsTrue(result.IsValid);

            return ref result.Ref;
        }

        [System.Obsolete("Please use similar function from TaskReference.")]
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

        [System.Obsolete("Please use similar function from TaskReference.")]
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

        [System.Obsolete("Please use MatchFragment() from TaskReference.")]
        public static ref MatchFragmentTask Reduce(this ActionTask action, Identifier<PoseSequence> sequences, Identifier<SamplingTimeData> samplingTime, Identifier<TrajectoryData> trajectory, float threshold = 0.0f)
        {
            ref var synthesizer = ref action.synthesizer.Ref;

            var closestMatch = action.TimeIndex();

            var identifier = synthesizer.Allocate(
                MatchFragmentTask.Create(
                    ref synthesizer, sequences,
                    samplingTime, trajectory,
                    closestMatch), action.self);

            ref var task =
                ref synthesizer.GetRef<MatchFragmentTask>(
                    identifier).Ref;

            task.threshold = threshold;

            return ref task;
        }

        [System.Obsolete("Please use MatchFragment() from TaskReference.")]
        public static ref MatchFragmentTask Reduce(this ActionTask action, Identifier<PoseSequence> sequences)
        {
            return ref action.Reduce(sequences, Identifier<SamplingTimeData>.Invalid, Identifier<TrajectoryData>.Invalid);
        }

        [System.Obsolete("Please use similar function from TaskReference.")]
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

        [System.Obsolete("Please use similar function from TaskReference.")]
        public static ref CurrentPoseTask CurrentPose(this ActionTask action)
        {
            ref var synthesizer = ref action.synthesizer.Ref;

            var samplingTime = action.SamplingTime();

            var identifier = synthesizer.Allocate(
                CurrentPoseTask.Create(
                    ref synthesizer, samplingTime), action.self);

            return ref synthesizer.GetRef<CurrentPoseTask>(identifier).Ref;
        }

        [System.Obsolete("Please use PlayAtTime() from TaskReference.")]
        public static ref PlayAtTimeTask Push(this ActionTask action, Identifier<TimeIndexData> samplingTime)
        {
            ref var synthesizer = ref action.synthesizer.Ref;

            var identifier =
                synthesizer.Allocate(
                    PlayAtTimeTask.Create(
                        ref synthesizer, samplingTime), action.self);

            return ref synthesizer.GetRef<PlayAtTimeTask>(identifier).Ref;
        }

        [System.Obsolete("Please use PlayAtTime() from TaskReference.")]
        public static ref PlayAtTimeTask Push(this ActionTask action, TimeIndexData samplingTime)
        {
            return ref action.Push(action.TimeIndex(samplingTime));
        }

        [System.Obsolete("Please use PlayFirstSequence() from TaskReference.")]
        public static ref PlayAtTimeTask Push(this ActionTask action, QueryResultData result)
        {
            return ref action.Push(action.Reduce(action.QueryResult(result)).closestMatch);
        }

        [System.Obsolete("Please use MatchPose() from TaskReference..MatchPose()")]
        public static ref PlayAtTimeTask PushConstrained(this ActionTask action, QueryResultData result, float threshold)
        {
            ref var synthesizer = ref action.synthesizer.Ref;

            var samplingTime =
                action.CurrentPose().samplingTime;

            return ref action.Push(
                action.Reduce(action.QueryResult(result),
                    samplingTime, Identifier<TrajectoryData>.Invalid,
                    threshold).closestMatch);
        }

        [System.Obsolete("Please use MatchPoseAndTrajectory() from TaskReference.")]
        public static ref PlayAtTimeTask PushConstrained(this ActionTask action, QueryResultData result, Identifier<TrajectoryData> trajectory)
        {
            ref var synthesizer = ref action.synthesizer.Ref;

            var samplingTime =
                action.CurrentPose().samplingTime;

            return ref action.Push(
                action.TrajectoryHeuristic(action.Reduce(
                    action.QueryResult(result), samplingTime,
                    trajectory).closestMatch, trajectory).timeIndex);
        }

        [System.Obsolete("Please use similar function from TaskReference.")]
        public static MemoryIdentifier Timer(this ActionTask action, float timeInSeconds = 0.0f)
        {
            ref var synthesizer = ref action.synthesizer.Ref;

            return synthesizer.Allocate(
                TimerTask.Create(ref synthesizer,
                    timeInSeconds), action.self);
        }
    }
}
