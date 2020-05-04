using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.Assertions.Comparers;
using UnityEngine.Profiling;
using UnityEngine.UIElements;

namespace Unity.Kinematica.Editor
{
    class MarkerTrack : GutterTrack
    {
        static FloatComparer k_MarkerTimelineComparer = new FloatComparer(0.0001f);

        public MarkerTrack(Timeline owner) : base(owner)
        {
            name = "Markers";
            AddToClassList("markerTrack");

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            Undo.undoRedoPerformed += ReloadElements;
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            Undo.undoRedoPerformed -= ReloadElements;
        }

        public override void SetClip(TaggedAnimationClip taggedClip)
        {
            if (m_TaggedClip != null)
            {
                m_TaggedClip.MarkerAdded -= OnMarkerAdded;
                m_TaggedClip.MarkerRemoved -= OnMarkerRemoved;
            }

            var markers = GetMarkerElements().ToList();
            foreach (var mte in markers)
            {
                mte.MarkerTimelineElementMoved -= OnMarkerTimelineElementMoved;
                mte.RemoveFromHierarchy();
            }

            base.SetClip(taggedClip);

            if (m_TaggedClip != null)
            {
                m_TaggedClip.MarkerAdded += OnMarkerAdded;
                m_TaggedClip.MarkerRemoved += OnMarkerRemoved;
            }

            ReloadElements();
        }

        public override void ReloadElements()
        {
            Profiler.BeginSample("MarkerTrack.ReloadElements");
            Clear();

            if (m_TaggedClip != null)
            {
                foreach (var m in m_TaggedClip.Markers)
                {
                    CreateMarkerElement(m);
                }
            }

            SetDisplay(style.display.value);

            Profiler.EndSample();
        }

        public override void ResizeContents()
        {
            foreach (MarkerElement visualElement in GetMarkerElements())
            {
                visualElement.Reposition();
            }
        }

        void OnMarkerTimelineElementMoved(float previous, float current)
        {
            var elementsAtPreviousTime = GetMarkersAtTime(previous).ToList();
            if (elementsAtPreviousTime.Count() == 1)
            {
                elementsAtPreviousTime.First().HideMultiple();
            }

            var elementsAtCurrentTime = GetMarkersAtTime(current).ToList();
            if (elementsAtCurrentTime.Count() > 1)
            {
                elementsAtCurrentTime.ForEach(me => me.ShowMultiple());
            }
            else
            {
                elementsAtCurrentTime.ForEach(me => me.HideMultiple());
            }
        }

        void CreateMarkerElement(MarkerAnnotation marker)
        {
            var me = new MarkerElement(marker, this);
            me.MarkerTimelineElementMoved += OnMarkerTimelineElementMoved;
            AddElement(me);

            if (m_Owner.SelectionContainer.m_Markers.Contains(marker) && !m_Owner.SelectionContainer.m_FullClipSelection)
            {
                MarkerElement.SelectMarkerElement(me, m_Owner.SelectionContainer.Count > 1);
            }
        }

        void OnMarkerAdded(MarkerAnnotation marker)
        {
            var markerElement = GetMarkerElements().FirstOrDefault(me => me.marker == marker);
            if (markerElement != null)
            {
                return;
            }

            CreateMarkerElement(marker);
        }

        void OnMarkerRemoved(MarkerAnnotation marker)
        {
            var markerTimelineElement = GetMarkerElements().FirstOrDefault(me => me.marker == marker);
            if (markerTimelineElement != null)
            {
                markerTimelineElement.RemoveFromHierarchy();
            }

            m_Owner.SelectionContainer.Remove(marker);
        }

        internal IEnumerable<MarkerElement> GetMarkerElements()
        {
            return Children().OfType<MarkerElement>();
        }

        internal IEnumerable<MarkerElement> GetMarkersAtTime(float time)
        {
            foreach (var e in GetMarkerElements())
            {
                if (k_MarkerTimelineComparer.Equals(e.marker.timeInSeconds, time))
                {
                    yield return e;
                }
            }
        }
    }
}
