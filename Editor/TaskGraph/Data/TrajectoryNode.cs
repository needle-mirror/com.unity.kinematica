using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UIElements;

using UnityEditor.UIElements;

using Unity.Mathematics;

using TrajectoryFragment = Unity.Kinematica.Binary.TrajectoryFragment;

namespace Unity.Kinematica.Editor
{
    [GraphNode(typeof(Trajectory))]
    internal class TrajectoryNode : GraphNode
    {
        Label label = new Label();

        bool showFragment;

        Binary.TrajectoryFragmentDisplay.Options options;

        public TrajectoryNode()
        {
            options =
                Binary.TrajectoryFragmentDisplay.CreateOptions();
        }

        public override void OnSelected(ref MotionSynthesizer synthesizer)
        {
            ref var binary = ref synthesizer.Binary;

            ref var memoryChunk = ref owner.memoryChunk.Ref;

            var trajectory = memoryChunk.GetArray<AffineTransform>(identifier);

            var anchorTransform = synthesizer.WorldRootTransform;

            if (showFragment)
            {
                var fragment =
                    binary.CreateTrajectoryFragment(
                        0, trajectory);

                DisplayDetails(ref binary, anchorTransform, fragment);

                fragment.Dispose();
            }
            else
            {
                DebugExtensions.DebugDrawTrajectory(anchorTransform,
                    trajectory,
                    binary.SampleRate,
                    options.baseColor,
                    options.baseColor);
            }

            var timeHorizon = binary.TimeHorizon;

            var sampleTimeInSeconds = timeHorizon * options.timeOffset;

            var rootTransform = GetRootTransform(ref binary, sampleTimeInSeconds);

            AffineTransform transform =
                anchorTransform * rootTransform;

            if (options.showForward)
            {
                var forward =
                    anchorTransform.transformDirection(
                        Missing.zaxis(rootTransform.q));

                DisplayAxis(transform.t, forward * 0.5f, options.forwardColor);
            }

            float linearSpeedInMetersPerSecond = 0.0f;

            if (options.showVelocity)
            {
                var velocity =
                    anchorTransform.transformDirection(
                        GetRootVelocity(ref binary, sampleTimeInSeconds));

                linearSpeedInMetersPerSecond = math.length(velocity);

                velocity = math.normalizesafe(velocity);

                DisplayAxis(transform.t, velocity * 0.5f, options.velocityColor);
            }

            label.text = $"Linear speed: {linearSpeedInMetersPerSecond}";
        }

        AffineTransform GetRootTransform(ref Binary binary, float sampleTimeInSeconds)
        {
            ref var memoryChunk = ref owner.memoryChunk.Ref;

            var trajectory = memoryChunk.GetArray<AffineTransform>(identifier);

            var timeHorizon = binary.TimeHorizon;

            int trajectoryLength = trajectory.Length;

            int halfTrajectoryLength = trajectoryLength / 2;

            var fraction = sampleTimeInSeconds / timeHorizon;

            int sampleKeyFrame =
                halfTrajectoryLength +
                Missing.truncToInt(
                    fraction * halfTrajectoryLength);

            var numFramesMinusOne = trajectoryLength - 1;

            sampleKeyFrame = math.clamp(sampleKeyFrame, 0, numFramesMinusOne);

            if (sampleKeyFrame >= numFramesMinusOne)
            {
                return trajectory[numFramesMinusOne];
            }

            var sampleRate = binary.SampleRate;

            float fractionalKeyFrame = sampleTimeInSeconds * sampleRate;
            float theta = math.saturate(fractionalKeyFrame - sampleKeyFrame);

            if (theta <= Missing.epsilon)
            {
                return trajectory[sampleKeyFrame];
            }

            AffineTransform t0 = trajectory[sampleKeyFrame + 0];
            AffineTransform t1 = trajectory[sampleKeyFrame + 1];

            return Missing.lerp(t0, t1, theta);
        }

        float3 GetRootVelocity(ref Binary binary, float sampleTimeInSeconds)
        {
            var timeHorizon = binary.TimeHorizon;

            var sampleRate = binary.SampleRate;

            var deltaTime = math.rcp(sampleRate);

            var futureSampleTimeInSeconds =
                math.min(sampleTimeInSeconds + deltaTime, timeHorizon);

            sampleTimeInSeconds =
                math.max(futureSampleTimeInSeconds - deltaTime, -timeHorizon);

            var t1 = GetRootTransform(ref binary, futureSampleTimeInSeconds).t;
            var t0 = GetRootTransform(ref binary, sampleTimeInSeconds).t;

            return (t1 - t0) / deltaTime;
        }

        public void DisplayDetails(ref Binary binary, AffineTransform anchorTransform, TrajectoryFragment fragment)
        {
            var timeHorizon = binary.TimeHorizon;

            ref var metric = ref binary.GetMetric(fragment.metricIndex);

            var numTrajectorySamples = metric.numTrajectorySamples;

            var advanceInSeconds = timeHorizon * math.rcp(numTrajectorySamples);

            var numStepsAcrossTimeHorizon = numTrajectorySamples * 2;

            var previousRootPosition =
                GetRootTransform(ref binary, -timeHorizon).t;

            int readIndex = 0;

            for (int i = 0; i < numStepsAcrossTimeHorizon; ++i)
            {
                var displacement = fragment[readIndex++];

                var rootPosition = previousRootPosition + displacement;

                DebugDraw.DrawLine(
                    anchorTransform.transform(previousRootPosition),
                    anchorTransform.transform(rootPosition),
                    options.detailsColor);

                previousRootPosition = rootPosition;
            }

            var negativeOffsetTime = -timeHorizon;
            var positiveOffsetTime = timeHorizon;

            void DisplayVelocity(AffineTransform rootTransform, float3 velocity)
            {
                velocity = math.normalizesafe(velocity);

                DisplayAxis(rootTransform.t, velocity * 0.5f, options.velocityColor, 0.5f);
            }

            void DisplayForward(AffineTransform rootTransform, float3 forward)
            {
                DisplayAxis(rootTransform.t, forward * 0.5f, options.forwardColor, 0.5f);
            }

            for (int i = 0; i < numTrajectorySamples; ++i)
            {
                AffineTransform pastRelativeRootTransform =
                    GetRootTransform(ref binary, negativeOffsetTime);

                AffineTransform pastRootTransform =
                    anchorTransform * pastRelativeRootTransform;

                AffineTransform futureRelativeRootTransform =
                    GetRootTransform(ref binary, positiveOffsetTime);

                AffineTransform futureRootTransform =
                    anchorTransform * futureRelativeRootTransform;

                var pastRootVelocity =
                    anchorTransform.transformDirection(fragment[readIndex++]);

                var futureRootVelocity =
                    anchorTransform.transformDirection(fragment[readIndex++]);

                DisplayVelocity(pastRootTransform, pastRootVelocity);

                DisplayVelocity(futureRootTransform, futureRootVelocity);

                var pastRootForward =
                    anchorTransform.transformDirection(fragment[readIndex++]);

                var futureRootForward =
                    anchorTransform.transformDirection(fragment[readIndex++]);

                DisplayForward(pastRootTransform, pastRootForward);

                DisplayForward(futureRootTransform, futureRootForward);

                negativeOffsetTime += advanceInSeconds;
                positiveOffsetTime -= advanceInSeconds;
            }

            Assert.IsTrue(readIndex == fragment.array.Length);
        }

        static void DisplayAxis(float3 startPosition, float3 axis, Color color, float scale = 1.0f)
        {
            float height = 0.1f * scale;
            float width = height * 0.5f;

            var endPosition = startPosition + axis * scale;

            var length = math.length(axis) * scale;

            if (length >= height)
            {
                var normalizedDirection = axis * math.rcp(length);

                var rotation =
                    Missing.forRotation(Missing.up, normalizedDirection);

                DebugDraw.DrawLine(startPosition, endPosition, color);

                DebugDraw.DrawCone(
                    endPosition - normalizedDirection * scale * height,
                    rotation, width, height, color);
            }
        }

        public override void DrawDefaultInspector()
        {
            HandleShowFragment();
            HandleTimeOffset();
            HandleTrajectoryColor();
            HandleVelocityColor();
            HandleDisplacementColor();

            controlsContainer.Add(label);
        }

        public void HandleShowFragment()
        {
            var toggleButton = new Toggle();

            toggleButton.text = "Show Fragment";
            toggleButton.value = showFragment;

            controlsContainer.Add(toggleButton);

            toggleButton.RegisterValueChangedCallback((e) =>
            {
                showFragment = e.newValue;
            });
        }

        public void HandleTimeOffset()
        {
            var sliderButton = new Slider();

            sliderButton.label = "Time Offset";
            sliderButton.value = 0.0f;
            sliderButton.lowValue = -1.0f;
            sliderButton.highValue = 1.0f;

            controlsContainer.Add(sliderButton);

            sliderButton.RegisterValueChangedCallback((e) =>
            {
                options.timeOffset = e.newValue;
            });
        }

        public void HandleTrajectoryColor()
        {
            var colorField = new ColorField();

            colorField.label = "Trajectory Color";
            colorField.value = options.baseColor;

            controlsContainer.Add(colorField);

            colorField.RegisterValueChangedCallback((e) =>
            {
                options.baseColor = e.newValue;
            });
        }

        public void HandleVelocityColor()
        {
            var colorField = new ColorField();

            colorField.label = "Velocity Color";
            colorField.value = options.velocityColor;

            controlsContainer.Add(colorField);

            colorField.RegisterValueChangedCallback((e) =>
            {
                options.velocityColor = e.newValue;
            });
        }

        public void HandleDisplacementColor()
        {
            var colorField = new ColorField();

            colorField.label = "Displacement Color";
            colorField.value = options.forwardColor;

            controlsContainer.Add(colorField);

            colorField.RegisterValueChangedCallback((e) =>
            {
                options.forwardColor = e.newValue;
            });
        }
    }
}
