using System;
using UnityEngine;
using UnityEngine.Assertions.Comparers;

namespace Unity.Kinematica.Editor
{
    partial class Timeline
    {
        public float WidthMultiplier
        {
            get
            {
                if (TaggedClip == null || !TaggedClip.Valid)
                {
                    return 1f;
                }

                float duration = TaggedClip.AnimationClip.length * k_TimelineLengthMultiplier + SecondsBeforeZero;
                if (FloatComparer.AreEqual(duration, 0f, FloatComparer.kEpsilon) || float.IsNaN(m_TimelineScrollableArea.layout.width))
                {
                    return 1f;
                }

                return m_TimelineScrollableArea.layout.width / duration;
            }
        }

        void ResizeContents()
        {
            if (TaggedClip == null)
            {
                return;
            }

            foreach (Track t in m_TrackElements)
            {
                t.ResizeContents();
            }

            UpdatePlayheadPositions();
            AdjustTicks();
        }

        void UpdatePlayheadPositions(bool propagateToLabel = true)
        {
            //TODO - let the playhead itself handle this ...
            ActiveTick.style.left = TimeToLocalPos(ActiveTime, m_TimelineWorkArea);
            ActiveDebugTick.style.left = TimeToLocalPos(DebugTime, m_TimelineWorkArea);
            if (propagateToLabel)
            {
                SetFPSLabelText();
            }
        }

        void AdjustTicks()
        {
            if (TaggedClip == null)
            {
                return;
            }

            float startOfClipPos = TimeToLocalPos(0f, m_TimelineWorkArea);
            float clipDuration = TaggedClip.Clip.DurationInSeconds * WidthMultiplier;
            if (m_Mode != TimelineViewMode.seconds)
            {
                clipDuration = (float)Math.Ceiling(clipDuration);
            }

            float endOfClipPos = TimeToLocalPos(TaggedClip.Clip.DurationInSeconds, m_TimelineWorkArea);
            m_ClipLengthBar.style.left = startOfClipPos;
            m_EndOfClipLine.style.left = endOfClipPos;
            m_ClipLengthBar.style.width = clipDuration;
            m_ClipArea.style.left = startOfClipPos;
            m_ClipArea.style.width = clipDuration;
            m_ClipArea.style.height = m_ScrollViewContainer.layout.height;

            RepositionBoundaryClips(startOfClipPos, endOfClipPos);
            AnnotationsTrack annotationsTrack = null;
            float takenHeight = 0f;

            foreach (var track in m_TrackElements)
            {
                if (track is AnnotationsTrack at)
                {
                    annotationsTrack = at;
                }
                else
                {
                    takenHeight += track.layout.height;
                }
            }

            if (annotationsTrack != null)
            {
                annotationsTrack.style.minHeight = Math.Max(m_ScrollViewContainer.layout.height - takenHeight, 0f);
            }
        }

        void RepositionBoundaryClips()
        {
            if (TaggedClip?.Clip == null)
            {
                return;
            }


            float startOfClipPos = TimeToLocalPos(0f, m_TimelineWorkArea);
            float endOfClipPos = TimeToLocalPos(TaggedClip.Clip.DurationInSeconds, m_TimelineWorkArea);
            RepositionBoundaryClips(startOfClipPos, endOfClipPos);
        }

        void RepositionBoundaryClips(float startOfClipPos, float endOfClipPos)
        {
            Vector2 estimatedTextSize = m_PreBoundaryClipElement.MeasureTextSize(m_PreBoundaryClipElement.text, m_PreBoundaryClipElement.layout.width, MeasureMode.Undefined, m_PreBoundaryClipElement.layout.height,
                MeasureMode.Undefined);
            float controlWidth = Math.Max(float.IsNaN(estimatedTextSize.x) ? m_PreBoundaryClipElement.layout.width : estimatedTextSize.x + 16, 80) + 2;

            m_PreBoundaryClipElement.style.left = startOfClipPos - controlWidth;
            m_PostBoundaryClipElement.style.left = endOfClipPos;
        }
    }
}
