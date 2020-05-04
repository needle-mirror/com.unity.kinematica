using UnityEngine.UIElements;

namespace Unity.Kinematica.Editor
{
    interface ITimelineElement
    {
        void Unselect();
        void Reposition();
        System.Object Object { get; }

        Timeline Timeline { get; }
    }

    abstract class TimeRangeElement : VisualElement, ITimelineElement
    {
        const float k_LabelCutoff = 36f; //TODO - ellipses

        protected readonly VisualElement m_LabelContainer;
        protected readonly Label m_Label;

        public Track Track { get; set; }

        public Timeline Timeline
        {
            get
            {
                return Track.m_Owner;
            }
        }

        protected TimeRangeElement(Track track)
        {
            Track = track;

            m_LabelContainer = new VisualElement();
            m_LabelContainer.AddToClassList("timelineTimeRangeLabelContainer");
            m_Label = new Label();
            m_Label.AddToClassList("timelineTimeRangeLabel");
            m_LabelContainer.Add(m_Label);

            Add(m_LabelContainer);

            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        public virtual void Unselect() {}

        public void Reposition()
        {
            Resize();
        }

        public virtual object Object
        {
            get { throw new System.NotImplementedException(); }
        }

        public abstract void Resize();

        protected virtual void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            UnregisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        void OnGeometryChanged(GeometryChangedEvent evt)
        {
            if (layout.width - m_Label.layout.width < k_LabelCutoff)
            {
                m_Label.style.visibility = Visibility.Hidden;
            }
            else
            {
                m_Label.style.visibility = Visibility.Visible;
            }
        }
    }
}
