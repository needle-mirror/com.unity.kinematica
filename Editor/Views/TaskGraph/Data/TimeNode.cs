using UnityEngine.UIElements;
using UnityEditor.UIElements;
using Unity.SnapshotDebugger;

namespace Unity.Kinematica.Editor
{
    internal abstract class TimeNode : GraphNode
    {
        Label label = new Label();
        Label timeIndexLabel = new Label();

        Binary.PoseFragmentDisplay.Options poseOptions;
        Binary.TrajectoryFragmentDisplay.Options trajectoryOptions;

        public TimeNode()
        {
            poseOptions =
                Binary.PoseFragmentDisplay.CreateOptions();

            trajectoryOptions =
                Binary.TrajectoryFragmentDisplay.CreateOptions();
        }

        BuilderWindow FindBuilderWindow()
        {
            var windows =
                UnityEngine.Resources.FindObjectsOfTypeAll(
                    typeof(BuilderWindow)) as BuilderWindow[];

            if (windows.Length == 0)
            {
                return null;
            }

            return windows[0];
        }

        void HighlightTimeIndex(ref MotionSynthesizer synthesizer, TimeIndex timeIndex)
        {
            var builderWindow = FindBuilderWindow();

            if (builderWindow != null)
            {
                builderWindow.HighlightTimeIndex(
                    ref synthesizer, timeIndex, true);
            }
        }

        TimeIndex RetrieveDebugTimeIndex(ref MotionSynthesizer synthesizer)
        {
            var builderWindow = FindBuilderWindow();

            if (builderWindow != null)
            {
                return
                    builderWindow.RetrieveDebugTimeIndex(
                    ref synthesizer.Binary);
            }

            return TimeIndex.Invalid;
        }

        public override void OnSelected(ref MotionSynthesizer synthesizer)
        {
            ref var binary = ref synthesizer.Binary;

            var samplingTime = GetSamplingTime();

            HighlightTimeIndex(
                ref synthesizer, samplingTime.timeIndex);

            var overrideTime =
                RetrieveDebugTimeIndex(ref synthesizer);

            if (overrideTime.IsValid)
            {
                samplingTime =
                    SamplingTime.Create(overrideTime);
            }

            if (samplingTime.IsValid)
            {
                var worldRootTransform = synthesizer.WorldRootTransform;

                var poseFragment =
                    binary.ReconstructPoseFragment(
                        samplingTime);

                if (poseFragment.IsValid)
                {
                    Binary.PoseFragmentDisplay.Create(
                        poseFragment).Display(
                            ref binary, worldRootTransform,
                            poseOptions);

                    poseFragment.Dispose();
                }

                var trajectoryFragment =
                    binary.ReconstructTrajectoryFragment(
                        samplingTime);

                if (trajectoryFragment.IsValid)
                {
                    var linearSpeedInMetersPerSecond =
                        Binary.TrajectoryFragmentDisplay.Create(
                            trajectoryFragment).Display(
                                ref binary, worldRootTransform,
                                trajectoryOptions);

                    trajectoryFragment.Dispose();

                    label.text = $"Linear speed: {linearSpeedInMetersPerSecond}";
                }
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
                    SetSamplingTime(ref synthesizer,
                        SamplingTime.Create(overrideTime));
                }
                SamplingTime st = GetSamplingTime();
                if (st.IsValid)
                {
                    var clipName = "";
                    int frame = -1;
                    GetClipAndFrameFromSamplingTime(ref synthesizer, GetSamplingTime(), ref clipName, ref frame);
                    timeIndexLabel.text = $"{clipName} frame {frame}";
                }
                else
                {
                    timeIndexLabel.text = $"Not Valid";
                }
            }
            else
            {
                timeIndexLabel.text = "";
            }
        }

        public abstract SamplingTime GetSamplingTime();

        public abstract void SetSamplingTime(ref MotionSynthesizer synthesizer, SamplingTime samplingTime);

        public override void DrawDefaultInspector()
        {
            HandleShowTimeSpan();
            HandleShowPoseDetails();
            HandleShowTrajectoryDetails();
            HandleTimeOffset();
            HandlePoseColor();
            HandleTrajectoryColor();
            HandleDetailsColor();
            HandleVelocityColor();
            HandleDisplacementColor();

            controlsContainer.Add(label);
            controlsContainer.Add(timeIndexLabel);
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

        public void HandlePoseColor()
        {
            var colorField = new ColorField();

            colorField.label = "Pose Color";
            colorField.value = poseOptions.poseColor;

            controlsContainer.Add(colorField);

            colorField.RegisterValueChangedCallback((e) =>
            {
                poseOptions.poseColor = e.newValue;
            });
        }

        public void HandleTrajectoryColor()
        {
            var colorField = new ColorField();

            colorField.label = "Trajectory Color";
            colorField.value = trajectoryOptions.baseColor;

            controlsContainer.Add(colorField);

            colorField.RegisterValueChangedCallback((e) =>
            {
                trajectoryOptions.baseColor = e.newValue;
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

        void GetClipAndFrameFromSamplingTime(ref MotionSynthesizer synthesizer, SamplingTime time, ref string clipName, ref int frame)
        {
            AnimationSampleTimeIndex animSampleTime = synthesizer.Binary.GetAnimationSampleTimeIndex(time.timeIndex);
            clipName = animSampleTime.clipName;
            frame = animSampleTime.animFrameIndex;
        }
    }
}
