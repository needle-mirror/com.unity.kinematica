using UnityEngine.UIElements;
using UnityEditor.UIElements;
using Unity.SnapshotDebugger;
using UnityEngine.Assertions;

namespace Unity.Kinematica.Editor
{
    [GraphNode(typeof(PoseSequence))]
    internal class PoseSequenceNode : GraphNode
    {
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
        }

        public unsafe override void OnPreLateUpdate(ref MotionSynthesizer synthesizer)
        {
            if (Debugger.instance.rewind)
            {
                var overrideTime =
                    RetrieveDebugTimeIndex(ref synthesizer);

                if (overrideTime.IsValid)
                {
                    ref var memoryChunk = ref synthesizer.memoryChunkShadow.Ref;

                    var poseSequence = memoryChunk.GetArray<PoseSequence>(identifier);

                    for (int i = 0; i < poseSequence.Length; ++i)
                    {
                        poseSequence[i].numFrames = 0;
                    }

                    ref var binary = ref synthesizer.Binary;

                    var numIntervals = binary.numIntervals;

                    for (int i = 0; i < numIntervals; ++i)
                    {
                        ref var interval = ref binary.GetInterval(i);

                        if (interval.segmentIndex == overrideTime.segmentIndex)
                        {
                            if (interval.Contains(overrideTime.frameIndex))
                            {
                                poseSequence[0].intervalIndex = i;
                                poseSequence[0].firstFrame = overrideTime.frameIndex;
                                poseSequence[0].numFrames = 1;

                                break;
                            }
                        }
                    }

                    memoryChunk.GetHeader(identifier)->SetDirty();
                }
            }
        }
    }
}
