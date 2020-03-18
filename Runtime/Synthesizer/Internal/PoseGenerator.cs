using Unity.Collections;
using Unity.Mathematics;
using Unity.SnapshotDebugger;

using UnityEngine.Assertions;

namespace Unity.Kinematica
{
    internal struct PoseGenerator
    {
        struct ScalarTransition
        {
            public float t1;

            public float x0;
            public float v0;
            public float a0;

            float A;
            float B;
            float C;

            public const float epsilon = 0.0001f;

            public float X(float t)
            {
                float t2 = t * t;
                float t3 = t2 * t;
                float t4 = t3 * t;
                float t5 = t4 * t;

                float xt = A * t5 + B * t4 + C * t3 + (a0 / 2.0f) * t2 + v0 * t + x0;

                return xt;
            }

            public static ScalarTransition Create(float x0, float v0, float t1)
            {
                if (t1 <= epsilon)
                {
                    return new ScalarTransition();
                }

                v0 = math.min(0.0f, v0);

                if (v0 < -epsilon)
                {
                    t1 = math.min(t1, -5.0f * x0 / v0);
                }

                float a0 = (-8.0f * v0 * t1 - 20.0f * x0) / Missing.squared(t1);

                float t12 = t1 * t1;
                float t13 = t12 * t1;
                float t14 = t13 * t1;
                float t15 = t14 * t1;

                float A = -(a0 * t12 + 6.0f * v0 * t1 + 12.0f * x0) / (2.0f * t15);
                float B = (3.0f * a0 * t12 + 16.0f * v0 * t1 + 30.0f * x0) / (2.0f * t14);
                float C = -(3.0f * a0 * t12 + 12.0f * v0 * t1 + 20.0f * x0) / (2.0f * t13);

                return new ScalarTransition
                {
                    t1 = t1,
                    x0 = x0,
                    v0 = v0,
                    a0 = a0,
                    A = A,
                    B = B,
                    C = C
                };
            }
        }

        struct LinearTransition
        {
            public float t;

            public float3 axis;

            public ScalarTransition transition;

            public static LinearTransition Create(float3 displacement, float3 velocity, float durationInSeconds, float inverseDeltaTime)
            {
                var axis = math.normalizesafe(displacement);

                var x0 = math.length(displacement);

                var x1 = math.length(math.dot(axis, velocity));

                var v0 = (x0 - x1) * inverseDeltaTime;

                v0 = math.min(v0, 0.0f);

                var transition = ScalarTransition.Create(x0, v0, durationInSeconds);

                return new LinearTransition
                {
                    axis = axis,
                    transition = transition
                };
            }

            public void Update(float deltaTime)
            {
                t = math.min(t + deltaTime, transition.t1);
            }

            public float3 Evaluate()
            {
                return axis * transition.X(t);
            }
        }

        struct AngularTransition
        {
            public float t;

            public float3 axis;

            public ScalarTransition transition;

            public static AngularTransition Create(quaternion displacement, quaternion velocity, float durationInSeconds, float inverseDeltaTime)
            {
                float angle;

                var axis = Missing.axisAngle(displacement, out angle);

                if (angle < 0.0f)
                {
                    angle = -angle;
                    axis = -axis;
                    velocity = Missing.negate(velocity);
                }

                angle %= 2.0f * math.PI;

                var x0 = angle;

                var x1 = 2.0f * math.atan(
                    math.dot(velocity.value.xyz, axis) /
                    velocity.value.w);

                var v0 = (x0 - x1) * inverseDeltaTime;

                v0 = math.min(v0, 0.0f);

                var transition = ScalarTransition.Create(x0, v0, durationInSeconds);

                return new AngularTransition
                {
                    axis = axis,
                    transition = transition
                };
            }

            public void Update(float deltaTime)
            {
                t = math.min(t + deltaTime, transition.t1);
            }

            public quaternion Evaluate()
            {
                var q = quaternion.AxisAngle(axis, transition.X(t));

                return q;
            }
        }

        struct TransformTransition
        {
            public LinearTransition linear;
            public AngularTransition angular;

            public const float epsilon = 0.0001f;

            public static TransformTransition Create(AffineTransform deltaTransform, AffineTransform velocityTransform, float durationInSeconds, float deltaTime)
            {
                float inverseDeltaTime = 0.0f;

                if (deltaTime > epsilon)
                {
                    inverseDeltaTime = math.rcp(deltaTime);
                }

                var linear =
                    LinearTransition.Create(
                        deltaTransform.t, velocityTransform.t,
                        durationInSeconds, inverseDeltaTime);

                var angular =
                    AngularTransition.Create(
                        deltaTransform.q, velocityTransform.q,
                        durationInSeconds, inverseDeltaTime);

                return new TransformTransition
                {
                    linear = linear,
                    angular = angular
                };
            }

            public static TransformTransition Identity => Create(AffineTransform.identity, AffineTransform.identity, 0.0f, 0.0f);

            public void Update(float deltaTime)
            {
                linear.Update(deltaTime);
                angular.Update(deltaTime);
            }

            public AffineTransform Evaluate()
            {
                return new AffineTransform(
                    linear.Evaluate(), angular.Evaluate());
            }
        }

        internal void Construct(ref MemoryBlock memoryBlock, ref Binary binary, float blendDuration)
        {
            this.blendDuration = blendDuration;

            m_binary = MemoryRef<Binary>.Create(ref binary);

            var numJoints = binary.numJoints;

            previousPose.Construct(ref memoryBlock, numJoints);
            currentPose.Construct(ref memoryBlock, numJoints);

            transitions = memoryBlock.CreateArray(numJoints, TransformTransition.Identity);

            for (int i = 0; i < numJoints; ++i)
            {
                currentPose.transforms[i] =
                    binary.animationRig.bindPose[i].localTransform;
            }
        }

        public void TriggerTransition()
        {
            triggerTransition = true;
        }

        public void Update(AffineTransform worldRootTransform, SamplingTime samplingTime, float deltaTime)
        {
            Assert.IsTrue(samplingTime.IsValid);

            //
            // Sample target pose into temporary buffer
            //

            var targetPose = SamplePoseAt(samplingTime);

            //
            // Trigger transition is requested
            //

            TriggerTransition(ref targetPose.Ref, previousDeltaTime);

            ref var binary = ref Binary;

            int numJoints = binary.numJoints;

            for (int i = 0; i < numJoints; ++i)
            {
                transitions[i].Update(deltaTime);
            }

            //
            // Store the final output pose in the pose history buffer
            //

            previousPose.CopyFrom(ref currentPose);

            previousDeltaTime = deltaTime;

            //
            // currentPose = targetPose + deltaPose
            //

            for (int i = 0; i < numJoints; ++i)
            {
                currentPose.transforms[i] =
                    targetPose.Ref.transforms[i] *
                    transitions[i].Evaluate();
            }

            currentPose.transforms[0] = worldRootTransform;

            targetPose.Dispose();
        }

        internal TransformBuffer LocalSpaceTransformBuffer => currentPose;

        public void WriteToStream(Buffer buffer)
        {
            buffer.Write(previousDeltaTime);
            buffer.Write(triggerTransition);

            transitions.WriteToStream(buffer);
            previousPose.WriteToStream(buffer);
            currentPose.WriteToStream(buffer);
        }

        public void ReadFromStream(Buffer buffer)
        {
            previousDeltaTime = buffer.ReadSingle();
            triggerTransition = buffer.ReadBoolean();

            transitions.ReadFromStream(buffer);
            previousPose.ReadFromStream(buffer);
            currentPose.ReadFromStream(buffer);
        }

        internal static MemoryRequirements GetMemoryRequirements(ref Binary binary, float blendDuration)
        {
            var numJoints = binary.numJoints;

            var memoryRequirements = TransformBuffer.GetMemoryRequirements(numJoints);

            memoryRequirements += TransformBuffer.GetMemoryRequirements(numJoints);

            memoryRequirements += MemoryRequirements.Of<TransformTransition>() * numJoints;

            return memoryRequirements;
        }

        void TriggerTransition(ref TransformBuffer targetPose, float deltaTime)
        {
            if (triggerTransition)
            {
                triggerTransition = false;

                ref var binary = ref Binary;

                int numJoints = binary.numJoints;

                for (int i = 0; i < numJoints; ++i)
                {
                    var targetTransform = targetPose.transforms[i];
                    var currentTransform = currentPose.transforms[i];
                    var previousTransform = previousPose.transforms[i];

                    if (math.dot(previousTransform.q, currentTransform.q) < 0.0f)
                    {
                        previousTransform.q = Missing.negate(previousTransform.q);
                    }

                    if (math.dot(targetTransform.q, currentTransform.q) < 0.0f)
                    {
                        targetTransform.q = Missing.negate(targetTransform.q);
                    }

                    var deltaTransform =
                        targetTransform.inverseTimes(
                            currentTransform);

                    var velocityTransform =
                        targetTransform.inverseTimes(
                            previousTransform);

                    transitions[i] =
                        TransformTransition.Create(
                            deltaTransform, velocityTransform,
                            blendDuration, deltaTime);
                }
            }
        }

        MemoryHeader<TransformBuffer> SamplePoseAt(SamplingTime samplingTime)
        {
            ref var binary = ref Binary;

            int numJoints = binary.numJoints;

            MemoryHeader<TransformBuffer> transformBuffer =
                TransformBuffer.Create(numJoints, Allocator.Temp);

            binary.SamplePoseAt(samplingTime, ref transformBuffer.Ref);

            return transformBuffer;
        }

        ref Binary Binary => ref m_binary.Ref;

        [ReadOnly]
        float blendDuration;

        [ReadOnly]
        MemoryRef<Binary> m_binary;

        BlittableBool triggerTransition;

        float previousDeltaTime;

        TransformBuffer previousPose;

        TransformBuffer currentPose;

        MemoryArray<TransformTransition> transitions;
    }
}
