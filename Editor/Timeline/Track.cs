using Unity.Kinematica.UIElements;
using UnityEngine.UIElements;

namespace Unity.Kinematica.Editor
{
    abstract class Track : VisualElement
    {
        protected internal readonly Timeline m_Owner;
        protected readonly VisualElement m_Track;

        Label m_Label;

        protected Track(Timeline owner)
        {
            AddToClassList("track");
            m_Owner = owner;

            UIElementsUtils.ApplyStyleSheet(Timeline.k_Stylesheet, this);
            UIElementsUtils.CloneTemplateInto("Track.uxml", this);

            m_Track = this.Q<VisualElement>(classes: "trackElement");
            m_Track.AddToClassList(Timeline.k_timelineCellStyleKey);

            m_Label = this.Q<Label>("trackName");
            m_Label.style.display = DisplayStyle.None;
        }

        protected Track(string name, Timeline owner) : this(owner)
        {
            m_Label = this.Q<Label>("trackName");
            m_Label.text = name;
        }

        public void AddElement(ITimelineElement timelineElement)
        {
            if (timelineElement is VisualElement element)
            {
                m_Track.Add(element);
            }
        }

        public virtual void OnAssetModified() {}

        public abstract void ReloadElements();

        public abstract void ResizeContents();

        internal void RepositionLabels()
        {
            if (m_Label == null)
            {
                return;
            }

            VisualElement sv = GetFirstAncestorOfType<ScrollView>();

            m_Label.style.left = 4 - (parent.worldBound.x - sv.worldBound.x);
        }
    }
}
