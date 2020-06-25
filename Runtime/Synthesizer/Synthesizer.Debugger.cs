using System.Collections.Generic;
using Unity.SnapshotDebugger;

using Buffer = Unity.SnapshotDebugger.Buffer;

namespace Unity.Kinematica
{
    public partial struct MotionSynthesizer
    {
        /// <summary>
        /// return the currently active animation frames
        /// </summary>
        /// <returns></returns>
        public List<AnimationFrameDebugInfo> GetFrameDebugInfo()
        {
            List<AnimationFrameDebugInfo> snapshots = new List<AnimationFrameDebugInfo>();

#if UNITY_EDITOR
            if (poseGenerator.CurrentPushIndex >= 0)
            {
                AnimationSampleTimeIndex animSampleTime = Binary.GetAnimationSampleTimeIndex(Time.timeIndex);

                if (animSampleTime.IsValid)
                {
                    AnimationFrameDebugInfo lastFrame = new AnimationFrameDebugInfo()
                    {
                        sequenceIdentifier = poseGenerator.CurrentPushIndex,
                        animName = animSampleTime.clipName,
                        animFrame = animSampleTime.animFrameIndex,
                        weight = poseGenerator.ApproximateTransitionProgression,
                        blendOutDuration = BlendDuration,
                    };
                    snapshots.Add(lastFrame);
                }
            }
#endif

            return snapshots;
        }

        internal void WriteToStream(Buffer buffer)
        {
            buffer.Write(rootTransform);
            buffer.Write(rootDeltaTransform);
            buffer.Write(samplingTime);
            buffer.Write(lastSamplingTime);
            buffer.Write(delayedPushTime);

            poseGenerator.WriteToStream(buffer);
            trajectory.WriteToStream(buffer);

            memoryChunk.Ref.WriteToStream(buffer);
        }

        internal void ReadFromStream(Buffer buffer)
        {
            rootTransform = buffer.ReadAffineTransform();
            rootDeltaTransform = buffer.ReadAffineTransform();
            samplingTime = buffer.ReadSamplingTime();
            lastSamplingTime = buffer.ReadTimeIndex();
            delayedPushTime = buffer.ReadTimeIndex();

            poseGenerator.ReadFromStream(buffer);
            trajectory.ReadFromStream(buffer);

            memoryChunk.Ref.ReadFromStream(buffer);
        }

        /// <summary>
        /// Post process callback called after all snapshot objects have been serialized, can be use to serialize additional data
        /// </summary>
        internal void OnWritePostProcess(Buffer buffer)
        {
#if UNITY_EDITOR
            memoryChunkShadow.Ref.WriteToStream(buffer);
#endif
        }

        /// <summary>
        /// Post process callback called after all snapshot objects have been deserialized, can be use to deserialize additional data
        /// </summary>
        internal void OnReadPostProcess(Buffer buffer)
        {
#if UNITY_EDITOR
            memoryChunkShadow.Ref.ReadFromStream(buffer);
#endif
        }
    }
}
