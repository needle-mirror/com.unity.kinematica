using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;

namespace Unity.Kinematica
{
    internal struct NavigationPath
    {
        public NavigationPath(int numControlPoints, ref NavigationParams navParams)
        {
            nextControlPoint = 0;
            this.numControlPoints = numControlPoints;
            this.navParams = navParams;
        }

        public bool GoalReached => nextControlPoint >= numControlPoints;

        public int NextControlPoint => nextControlPoint;

        public NavigationParams NavParams => navParams;

        public void UpdateAgentTransform(AffineTransform agentTransform, ref Spline pathSpline)
        {
            if (GoalReached)
            {
                return;
            }

            float3 nextControlPointPos = pathSpline.segments[nextControlPoint].OutPosition;
            float controlPointRadius = nextControlPoint >= numControlPoints - 1 ? navParams.finalControlPointRadius : navParams.intermediateControlPointRadius;
            if (math.distancesq(agentTransform.t, nextControlPointPos) <= controlPointRadius * controlPointRadius)
            {
                ++nextControlPoint;
                if (GoalReached)
                {
                    return;
                }
            }

            // use agent transform as first control point of the spline (starting from nextControlPoint, segments before are discarded)
            ref HermitCurve segment = ref pathSpline.segments[nextControlPoint];

            segment = HermitCurve.Create(
                agentTransform.t,
                agentTransform.Forward,
                segment.OutPosition,
                segment.OutTangent,
                navParams.pathCurvature);
        }

        public SplinePoint EvaluatePointAtDistance(float distance, ref Spline pathSpline)
        {
            return pathSpline.EvaluatePointAtDistance(distance, nextControlPoint);
        }

        public void GenerateTrajectory(ref MotionSynthesizer synthesizer, ref Spline pathSpline, ref MemoryArray<AffineTransform> trajectory)
        {
            Assert.IsTrue(trajectory.Length > 0);
            if (trajectory.Length == 0)
            {
                return;
            }

            AffineTransform rootTransform = synthesizer.WorldRootTransform;

            float maxSpeedAtCorner = navParams.desiredSpeed;
            if (nextControlPoint < pathSpline.segments.Length - 1)
            {
                float3 curSegmentDir = pathSpline.segments[nextControlPoint].SegmentDirection;
                float3 nextSegmentDir = pathSpline.segments[nextControlPoint + 1].SegmentDirection;

                float alignment = math.max(math.dot(curSegmentDir, nextSegmentDir), 0.0f);
                maxSpeedAtCorner = math.lerp(navParams.maxSpeedAtRightAngle, navParams.desiredSpeed, alignment);
            }

            int halfTrajectoryLength = trajectory.Length / 2;

            float deltaTime = synthesizer.Binary.TimeHorizon / halfTrajectoryLength;
            float distance = 0.0f;

            float speed = math.length(synthesizer.CurrentVelocity);
            float remainingDistOnSpline = pathSpline.ComputeCurveLength(nextControlPoint);
            float remainingDistOnSegment = pathSpline.segments[nextControlPoint].CurveLength;

            for (int index = halfTrajectoryLength; index < trajectory.Length; ++index)
            {
                if (remainingDistOnSpline > 0.0f)
                {
                    // acceleration to reach desired speed
                    float acceleration = math.clamp((navParams.desiredSpeed - speed) / deltaTime,
                        -navParams.maximumDeceleration,
                        navParams.maximumAcceleration);

                    // decelerate if needed to reach maxSpeedAtCorner
                    float brakingDistance = 0.0f;
                    if (remainingDistOnSegment > 0.0f && speed > maxSpeedAtCorner)
                    {
                        brakingDistance = NavigationParams.ComputeDistanceToReachSpeed(speed, maxSpeedAtCorner, -navParams.maximumDeceleration);
                        if (remainingDistOnSegment <= brakingDistance)
                        {
                            acceleration = math.min(acceleration, NavigationParams.ComputeAccelerationToReachSpeed(speed, maxSpeedAtCorner, remainingDistOnSegment));
                        }
                    }

                    // decelerate if needed to stop when last control point is reached
                    brakingDistance = NavigationParams.ComputeDistanceToReachSpeed(speed, 0.0f, -navParams.maximumDeceleration);
                    if (remainingDistOnSpline <= brakingDistance)
                    {
                        acceleration = math.min(acceleration, NavigationParams.ComputeAccelerationToReachSpeed(speed, 0.0f, remainingDistOnSpline));
                    }

                    speed += acceleration * deltaTime;
                }
                else
                {
                    speed = 0.0f;
                }

                float moveDist = speed * deltaTime;
                remainingDistOnSegment -= moveDist;
                remainingDistOnSpline -= moveDist;
                distance += moveDist;

                AffineTransform point = EvaluatePointAtDistance(distance, ref pathSpline);
                trajectory[index] = rootTransform.inverseTimes(point);
            }
        }

        public void DrawPath(ref Spline pathSpline)
        {
            if (GoalReached)
            {
                return;
            }

            float pathLength = pathSpline.ComputeCurveLength(nextControlPoint);
            if (pathLength <= 0.0f)
            {
                return;
            }

            float stepDist = 0.2f;
            int lines = (int)math.floor(pathLength / stepDist);

            float3 position = pathSpline.EvaluatePointAtDistance(0.0f, nextControlPoint).position;
            for (int i = 0; i < lines; ++i)
            {
                float distance = ((i + 1) / (float)lines) * pathLength;
                float3 nextPosition = pathSpline.EvaluatePointAtDistance(distance, nextControlPoint).position;

                DebugDraw.DrawLine(position, nextPosition, Color.white);

                position = nextPosition;
            }

            for (int i = 0; i < pathSpline.segments.Length; ++i)
            {
                float radius = (i == pathSpline.segments.Length - 1) ? navParams.finalControlPointRadius : navParams.intermediateControlPointRadius;
                DebugDraw.DrawSphere(pathSpline.segments[i].OutPosition, quaternion.identity, radius, (nextControlPoint <= i) ? Color.red : Color.white);
            }
        }

        int nextControlPoint;
        int numControlPoints;
        NavigationParams navParams;
    }
}
