using UnityEngine.UIElements;
using UnityEditor.UIElements;
using Unity.SnapshotDebugger;
using UnityEngine;
using Unity.Mathematics;
using UnityEngine.Assertions;
using System.Collections.Generic;

namespace Unity.Kinematica.Editor
{
    internal abstract class TimeNode : GraphNode
    {
        internal class FragmentDisplayInfo
        {
            internal static FragmentDisplayInfo Create(string name, Color color, float3 offset)
            {
                FragmentDisplayInfo fragment = new FragmentDisplayInfo()
                {
                    name = name,
                    samplingTime = SamplingTime.Invalid,
                    label = new Label(),
                    colorField = new ColorField($"{name} Color"),
                    offset = offset,
                    color = color
                };

                fragment.colorField.RegisterValueChangedCallback((e) =>
                {
                    fragment.color = e.newValue;
                });

                return fragment;
            }

            public string name;
            public ColorField colorField;
            public SamplingTime samplingTime;
            public Label label;
            public float3 offset;

            public Color color
            {
                get
                {
                    return drawColor;
                }
                set
                {
                    drawColor = value;
                    label.style.color = Color.Lerp(Color.white, value, 0.5f);
                    colorField.value = value;
                }
            }

            Color drawColor;
        }

        Label label = new Label();
        Label timeIndexLabel = new Label();

        Binary.PoseFragmentDisplay.Options poseOptions;
        Binary.TrajectoryFragmentDisplay.Options trajectoryOptions;

        List<FragmentDisplayInfo> displayedFragments;

        protected FragmentDisplayInfo currentFragment;
        protected FragmentDisplayInfo userFragment;

        float offsetDistance;
        float offsetAngle;

        public TimeNode()
        {
            poseOptions =
                Binary.PoseFragmentDisplay.CreateOptions();

            trajectoryOptions =
                Binary.TrajectoryFragmentDisplay.CreateOptions();

            displayedFragments = new List<FragmentDisplayInfo>();

            currentFragment = AddFragmentDisplay("Current", poseOptions.poseColor, float3.zero);
            userFragment = AddFragmentDisplay("User", Color.red, -Missing.right);

            offsetDistance = 1.0f;
            offsetAngle = 0.0f;
        }

        protected void HighlightTimeIndex(ref MotionSynthesizer synthesizer, TimeIndex timeIndex)
        {
            var builderWindow = Utility.FindBuilderWindow();

            if (builderWindow != null)
            {
                builderWindow.HighlightTimeIndex(
                    ref synthesizer, timeIndex, true);
            }
        }

        protected TimeIndex RetrieveDebugTimeIndex(ref MotionSynthesizer synthesizer)
        {
            var builderWindow = Utility.FindBuilderWindow();

            if (builderWindow != null)
            {
                return
                    builderWindow.RetrieveDebugTimeIndex(
                    ref synthesizer.Binary);
            }

            return TimeIndex.Invalid;
        }

        AffineTransform ComputeFragmentDrawOffset(float3 localOffset)
        {
            quaternion offsetRotation = quaternion.AxisAngle(Missing.up, offsetAngle);
            float3 offset = math.mul(offsetRotation, localOffset) * offsetDistance;
            return AffineTransform.Create(offset, quaternion.identity);
        }

        protected FragmentDisplayInfo AddFragmentDisplay(string name, Color color, float3 offset)
        {
            FragmentDisplayInfo fragment = FragmentDisplayInfo.Create(name, color, offset);
            displayedFragments.Add(fragment);
            return fragment;
        }

        protected void DrawFragment(ref MotionSynthesizer synthesizer, ref FragmentDisplayInfo fragment)
        {
            fragment.label.text = "";

            if (!fragment.samplingTime.IsValid)
            {
                return;
            }

            fragment.label.text = GetClipAndFrameInfoText(ref synthesizer, fragment.samplingTime);

            ref Binary binary = ref synthesizer.Binary;
            AffineTransform worldRootTransform = synthesizer.WorldRootTransform * ComputeFragmentDrawOffset(fragment.offset);

            Binary.PoseFragment poseFragment = binary.ReconstructPoseFragment(fragment.samplingTime);
            if (poseFragment.IsValid)
            {
                Binary.PoseFragmentDisplay.Options drawOptions = poseOptions;
                drawOptions.poseColor = fragment.color;

                Binary.PoseFragmentDisplay.Create(poseFragment).Display(
                    ref binary, worldRootTransform,
                    drawOptions);

                poseFragment.Dispose();
            }

            Binary.TrajectoryFragment trajectoryFragment = binary.ReconstructTrajectoryFragment(fragment.samplingTime);
            if (trajectoryFragment.IsValid)
            {
                Binary.TrajectoryFragmentDisplay.Options drawOptions = trajectoryOptions;
                drawOptions.baseColor = fragment.color;

                float linearSpeedInMetersPerSecond =
                    Binary.TrajectoryFragmentDisplay.Create(
                        trajectoryFragment).Display(
                            ref binary, worldRootTransform,
                            trajectoryOptions);

                trajectoryFragment.Dispose();

                fragment.label.text = $"{fragment.label.text}, speed: {linearSpeedInMetersPerSecond}";
            }
        }

        public override void OnSelected(ref MotionSynthesizer synthesizer)
        {
            ref var binary = ref synthesizer.Binary;

            currentFragment.samplingTime = GetSamplingTime();

            DrawFragment(ref synthesizer, ref currentFragment);

            TimeIndex overrideTime = RetrieveDebugTimeIndex(ref synthesizer);

            if (overrideTime.IsValid)
            {
                userFragment.samplingTime = SamplingTime.Create(overrideTime);
                DrawFragment(ref synthesizer, ref userFragment);
            }
        }

        public override void OnPreLateUpdate(ref MotionSynthesizer synthesizer)
        {
            if (Debugger.instance.rewind)
            {
                var overrideTime =
                    RetrieveDebugTimeIndex(ref synthesizer);

                if (overrideTime.IsValid)
                {
                    userFragment.samplingTime = SamplingTime.Create(overrideTime);
                }
            }
        }

        public abstract SamplingTime GetSamplingTime();

        protected virtual void HandleOptions()
        {
            HandleShowTimeSpan();
            HandleShowPoseDetails();
            HandleShowTrajectoryDetails();
            HandleTimeOffset();
            HandleDistanceOffset();
            HandleAngleOffset();
            HandleDetailsColor();
            HandleVelocityColor();
            HandleDisplacementColor();
        }

        public override void DrawDefaultInspector()
        {
            HandleOptions();

            foreach (FragmentDisplayInfo fragment in displayedFragments)
            {
                controlsContainer.Add(fragment.colorField);
            }

            foreach (FragmentDisplayInfo fragment in displayedFragments)
            {
                controlsContainer.Add(fragment.label);
            }
        }

        public void HandleShowTimeSpan()
        {
            var toggleButton = new Toggle();

            toggleButton.text = "Show Pose TimeSpan";
            toggleButton.value = poseOptions.showTimeSpan;

            controlsContainer.Add(toggleButton);

            toggleButton.RegisterValueChangedCallback((e) =>
            {
                poseOptions.showTimeSpan = e.newValue;
            });
        }

        public void HandleShowPoseDetails()
        {
            var toggleButton = new Toggle();

            toggleButton.text = "Show Pose Details";
            toggleButton.value = poseOptions.showDetails;

            controlsContainer.Add(toggleButton);

            toggleButton.RegisterValueChangedCallback((e) =>
            {
                poseOptions.showDetails = e.newValue;
            });
        }

        public void HandleShowTrajectoryDetails()
        {
            var toggleButton = new Toggle();

            toggleButton.text = "Show Trajectory Details";
            toggleButton.value = trajectoryOptions.showDetails;

            controlsContainer.Add(toggleButton);

            toggleButton.RegisterValueChangedCallback((e) =>
            {
                trajectoryOptions.showDetails = e.newValue;
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
                poseOptions.timeOffset = e.newValue;
                trajectoryOptions.timeOffset = e.newValue;
            });
        }

        public void HandleDistanceOffset()
        {
            var sliderButton = new Slider();

            sliderButton.label = "Distance Offset";
            sliderButton.value = offsetDistance;
            sliderButton.lowValue = 0.0f;
            sliderButton.highValue = 5.0f;

            controlsContainer.Add(sliderButton);

            sliderButton.RegisterValueChangedCallback((e) =>
            {
                offsetDistance = e.newValue;
            });
        }

        public void HandleAngleOffset()
        {
            var sliderButton = new Slider();

            sliderButton.label = "Angle Offset";
            sliderButton.value = math.degrees(offsetAngle);
            sliderButton.lowValue = 0.0f;
            sliderButton.highValue = 360.0f;

            controlsContainer.Add(sliderButton);

            sliderButton.RegisterValueChangedCallback((e) =>
            {
                offsetAngle = math.radians(e.newValue);
            });
        }

        public void HandlePoseColor()
        {
            var colorField = new ColorField();

            colorField.label = "Pose Color";
            colorField.value = currentFragment.color;

            controlsContainer.Add(colorField);

            colorField.RegisterValueChangedCallback((e) =>
            {
                currentFragment.color = e.newValue;
            });
        }

        public void HandleOverrideColor()
        {
            var colorField = new ColorField();

            colorField.label = "Override color";
            colorField.value = userFragment.color;

            controlsContainer.Add(colorField);

            colorField.RegisterValueChangedCallback((e) =>
            {
                userFragment.color = e.newValue;
            });
        }

        public void HandleDetailsColor()
        {
            var colorField = new ColorField();

            colorField.label = "Details Color";
            colorField.value = poseOptions.detailsColor;

            controlsContainer.Add(colorField);

            colorField.RegisterValueChangedCallback((e) =>
            {
                poseOptions.detailsColor = e.newValue;
                trajectoryOptions.detailsColor = e.newValue;
            });
        }

        public void HandleVelocityColor()
        {
            var colorField = new ColorField();

            colorField.label = "Velocity Color";
            colorField.value = trajectoryOptions.velocityColor;

            controlsContainer.Add(colorField);

            colorField.RegisterValueChangedCallback((e) =>
            {
                trajectoryOptions.velocityColor = e.newValue;
            });
        }

        public void HandleDisplacementColor()
        {
            var colorField = new ColorField();

            colorField.label = "Displacement Color";
            colorField.value = trajectoryOptions.forwardColor;

            controlsContainer.Add(colorField);

            colorField.RegisterValueChangedCallback((e) =>
            {
                trajectoryOptions.forwardColor = e.newValue;
            });
        }

        string GetClipAndFrameInfoText(ref MotionSynthesizer synthesizer, SamplingTime time)
        {
            Assert.IsTrue(time.IsValid);
            AnimationSampleTimeIndex animSampleTime = synthesizer.Binary.GetAnimationSampleTimeIndex(time.timeIndex);
            return $"{animSampleTime.clipName} frame {animSampleTime.animFrameIndex}";
        }
    }
}
