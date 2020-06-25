using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;
using System;
using UnityEngine.UIElements;

namespace Unity.Kinematica.Editor
{
    [GraphNode(typeof(MatchFragmentTask))]
    internal class MatchFragmentNode : TimeNode
    {
        static readonly int MaxNumCandidates = 1000;

        FragmentDisplayInfo bestFragment;
        List<Tuple<TimeIndex, DeviationScore>> candidates;
        int highlightedCandidate;
        bool bHighlightCandidate;

        public MatchFragmentNode() : base()
        {
            bestFragment = AddFragmentDisplay("Best", Color.blue, Missing.right);
            candidates = null;
            highlightedCandidate = 0;
            bHighlightCandidate = false;
        }

        public override void OnSelected(ref MotionSynthesizer synthesizer)
        {
            base.OnSelected(ref synthesizer);

            ref var memoryChunk = ref owner.memoryChunk.Ref;
            ref MatchFragmentTask task = ref memoryChunk.GetRef<MatchFragmentTask>(identifier).Ref;

            // best candidate
            TimeIndex bestCandidateTime = memoryChunk.GetRef<TimeIndex>(task.closestMatch).Ref;
            bestFragment.samplingTime = SamplingTime.Create(bestCandidateTime);
            DrawFragment(ref synthesizer, ref bestFragment);

            if (task.samplingTime.IsValid)
            {
                DeviationTable deviationTable = ComputeDeviationTable(ref task);

                AddCostInformation(userFragment, ref synthesizer.Binary, ref deviationTable);
                AddCostInformation(bestFragment, ref synthesizer.Binary, ref deviationTable);

                candidates = deviationTable.SortCandidatesByDeviation(ref synthesizer.Binary);

                if (bHighlightCandidate && candidates.Count > 0)
                {
                    HighlightTimeIndex(ref synthesizer, candidates[math.min(highlightedCandidate, candidates.Count - 1)].Item1);
                    bHighlightCandidate = false;
                }
                else
                {
                    highlightedCandidate = FindHighlightedCandidate(ref synthesizer);
                }


                deviationTable.Dispose();
            }
        }

        public override SamplingTime GetSamplingTime()
        {
            ref var memoryChunk = ref owner.memoryChunk.Ref;

            ref MatchFragmentTask task = ref memoryChunk.GetRef<MatchFragmentTask>(identifier).Ref;

            return memoryChunk.GetRef<SamplingTime>(task.samplingTime).Ref;
        }

        protected override void HandleOptions()
        {
            base.HandleOptions();
            HandleCandidates();
        }

        int FindHighlightedCandidate(ref MotionSynthesizer synthesizer)
        {
            if (candidates == null)
            {
                return 0;
            }

            TimeIndex debugTimeIndex = RetrieveDebugTimeIndex(ref synthesizer);
            for (int i = 0; i < math.min(MaxNumCandidates, candidates.Count); ++i)
            {
                if (candidates[i].Item1.Equals(debugTimeIndex))
                {
                    return i;
                }
            }

            return 0;
        }

        public void HandleCandidates()
        {
            var sliderButton = new Slider();

            sliderButton.label = "Matched candidates";
            sliderButton.value = highlightedCandidate;
            sliderButton.lowValue = 0.0f;
            sliderButton.highValue = MaxNumCandidates - 1;

            controlsContainer.Add(sliderButton);

            sliderButton.RegisterValueChangedCallback((e) =>
            {
                bHighlightCandidate = true;
                highlightedCandidate = Missing.roundToInt(e.newValue);
            });
        }

        DeviationTable ComputeDeviationTable(ref MatchFragmentTask task)
        {
            unsafe
            {
                if (task.trajectory.IsValid)
                {
                    return task.SelectMatchingPoseAndTrajectory(true);
                }
                else
                {
                    return task.SelectMatchingPose(true);
                }
            }
        }

        void AddCostInformation(FragmentDisplayInfo fragment, ref Binary binary, ref DeviationTable deviationTable)
        {
            if (!fragment.samplingTime.IsValid)
            {
                return;
            }

            DeviationScore deviation = deviationTable.GetDeviation(ref binary, fragment.samplingTime.timeIndex);
            if (!deviation.IsValid)
            {
                return;
            }

            if (deviation.trajectoryDeviation >= 0.0f)
            {
                fragment.label.text = $"{fragment.label.text}\nPose cost:{deviation.poseDeviation:0.000}, trajectory:{deviation.trajectoryDeviation:0.000}, total:{deviation.poseDeviation + deviation.trajectoryDeviation:0.000}";
            }
            else
            {
                fragment.label.text = $"{fragment.label.text}\nPose cost:{deviation.poseDeviation:0.000}";
            }
        }
    }
}
