using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions.Comparers;
using UnityEngine.UIElements;

namespace Unity.Kinematica.Editor
{
    class AnnotationsTrack : Track
    {
        internal TaggedAnimationClip m_TaggedClip;

        ContextualMenuManipulator m_TracksContextManipulator;

        static FloatComparer k_MarkerTimelineComparer = new FloatComparer(0.0001f);

        public AnnotationsTrack(Timeline owner) : base(owner)
        {
            m_TracksContextManipulator = new ContextualMenuManipulator(evt =>
            {
                AddTagMenu(evt, action => OnAddAnnotationSelection(action.userData as Type), EditorApplication.isPlaying ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
                evt.menu.AppendSeparator();
                AddMarkerMenu(evt, action => OnAddAnnotationSelection(action.userData as Type), EditorApplication.isPlaying ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
            });
        }

        public override void OnAssetModified()
        {
            if (m_TaggedClip != null)
            {
                var clipTags = m_TaggedClip.Tags;
                var clipMarkers = m_TaggedClip.Markers;
                var timelineElements = m_Track.Children().OfType<ITimelineElement>().ToList();
                if (clipTags.Count + clipMarkers.Count != timelineElements.Count)
                {
                    ReloadElements();
                }
                else
                {
                    foreach (var timelineElement in timelineElements)
                    {
                        if (timelineElement is TagElement tagElement)
                        {
                            if (!clipTags.Contains(tagElement.m_Tag))
                            {
                                ReloadElements();
                                return;
                            }
                        }
                        else if (timelineElement is MarkerTimelineElement markerElement)
                        {
                            if (!clipMarkers.Contains(markerElement.marker))
                            {
                                ReloadElements();
                                return;
                            }
                        }
                    }

                    foreach (var timelineElement in timelineElements)
                    {
                        timelineElement.Reposition();
                    }
                }
            }
        }

        public override void ReloadElements()
        {
            m_Track.Clear();
            var markerElements = GetMarkerElements().ToList();
            foreach (var markerElement in markerElements)
            {
                markerElement.RemoveFromHierarchy();
            }

            if (m_TaggedClip == null)
            {
                return;
            }

            foreach (var tag in m_TaggedClip.Tags)
            {
                //TODO - group instances of same tag on same line (multiple TagElement to one TagTimelineEntry)
                var tagElement = new TagElement(m_Owner, m_TaggedClip, tag, m_Owner.WidthMultiplier);
                m_Track.Add(tagElement);
            }

            CreateMarkerElementsFromClip();
        }

        public override void ResizeContents()
        {
            foreach (var visualElement in m_Track.Children())
            {
                if (visualElement is TimeRangeElement rangeElement)
                {
                    rangeElement.Resize(m_Owner.WidthMultiplier);
                }
            }

            foreach (MarkerTimelineElement visualElement in GetMarkerElements())
            {
                visualElement.Reposition(m_Owner.WidthMultiplier);
            }
        }

        public bool ReorderTagElement(ITimelineElement element, int direction)
        {
            if (direction == 0 || element == null)
            {
                return false;
            }

            TagAnnotation tag = element.Object as TagAnnotation;
            if (tag == null)
            {
                return false;
            }

            int currentIndex = m_TaggedClip.Tags.IndexOf(tag);
            int newIndex = currentIndex + direction;

            if (newIndex < 0 || newIndex > m_TaggedClip.Tags.Count - 1)
            {
                return false;
            }

            m_TaggedClip.Tags.Remove(tag);
            m_TaggedClip.Tags.Insert(newIndex, tag);
            m_Track.Insert(newIndex, element as VisualElement);
            return true;
        }

        public void SetClip(TaggedAnimationClip taggedClip)
        {
            if (m_TaggedClip != null)
            {
                m_TaggedClip.DataChanged -= ReloadElements;
                m_TaggedClip.MarkerAdded -= OnMarkerAdded;
                m_TaggedClip.MarkerRemoved -= OnMarkerRemoved;
            }

            var markers = GetMarkerElements().ToList();
            foreach (var mte in markers)
            {
                mte.MarkerTimelineElementMoved -= OnMarkerTimelineElementMoved;
                mte.RemoveFromHierarchy();
            }

            m_TaggedClip = taggedClip;

            if (m_TaggedClip != null)
            {
                m_TaggedClip.DataChanged += ReloadElements;
                m_TaggedClip.MarkerAdded += OnMarkerAdded;
                m_TaggedClip.MarkerRemoved += OnMarkerRemoved;

                this.AddManipulator(m_TracksContextManipulator);
            }
            else
            {
                this.RemoveManipulator(m_TracksContextManipulator);
            }

            ReloadElements();
        }

        void CreateMarkerElementsFromClip()
        {
            foreach (var m in m_TaggedClip.Markers)
            {
                CreateMarkerElement(m);
            }
        }

        /*
         * Markers
         */
        void CreateMarkerElement(MarkerAnnotation marker)
        {
            var me = new MarkerTimelineElement(marker, this);
            me.MarkerTimelineElementMoved += OnMarkerTimelineElementMoved;
            Add(me);
            me.Reposition(m_Owner.WidthMultiplier);
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

        void OnMarkerTimelineElementMoved(float previous, float current)
        {
            var elementsAtPreviousTime = GetMarkersAtTime(previous).ToList();
            if (elementsAtPreviousTime.Count() == 1)
            {
                elementsAtPreviousTime.ForEach(me => me.HideMultiple());
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

        internal IEnumerable<MarkerTimelineElement> GetMarkerElements()
        {
            return Children().OfType<MarkerTimelineElement>();
        }

        internal IEnumerable<MarkerTimelineElement> GetMarkersAtTime(float time)
        {
            foreach (var e in GetMarkerElements())
            {
                if (k_MarkerTimelineComparer.Equals(e.marker.timeInSeconds, time))
                {
                    yield return e;
                }
            }
        }

        internal float EndOfTags
        {
            get
            {
                float height = 0f;
                var tags = m_Track.Children().OfType<TagElement>();
                foreach (var tag in tags)
                {
                    height = Mathf.Max(height, tag.layout.yMax);
                }

                return height;
            }
        }

        void OnAddAnnotationSelection(Type type)
        {
            if (type != null && m_TaggedClip != null)
            {
                if (TagAttribute.IsTagType(type))
                {
                    m_TaggedClip.AddTag(type, m_Owner.ActiveTime);
                }
                else if (MarkerAttribute.IsMarkerType(type))
                {
                    m_TaggedClip.AddMarker(type, m_Owner.ActiveTime);
                }
            }
        }

        static void AddTagMenu(ContextualMenuPopulateEvent evt, Action<DropdownMenuAction> action, DropdownMenuAction.Status menuStatus)
        {
            const string prefix = "Add Tag/";
            foreach (var tagType in TagAttribute.GetVisibleTypesInInspector())
            {
                evt.menu.AppendAction($"{prefix}{TagAttribute.GetDescription(tagType)}", action, a => menuStatus, tagType);
            }
        }

        static void AddMarkerMenu(ContextualMenuPopulateEvent evt, Action<DropdownMenuAction> action, DropdownMenuAction.Status menuStatus)
        {
            const string prefix = "Add Marker /";
            foreach (var markerType in MarkerAttribute.GetMarkerTypes())
            {
                evt.menu.AppendAction($"{prefix}{MarkerAttribute.GetDescription(markerType)}", action, a => menuStatus, markerType);
            }
        }
    }
}
