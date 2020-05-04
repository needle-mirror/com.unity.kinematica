using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
using UnityEngine.Assertions;

using Unity.Mathematics;
using Unity.SnapshotDebugger;
using Unity.Collections;

using Buffer = Unity.SnapshotDebugger.Buffer;
using System.Linq;

namespace Unity.Kinematica
{
    public partial class Kinematica : SnapshotProvider, FrameDebugProvider<AnimationFrameDebugInfo>
    {
        List<AnimationFrameDebugInfo> m_FrameDebugInfos = new List<AnimationFrameDebugInfo>();

        public int GetUniqueIdentifier()
        {
            return gameObject.GetInstanceID();
        }

        public string GetDisplayName()
        {
            return gameObject.name;
        }

        public List<AnimationFrameDebugInfo> GetFrameDebugInfo()
        {
            List<AnimationFrameDebugInfo> snapshots = new List<AnimationFrameDebugInfo>();

            if (synthesizer.IsValid && synthesizer.Ref.CurrentPushIndex >= 0)
            {
                AnimationSampleTimeIndex animSampleTime = synthesizer.Ref.Binary.GetAnimationSampleTimeIndex(synthesizer.Ref.Time.timeIndex);

                if (animSampleTime.IsValid)
                {
                    AnimationFrameDebugInfo lastFrame = new AnimationFrameDebugInfo()
                    {
                        sequenceIdentifier = synthesizer.Ref.CurrentPushIndex,
                        animName = animSampleTime.clipName,
                        animFrame = animSampleTime.animFrameIndex,
                        weight = synthesizer.Ref.ApproximateTransitionProgression,
                    };
                    snapshots.Add(lastFrame);


                    // update "blending-out" anims
                    if (blendDuration > 0.0f)
                    {
                        if (!m_FrameDebugInfos.Any((AnimationFrameDebugInfo f) => f.sequenceIdentifier == lastFrame.sequenceIdentifier))
                        {
                            m_FrameDebugInfos.Add(lastFrame);
                        }

                        float blendOutSpeed = 1.0f / blendDuration;
                        for (int i = 0; i < m_FrameDebugInfos.Count; ++i)
                        {
                            if (m_FrameDebugInfos[i].sequenceIdentifier == lastFrame.sequenceIdentifier)
                            {
                                m_FrameDebugInfos[i] = lastFrame;
                            }
                            else
                            {
                                var frameDebugInfo = m_FrameDebugInfos[i];
                                frameDebugInfo.weight -= blendOutSpeed * _deltaTime;
                                m_FrameDebugInfos[i] = frameDebugInfo;

                                // add previous anim frames that are still influencing the final pose because of the inertia
                                // those frames will be considered as blending out in the snapshot debugger
                                if (frameDebugInfo.weight > 0.0f)
                                {
                                    snapshots.Add(frameDebugInfo);
                                }
                            }
                        }

                        m_FrameDebugInfos.RemoveAll(f => f.weight <= 0.0f);
                    }
                }
            }

            return snapshots;
        }

        /// <summary>
        /// Stores the contents of the Kinematica component in the buffer passed as argument.
        /// </summary>
        /// <param name="buffer">Buffer that the contents of the Kinematica component should be written to.</param>
        public override void WriteToStream(Buffer buffer)
        {
            buffer.Write(transform.position);
            buffer.Write(transform.rotation);

            if (synthesizer.IsValid)
            {
                synthesizer.Ref.WriteToStream(buffer);
            }
        }

        /// <summary>
        /// Retrieves the contents of the Kinematica component from the buffer passed as argument.
        /// </summary>
        /// <param name="buffer">Buffer that the contents of the Kinematica component should be read from.</param>
        public override void ReadFromStream(Buffer buffer)
        {
            transform.position = buffer.ReadVector3();
            transform.rotation = buffer.ReadQuaternion();

            if (synthesizer.IsValid)
            {
                synthesizer.Ref.ReadFromStream(buffer);
            }
        }

        /// <summary>
        /// Override of the OnPostProcess() method which gets invoked during snapshot debugging.
        /// </summary>
        public override Buffer OnPostProcess()
        {
            if (synthesizer.IsValid)
            {
                return synthesizer.Ref.OnPostProcess();
            }

            return null;
        }

        /// <summary>
        /// Override of the OnPostProcess() method which gets invoked during snapshot debugging.
        /// </summary>
        public override void OnPostProcess(Buffer buffer)
        {
            if (synthesizer.IsValid)
            {
                synthesizer.Ref.OnPostProcess(buffer);
            }
        }
    }
}
