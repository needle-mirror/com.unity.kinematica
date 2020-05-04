using System;
using System.Collections.Generic;
using Unity.SnapshotDebugger;
using UnityEngine;

namespace Unity.Kinematica
{
    internal class AnimationDebugRecord : FrameDebugRecord
    {
        public override void Init(int providerIdentifier, string displayName)
        {
            base.Init(providerIdentifier, displayName);

            m_ProviderAlive = true;

            m_AnimationRecords = new CircularList<AnimationRecord>();
            m_NumActiveAnims = 0;
            m_LinesEndTimes = new List<float>();
        }

        public CircularList<AnimationRecord> AnimationRecords => m_AnimationRecords;

        public int NumLines => m_LinesEndTimes.Count;

        public override bool IsObsolete => !m_ProviderAlive && m_AnimationRecords.Count == 0;

        public override void UpdateRecordEntries(float frameStartTime, float frameEndTime, FrameDebugProvider provider)
        {
            var frameSnapshots = ((FrameDebugProvider<AnimationFrameDebugInfo>)provider).GetFrameDebugInfo();

            List<int> newSnapshots = new List<int>(); // snaphots from sequence that weren't already present in the records

            int updatedSnapshots = 0;

            for (int snapshotIndex = 0; snapshotIndex < frameSnapshots.Count; ++snapshotIndex)
            {
                AnimationFrameDebugInfo frameDebugInfo = frameSnapshots[snapshotIndex];

                bool bAddNewRecord = true;

                for (int i = m_AnimationRecords.Count - m_NumActiveAnims; i < m_AnimationRecords.Count - updatedSnapshots; ++i)
                {
                    AnimationRecord animRecord = m_AnimationRecords[i];
                    if (animRecord.sequenceIdentifier == frameDebugInfo.sequenceIdentifier)
                    {
                        // snapshot from already existing sequence, just add a new animation frame
                        animRecord.endTime = frameEndTime;
                        animRecord.AddAnimationFrame(frameEndTime, frameDebugInfo.weight, frameDebugInfo.animFrame);

                        m_LinesEndTimes[animRecord.rank] = frameEndTime;
                        m_AnimationRecords.SwapElements(i, m_AnimationRecords.Count - updatedSnapshots - 1);
                        ++updatedSnapshots;

                        bAddNewRecord = false;
                        break;
                    }
                }

                if (bAddNewRecord)
                {
                    newSnapshots.Add(snapshotIndex);
                }
            }

            foreach (int snapshotIndex in newSnapshots)
            {
                // snapshot from new sequence, add a new record
                AnimationFrameDebugInfo frameDebugInfo = frameSnapshots[snapshotIndex];

                CircularList<AnimationFrameInfo> animFrames = new CircularList<AnimationFrameInfo>();
                animFrames.PushBack(new AnimationFrameInfo { endTime = frameEndTime, weight = frameDebugInfo.weight, animFrame = frameDebugInfo.animFrame });

                m_AnimationRecords.PushBack(new AnimationRecord
                {
                    sequenceIdentifier = frameDebugInfo.sequenceIdentifier,
                    animName = frameDebugInfo.animName,
                    startTime = frameStartTime,
                    endTime = frameEndTime,
                    rank = GetNewAnimRank(frameStartTime, frameEndTime),
                    animFrames = animFrames
                });
            }

            m_NumActiveAnims = frameSnapshots.Count;
        }

        public override void PruneFramesBeforeTimestamp(float timestamp)
        {
            int i = 0;
            while (i < m_AnimationRecords.Count)
            {
                AnimationRecord animRecord = m_AnimationRecords[i];
                animRecord.PruneAnimationFramesBeforeTimestamp(timestamp);
                if (animRecord.animFrames.Count == 0)
                {
                    // all frames from animation record have been removed, we can remove the record
                    m_AnimationRecords.SwapElements(i, 0);
                    m_AnimationRecords.PopFront();
                }
                else
                {
                    ++i;
                }
            }

            m_NumActiveAnims = Mathf.Min(m_NumActiveAnims, m_AnimationRecords.Count);
        }

        public override void NotifyProviderRemoved()
        {
            m_ProviderAlive = false;
        }

        int GetNewAnimRank(float startTime, float endTime)
        {
            // find the highest available rank
            for (int rank = 0; rank < m_LinesEndTimes.Count; ++rank)
            {
                if (m_LinesEndTimes[rank] <= startTime)
                {
                    m_LinesEndTimes[rank] = endTime;
                    return rank;
                }
            }

            m_LinesEndTimes.Add(endTime);
            return m_LinesEndTimes.Count - 1;
        }

        bool                                m_ProviderAlive;

        CircularList<AnimationRecord>       m_AnimationRecords;
        int                                 m_NumActiveAnims;
        List<float>                         m_LinesEndTimes;
    }
}
