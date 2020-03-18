using UnityEngine;

namespace Unity.Kinematica.Editor
{
    internal class MetricBorderElement : TimeRangeElement
    {
        public float m_OffsetTime;
        public float m_Duration;

        Timeline m_Timeline;

        public MetricBorderElement(float offsetTime, float duration, Timeline timeline) : base(timeline.WidthMultiplier)
        {
            m_OffsetTime = offsetTime;
            m_Duration = duration;
            m_Timeline = timeline;

            style.backgroundColor = Color.black;

            AddToClassList("metricElement");
            SetEnabled(true);

            Resize(m_Duration);
        }

        protected override void Resize()
        {
            style.left = m_OffsetTime  * m_WidthMultiplier;
            style.width = m_Duration * m_WidthMultiplier;

            style.backgroundColor = Color.black;
            style.color = Color.black;
        }
    }

    class MetricElement : TimeRangeElement
    {
        public string MetricName { get; }

        public float m_StartTime;
        public float m_EndTime;

        MetricBorderElement m_LeftBorder;
        MetricBorderElement m_RightBorder;

        Timeline m_Timeline;

        public MetricElement(string name, float start, float end, float activeStart, float activeEnd, bool emptyInterval, Timeline timeline) : base(timeline.WidthMultiplier)
        {
            MetricName = name;
            float maxTime = timeline.TaggedClip == null ? 1f : timeline.TaggedClip.DurationInSeconds;
            m_StartTime = Mathf.Clamp(start, 0f, maxTime);
            m_EndTime = Mathf.Clamp(end, 0f, maxTime);

            m_Timeline = timeline;

            AddToClassList("metricElement");
            SetEnabled(false);

            if (emptyInterval)
            {
                m_Label.text = "Too short !";
                style.backgroundColor = Color.red;
                tooltip = "The metric segment is too short, all its poses are discarded";
            }
            else
            {
                m_Label.text = MetricName;
                tooltip = MetricName;

                if (activeStart > start)
                {
                    m_LeftBorder = new MetricBorderElement(0.0f, activeStart - start, timeline);
                    Add(m_LeftBorder);
                }

                if (activeEnd < end)
                {
                    m_RightBorder = new MetricBorderElement(end - start - (end - activeEnd), end - activeEnd, timeline);
                    Add(m_RightBorder);
                }
            }

            Resize(m_EndTime);
        }

        protected override void Resize()
        {
            style.left = (m_StartTime + m_Timeline.SecondsBeforeZero) * m_WidthMultiplier;
            style.width = (m_EndTime - m_StartTime) * m_WidthMultiplier;

            if (m_LeftBorder != null)
            {
                m_LeftBorder.Resize(m_WidthMultiplier);
            }

            if (m_RightBorder != null)
            {
                m_RightBorder.Resize(m_WidthMultiplier);
            }
        }
    }
}
