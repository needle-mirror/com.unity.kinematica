using System;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine.Assertions;

namespace Unity.Kinematica
{
    [Data("TrajectoryPath", "#2A3756"), BurstCompile]
    public struct NavigationTask : Task, GenericTask<NavigationTask>
    {
        public Identifier<NavigationTask> self { get; set; }

        public MemoryRef<MotionSynthesizer> synthesizer;

        public bool IsPathValid => pathCurvesIdentifier.IsValid;

        public int NextControlPoint => navigationPath.NextControlPoint;

        public bool GoalReached => navigationPath.GoalReached;

        [Output("Trajectory")]
        public Identifier<Trajectory> trajectory;

        MemoryIdentifier    pathCurvesIdentifier;
        NavigationPath      navigationPath;

        public unsafe Result Execute()
        {
            ClearTrajectory();

            if (!IsPathValid)
            {
                return Result.Failure;
            }

            if (GoalReached)
            {
                return Result.Success;
            }

            ref var synthesizer = ref this.synthesizer.Ref;

            MemoryArray<AffineTransform> trajectoryArray = synthesizer.GetArray<AffineTransform>(trajectory);

            Spline pathSpline = GetPathSpline();
            navigationPath.UpdateAgentTransform(synthesizer.WorldRootTransform, ref pathSpline);
            if (!GoalReached)
            {
                navigationPath.GenerateTrajectory(ref synthesizer, ref pathSpline, ref trajectoryArray);
            }

            return Result.Success;
        }

        [BurstCompile]
        public static Result ExecuteSelf(ref TaskPointer self)
        {
            return self.Cast<NavigationTask>().Execute();
        }

        public NavigationTask(ref MotionSynthesizer synthesizer, Identifier<Trajectory> trajectory)
        {
            self = Identifier<NavigationTask>.Invalid;

            this.synthesizer = synthesizer.self;

            pathCurvesIdentifier = MemoryIdentifier.Invalid;
            navigationPath = new NavigationPath();

            this.trajectory = trajectory;
        }

        public static NavigationTask Create(ref MotionSynthesizer synthesizer, Identifier<Trajectory> trajectory)
        {
            return new NavigationTask(ref synthesizer, trajectory);
        }

        public static implicit operator Identifier<NavigationTask>(NavigationTask task)
        {
            return task.self;
        }

        /// <summary>
        /// Move agent toward first control point, and then move them along path made of <c>controlPoints</c> until last control point
        /// </summary>
        public void FollowPath(float3[] controlPoints, NavigationParams navParams)
        {
            if (controlPoints == null || controlPoints.Length == 0)
            {
                throw new ArgumentException("NavigationTask FollowPath() function expects a non empty controlPoints array.", "controlPoints");
            }

            ref MotionSynthesizer synthesizerRef = ref synthesizer.Ref;
            MemoryIdentifier navTaskIdentifier = self;

            // allocate path spline
            MemoryIdentifier pathCurvesId = synthesizerRef.AllocateArray<HermitCurve>(controlPoints.Length, self);

            // previous allocation may have reallocated task memory and invalidated 'this'. Therefore we must retrieve
            // navigation task from synthesizer to get pointer to the appropriate data
            ref NavigationTask navTask = ref synthesizerRef.GetRef<NavigationTask>(navTaskIdentifier).Ref;

            navTask.FollowPath(pathCurvesId, controlPoints, ref navParams);
        }

        public void DrawPath()
        {
            if (IsPathValid)
            {
                Spline pathSpline = GetPathSpline();
                navigationPath.DrawPath(ref pathSpline);
            }
        }

        void FollowPath(MemoryIdentifier pathCurvesId, float3[] controlPoints, ref NavigationParams navParams)
        {
            if (pathCurvesIdentifier.IsValid)
            {
                synthesizer.Ref.MarkForDelete(pathCurvesIdentifier);
            }

            pathCurvesIdentifier = pathCurvesId;

            Spline pathSpline = GetPathSpline();
            AffineTransform startTransform = synthesizer.Ref.WorldRootTransform;
            pathSpline.Initialize(startTransform.t, startTransform.Forward, controlPoints, float3.zero, navParams.pathCurvature);

            navigationPath = new NavigationPath(controlPoints.Length, ref navParams);
        }

        Spline GetPathSpline()
        {
            Assert.IsTrue(IsPathValid);

            return new Spline()
            {
                segments = synthesizer.Ref.GetArray<HermitCurve>(pathCurvesIdentifier)
            };
        }

        void ClearTrajectory()
        {
            ref var synthesizer = ref this.synthesizer.Ref;

            MemoryArray<AffineTransform> trajectoryArray = synthesizer.GetArray<AffineTransform>(trajectory);
            synthesizer.trajectory.Array.CopyTo(ref trajectoryArray);

            int halfTrajectoryLength = trajectoryArray.Length / 2;
            for (int i = halfTrajectoryLength; i < trajectoryArray.Length; ++i)
            {
                trajectoryArray[i] = AffineTransform.identity;
            }
        }
    }
}
