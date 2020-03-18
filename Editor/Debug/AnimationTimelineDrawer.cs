using UnityEngine.Assertions;
using Unity.Mathematics;
using Unity.SnapshotDebugger;
using Unity.SnapshotDebugger.Editor;
using UnityEngine;

namespace Unity.Kinematica.Editor
{
    internal class AnimationTimelineDrawer : ITimelineDebugDrawer
    {
        internal struct AnimationFrameIdentifier
        {
            public int providerIdentifier;
            public int animIndex;
            public int animFrameIndex;
            public int mouseX;

            public bool IsValid
            {
                get
                {
                    return animIndex >= 0 && animFrameIndex >= 0;
                }
            }

            public static AnimationFrameIdentifier CreateInvalid()
            {
                return new AnimationFrameIdentifier()
                {
                    providerIdentifier = 0,
                    animIndex = -1,
                    animFrameIndex = -1,
                };
            }

            public bool Equals(AnimationFrameIdentifier rhs)
            {
                return (!IsValid && !rhs.IsValid) ||
                    (providerIdentifier == rhs.providerIdentifier && animIndex == rhs.animIndex && animFrameIndex == rhs.animFrameIndex);
            }
        }

        public AnimationTimelineDrawer()
        {
            m_SelectedAnimFrame = AnimationFrameIdentifier.CreateInvalid();
        }

        public float GetHeight()
        {
            float height = beginOffset;
            foreach (FrameDebugRecord debugRecord in Debugger.frameDebugger.Records)
            {
                if (debugRecord is AnimationDebugRecord record)
                {
                    height += GetAnimStateTimelineHeight(record);
                }
            }

            return height;
        }

        public void DrawTooltip()
        {
            if (m_SelectedAnimFrame.IsValid)
            {
                AnimationDebugRecord animStateRecord = (AnimationDebugRecord)Debugger.frameDebugger.GetRecord(m_SelectedAnimFrame.providerIdentifier);

                Assert.IsTrue(animStateRecord != null);
                if (animStateRecord == null)
                {
                    m_SelectedAnimFrame = AnimationFrameIdentifier.CreateInvalid();
                    return;
                }

                Assert.IsTrue(m_SelectedAnimFrame.animIndex < animStateRecord.AnimationRecords.Count);
                if (m_SelectedAnimFrame.animIndex >= animStateRecord.AnimationRecords.Count)
                {
                    m_SelectedAnimFrame = AnimationFrameIdentifier.CreateInvalid();
                    return;
                }

                AnimationRecord animRecord = animStateRecord.AnimationRecords[m_SelectedAnimFrame.animIndex];

                Assert.IsTrue(m_SelectedAnimFrame.animFrameIndex < animRecord.animFrames.Count);
                if (m_SelectedAnimFrame.animFrameIndex >= animRecord.animFrames.Count)
                {
                    m_SelectedAnimFrame = AnimationFrameIdentifier.CreateInvalid();
                    return;
                }

                AnimationFrameInfo animFrame = animRecord.animFrames[m_SelectedAnimFrame.animFrameIndex];

                string label = $"{animRecord.animName}\nFrame:{animFrame.animFrame:0.00}\nTime:{animFrame.animTime:0.00}\nWeight:{animFrame.weight:0.00}";
                Vector2 labelSize = TimelineWidget.GetLabelSize(label);

                Rect toolTipRect = new Rect(Event.current.mousePosition.x + 20, Event.current.mousePosition.y, labelSize.x + 5.0f, labelSize.y + 5.0f);

                TimelineWidget.DrawRectangleWithDetour(toolTipRect, new Color(0.75f, 0.75f, 0.75f, 1.0f), new Color(0.1f, 0.1f, 0.1f, 1.0f));
                TimelineWidget.DrawLabel(toolTipRect, label, new Color(0.1f, 0.1f, 0.1f, 1.0f));
            }
        }

        public void Draw(Rect rect, TimelineWidget.DrawInfo drawInfo)
        {
            m_SelectedAnimFrame = AnimationFrameIdentifier.CreateInvalid();

            float yPosition = rect.y + beginOffset;

            AnimationFrameIdentifier selectedAnimFrame = AnimationFrameIdentifier.CreateInvalid();
            foreach (FrameDebugRecord debugRecord in Debugger.frameDebugger.Records)
            {
                if (debugRecord is AnimationDebugRecord record)
                {
                    DrawAnimationTimeline(record, rect, drawInfo, ref yPosition);
                }
            }
        }

        private void DrawAnimationTimeline(AnimationDebugRecord animStateRecord, Rect rect, TimelineWidget.DrawInfo drawInfo, ref float yPosition)
        {
            if (animStateRecord.AnimationRecords.Count == 0)
            {
                return;
            }

            // Display name
            Vector2 labelSize = TimelineWidget.GetLabelSize(animStateRecord.DisplayName);
            TimelineWidget.DrawRectangle(new Rect(rect.x, yPosition, rect.width, characterNameRectangleHeight), new Color(0.1f, 0.1f, 0.1f, 1.0f));
            TimelineWidget.DrawLabel(new Rect(rect.x + 10.0f, yPosition, labelSize.x, characterNameRectangleHeight), animStateRecord.DisplayName, Color.white);

            yPosition += characterNameOffset;

            // Animations
            for (int i = 0; i < animStateRecord.AnimationRecords.Count; ++i)
            {
                DrawAnimationWidget(animStateRecord, i, rect, drawInfo, yPosition);
            }

            yPosition += animStateRecord.NumLines * animWidgetOffset + spaceBetweenTimelines;
        }

        private void DrawAnimationWidget(AnimationDebugRecord animStateRecord, int animIndex, Rect rect, TimelineWidget.DrawInfo drawInfo, float yPosition)
        {
            AnimationRecord animation = animStateRecord.AnimationRecords[animIndex];

            if (animation.endTime < drawInfo.rangeStart)
            {
                return;
            }

            if (animation.startTime > drawInfo.rangeStart + drawInfo.rangeWidth)
            {
                return;
            }

            float startPosition = drawInfo.GetPixelPosition(animation.startTime, rect.width);
            float endPosition = drawInfo.GetPixelPosition(animation.endTime, rect.width);


            yPosition = yPosition + animation.rank * animWidgetOffset;
            Rect animRect = new Rect(startPosition, yPosition, endPosition - startPosition, animWidgetHeight);

            TimelineWidget.DrawRectangleWithDetour(animRect, new Color(0.25f, 0.25f, 0.25f, 1.0f), new Color(0.5f, 0.5f, 0.5f, 1.0f));

            int barStartPosition = Missing.truncToInt(startPosition) + 1;
            int maxBarPosition = Missing.truncToInt(endPosition);
            for (int i = 0; i < animation.animFrames.Count; ++i)
            {
                int barEndPosition = Missing.truncToInt(drawInfo.GetPixelPosition(animation.animFrames[i].endTime, rect.width));
                if (barEndPosition > barStartPosition)
                {
                    float weight = animation.animFrames[i].weight;
                    if (weight < 1.0f)
                    {
                        Rect barRect = new Rect(barStartPosition, yPosition, barEndPosition - barStartPosition, (1.0f - weight) * animWidgetHeight);
                        TimelineWidget.DrawRectangle(barRect, new Color(0.05f, 0.05f, 0.05f, 1.0f));
                    }
                }
                barStartPosition = barEndPosition;
            }

            TimelineWidget.DrawLabelInsideRectangle(animRect, animation.animName, Color.white);

            // check if mouse is hovering the anim widget
            if (!m_SelectedAnimFrame.IsValid && endPosition > startPosition && animRect.Contains(Event.current.mousePosition))
            {
                float mouseNormalizedTime = (Event.current.mousePosition.x - startPosition) / (endPosition - startPosition);
                float mouseTime = animation.startTime + mouseNormalizedTime * (animation.endTime - animation.startTime);

                float curStartTime = animation.startTime;
                for (int i = 0; i < animation.animFrames.Count; ++i)
                {
                    float curEndTime = animation.animFrames[i].endTime;
                    if (curStartTime <= mouseTime && mouseTime <= curEndTime)
                    {
                        m_SelectedAnimFrame.providerIdentifier = animStateRecord.ProviderIdentifier;
                        m_SelectedAnimFrame.animIndex = animIndex;
                        m_SelectedAnimFrame.animFrameIndex = i;
                        m_SelectedAnimFrame.mouseX = Missing.truncToInt(Event.current.mousePosition.x);
                        return;
                    }
                    curStartTime = curEndTime;
                }
            }
        }

        private float GetAnimStateTimelineHeight(AnimationDebugRecord animStateRecord)
        {
            return animStateRecord.AnimationRecords.Count > 0 ? characterNameOffset + animStateRecord.NumLines * animWidgetOffset + spaceBetweenTimelines : 0.0f;
        }

        AnimationFrameIdentifier m_SelectedAnimFrame; // anim frame hovered by mouse

        static float beginOffset = 10.0f;
        static float characterNameRectangleHeight = 25.0f;
        static float characterNameOffset = 30.0f;
        static float animWidgetHeight = 25.0f;
        static float animWidgetOffset = 30.0f;
        static float spaceBetweenTimelines = 15.0f;
    }
}
