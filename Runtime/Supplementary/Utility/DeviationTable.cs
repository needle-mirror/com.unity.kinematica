using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Assertions;

namespace Unity.Kinematica
{
    internal struct DeviationScore
    {
        public float poseDeviation;
        public float trajectoryDeviation;

        public float TotalDeviation => math.max(poseDeviation, 0.0f) + math.max(trajectoryDeviation, 0.0f);

        public bool IsValid => poseDeviation >= 0.0f;

        public static DeviationScore CreateInvalid() => new DeviationScore() { poseDeviation = -1.0f, trajectoryDeviation = -1.0f };
    }

    internal struct DeviationTable : IDisposable
    {
        internal static DeviationTable CreateInvalid()
        {
            return new DeviationTable();
        }

        internal static DeviationTable Create(MemoryArray<PoseSequence> sequences)
        {
            int numDeviations = 0;
            for (int i = 0; i < sequences.Length; ++i)
            {
                numDeviations += sequences[i].numFrames;
            }

            DeviationTable table = new DeviationTable()
            {
                sequences = new NativeArray<PoseSequenceInfo>(sequences.length, Allocator.Persistent),
                deviations = new NativeArray<DeviationScore>(numDeviations, Allocator.Persistent)
            };

            int deviationIndex = 0;
            for (int i = 0; i < sequences.Length; ++i)
            {
                table.sequences[i] = new PoseSequenceInfo()
                {
                    sequence = sequences[i],
                    firstDeviationIndex = deviationIndex
                };

                deviationIndex += sequences[i].numFrames;
            }

            for (int i = 0; i < numDeviations; ++i)
            {
                table.deviations[i] = DeviationScore.CreateInvalid();
            }

            return table;
        }

        internal void SetDeviation(int sequenceIndex, int frameIndex, float poseDeviation, float trajectoryDeviation)
        {
            Assert.IsTrue(sequenceIndex < sequences.Length);
            Assert.IsTrue(frameIndex < sequences[sequenceIndex].sequence.numFrames);

            int deviationIndex = sequences[sequenceIndex].firstDeviationIndex + frameIndex;
            deviations[deviationIndex] = new DeviationScore()
            {
                poseDeviation = poseDeviation,
                trajectoryDeviation = trajectoryDeviation
            };
        }

        internal DeviationScore GetDeviation(ref Binary binary, TimeIndex timeIndex)
        {
            for (int i = 0; i < sequences.Length; ++i)
            {
                PoseSequenceInfo sequenceInfo = sequences[i];
                ref Binary.Interval interval = ref binary.GetInterval(sequenceInfo.sequence.intervalIndex);
                if (interval.Contains(timeIndex))
                {
                    if (timeIndex.frameIndex >= sequenceInfo.sequence.firstFrame && timeIndex.frameIndex < sequenceInfo.sequence.onePastLastFrame)
                    {
                        int deviationIndex = sequenceInfo.firstDeviationIndex + timeIndex.frameIndex - sequenceInfo.sequence.firstFrame;
                        return deviations[deviationIndex];
                    }
                }
            }

            return DeviationScore.CreateInvalid();
        }

        internal List<Tuple<TimeIndex, DeviationScore>> SortCandidatesByDeviation(ref Binary binary)
        {
            List<Tuple<TimeIndex, DeviationScore>> candidates = new List<Tuple<TimeIndex, DeviationScore>>(deviations.Length);

            for (int i = 0; i < sequences.Length; ++i)
            {
                PoseSequence sequence = sequences[i].sequence;
                ref Binary.Interval interval = ref binary.GetInterval(sequence.intervalIndex);
                for (int frame = 0; frame < sequence.numFrames; ++frame)
                {
                    DeviationScore deviation = deviations[sequences[i].firstDeviationIndex + frame];
                    if (deviation.IsValid)
                    {
                        TimeIndex timeIndex = TimeIndex.Create(interval.segmentIndex, sequence.firstFrame + frame);

                        Tuple<TimeIndex, DeviationScore> candidate = new Tuple<TimeIndex, DeviationScore>(timeIndex, deviation);
                        candidates.Add(candidate);
                    }
                }
            }

            candidates.Sort((x, y) => x.Item2.TotalDeviation.CompareTo(y.Item2.TotalDeviation));

            return candidates;
        }

        public void Dispose()
        {
            sequences.Dispose();
            deviations.Dispose();
        }

        internal struct PoseSequenceInfo
        {
            public PoseSequence sequence;
            public int firstDeviationIndex;
        }

        NativeArray<PoseSequenceInfo> sequences;
        NativeArray<DeviationScore> deviations;
    }
}
