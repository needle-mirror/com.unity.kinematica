using UnityEngine;
using UnityEngine.UIElements;

using UnityEditor.UIElements;

using Unity.Mathematics;

using CodeBookIndex = Unity.Kinematica.Binary.CodeBookIndex;

namespace Unity.Kinematica.Editor
{
    [GraphNode(typeof(TrajectoryHeuristicTask))]
    internal class TrajectoryHeuristicNode : GraphNode
    {
        Label currentLabel = new Label();
        Label candidateLabel = new Label();

        Color candidateColor = new Color(0.0f, 1.0f, 0.5f, 0.75f);
        Color currentColor = new Color(0.25f, 0.5f, 1.0f, 0.75f);
        Color desiredColor = new Color(0.75f, 0.6f, 0.0f, 0.75f);

        public override void OnSelected(ref MotionSynthesizer synthesizer)
        {
            ref var binary = ref synthesizer.Binary;

            ref var memoryChunk = ref owner.memoryChunk.Ref;

            ref var task = ref memoryChunk.GetRef<TrajectoryHeuristicTask>(identifier).Ref;

            var candidate = memoryChunk.GetRef<TimeIndex>(task.candidate).Ref;

            var worldRootTransform = synthesizer.WorldRootTransform;

            binary.DebugDrawTrajectory(
                worldRootTransform, SamplingTime.Create(candidate),
                binary.TimeHorizon, candidateColor);

            binary.DebugDrawTrajectory(
                worldRootTransform, synthesizer.Time,
                binary.TimeHorizon, currentColor);

            var trajectory =
                memoryChunk.GetArray<AffineTransform>(
                    task.desiredTrajectory);

            DebugExtensions.DebugDrawTrajectory(worldRootTransform,
                trajectory,
                binary.SampleRate,
                desiredColor,
                desiredColor);

            var codeBookIndex = GetCodeBookIndex(ref binary, candidate);

            if (codeBookIndex.IsValid)
            {
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

                currentLabel.text = string.Format(
                    "Current {0:0.000}", current2Desired);

                candidateLabel.text = string.Format(
                    "Candidate {0:0.000}", candidate2Desired);

                desiredFragment.Dispose();
                currentFragment.Dispose();
                candidateFragment.Dispose();
            }
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

        public override void DrawDefaultInspector()
        {
            HandleCandidateColor();
            HandleCurrentColor();
            HandleDesiredColor();

            controlsContainer.Add(currentLabel);
            controlsContainer.Add(candidateLabel);
        }

        public void HandleCandidateColor()
        {
            var colorField = new ColorField();

            colorField.label = "Candidate Color";
            colorField.value = candidateColor;

            controlsContainer.Add(colorField);

            colorField.RegisterValueChangedCallback((e) =>
            {
                candidateColor = e.newValue;
            });
        }

        public void HandleCurrentColor()
        {
            var colorField = new ColorField();

            colorField.label = "Current Color";
            colorField.value = currentColor;

            controlsContainer.Add(colorField);

            colorField.RegisterValueChangedCallback((e) =>
            {
                currentColor = e.newValue;
            });
        }

        public void HandleDesiredColor()
        {
            var colorField = new ColorField();

            colorField.label = "Desired Color";
            colorField.value = desiredColor;

            controlsContainer.Add(colorField);

            colorField.RegisterValueChangedCallback((e) =>
            {
                desiredColor = e.newValue;
            });
        }
    }
}
