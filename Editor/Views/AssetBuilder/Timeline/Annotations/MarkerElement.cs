using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions.Comparers;
using UnityEngine.UIElements;

namespace Unity.Kinematica.Editor
{
    class MarkerElement : VisualElement, ITimelineElement, INotifyValueChanged<float>
    {
        const string k_MarkerStyleClass = "marker";
        const string k_MultiMarkerStyleClass = "multi-marker";

        public readonly MarkerAnnotation marker;

        MarkerTrack m_Track;

        VisualElement m_TimelineGuideline;
        Label m_ManipulateLabel;

        VisualElement m_MultipleMarkerIcon;
        VisualElement m_Content;
        readonly Color m_BackgroundColor;
        const float k_BackgroundAlpha = .7f;

        public MarkerElement(MarkerAnnotation marker, MarkerTrack track)
        {
            m_Track = track;
            Timeline = m_Track.m_Owner;
            this.marker = marker;
            focusable = true;

            m_Content = new VisualElement();
            m_Content.AddToClassList(k_MarkerStyleClass);
            m_BackgroundColor = AnnotationAttribute.GetColor(marker.payload.Type);
            var background = m_BackgroundColor;
            background.a = k_BackgroundAlpha;
            m_Content.style.backgroundColor = background;
            Add(m_Content);

            SetupManipulators(this);

            m_MultipleMarkerIcon = new VisualElement();
            m_MultipleMarkerIcon.AddToClassList(k_MultiMarkerStyleClass);
            m_MultipleMarkerIcon.style.visibility = Visibility.Hidden;
            Add(m_MultipleMarkerIcon);

            m_ManipulateLabel = new Label();
            m_ManipulateLabel.AddToClassList("markerManipulateLabel");
            m_ManipulateLabel.style.visibility = Visibility.Hidden;
            Add(m_ManipulateLabel);

            style.position = Position.Absolute;
            style.minHeight = 22;

            if (panel != null)
            {
                SetupTimelineGuideline(panel.visualTree);
            }
            else
            {
                RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            }

            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);

            marker.Changed += Reposition;
        }

        void SetupManipulators(VisualElement element)
        {
            element.AddManipulator(new MarkerDragManipulator(this));
            var contextManipulator = new ContextualMenuManipulator((obj) =>
            {
                SelectMarkerMenu(obj);
                obj.menu.AppendSeparator();
            });

            element.AddManipulator(contextManipulator);
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            UnregisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            SetupTimelineGuideline(evt.destinationPanel.visualTree);
            Reposition();
        }

        void SetupTimelineGuideline(VisualElement rootElement)
        {
            VisualElement timelineArea = rootElement.Q<VisualElement>(Timeline.k_TimelineWorkAreaName);
            VisualElement scrollableArea = timelineArea.Q<VisualElement>(Timeline.k_ScrollableTimeAreaName);
            m_TimelineGuideline = new VisualElement();
            m_TimelineGuideline.AddToClassList(k_MarkerStyleClass + "Guideline");
            m_TimelineGuideline.focusable = true;
            var background = m_BackgroundColor;
            background.a = k_BackgroundAlpha;
            m_TimelineGuideline.style.backgroundColor = background;
            m_TimelineGuideline.style.width = 1;
            m_TimelineGuideline.style.visibility = Visibility.Hidden;
            SetupManipulators(m_TimelineGuideline);
            scrollableArea.Add(m_TimelineGuideline);
        }

        void ShowManipulationLabel()
        {
            m_TimelineGuideline.style.visibility = Visibility.Visible;

            m_ManipulateLabel.style.visibility = Visibility.Visible;
            m_ManipulateLabel.text = TimelineUtility.GetTimeString(m_Track.m_Owner.ViewMode, marker.timeInSeconds, (int)m_Track.Clip.SampleRate);
        }

        void HideManipulationLabel()
        {
            m_TimelineGuideline.style.visibility = Visibility.Hidden;

            m_ManipulateLabel.style.visibility = Visibility.Hidden;
        }

        public void ShowMultiple()
        {
            m_MultipleMarkerIcon.style.visibility = Visibility.Visible;
        }

        public void HideMultiple()
        {
            m_MultipleMarkerIcon.style.visibility = Visibility.Hidden;
        }

        void SelectMarkerMenu(ContextualMenuPopulateEvent evt)
        {
            var markersAtTime = m_Track.GetMarkersAtTime(marker.timeInSeconds).ToList();
            foreach (var m in markersAtTime)
            {
                string text = markersAtTime.Count > 1 ? $"Select/{MarkerAttribute.GetDescription(m.marker.payload.Type)}" : $"Select {MarkerAttribute.GetDescription(m.marker.payload.Type)}";
                Action<DropdownMenuAction> action = a => SelectMarkerElement(m, false);
                if (m == this && markersAtTime.Count > 1)
                {
                    text += "*";
                }

                if (m.m_Selected)
                {
                    evt.menu.AppendAction(text, action, DropdownMenuAction.AlwaysDisabled);
                }
                else
                {
                    evt.menu.AppendAction(text, action, DropdownMenuAction.AlwaysEnabled);
                }
            }
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            marker.Changed -= Reposition;
            m_TimelineGuideline.RemoveFromHierarchy();
            UnregisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        internal event Action<float, float> MarkerTimelineElementMoved;

        float m_PreviousTime;

        public void Reposition()
        {
            var clip = m_Track.Clip;
            if (clip == null)
            {
                return;
            }


            float leftBefore = style.left.value.value;
            float timeLeftValue = Timeline.TimeToLocalPos(value, m_Track);
            style.left = timeLeftValue;
            m_TimelineGuideline.style.left = timeLeftValue;

            if (!FloatComparer.s_ComparerWithDefaultTolerance.Equals(leftBefore, timeLeftValue))
            {
                MarkerTimelineElementMoved?.Invoke(m_PreviousTime, value);
                m_PreviousTime = value;
            }
        }

        public void SetValueWithoutNotify(float value)
        {
            Undo.RecordObject(m_Track.Clip.Asset, "Drag Marker time");
            marker.timeInSeconds = value;
            Reposition();
        }

        public float value
        {
            get { return marker.timeInSeconds; }
            set
            {
                if (!EqualityComparer<float>.Default.Equals(marker.timeInSeconds, value))
                {
                    if (panel != null)
                    {
                        using (ChangeEvent<float> evt =
                                   ChangeEvent<float>.GetPooled(marker.timeInSeconds, value))
                        {
                            evt.target = this;
                            SetValueWithoutNotify(value);
                            SendEvent(evt);
                        }
                    }
                    else
                    {
                        SetValueWithoutNotify(value);
                    }

                    marker.NotifyChanged();
                    m_Track.m_Owner.TargetAsset.MarkDirty();
                }
            }
        }

        public void Unselect()
        {
            Color backgroundColor = m_BackgroundColor;
            backgroundColor.a = k_BackgroundAlpha;
            m_Content.style.backgroundColor = backgroundColor;
            m_TimelineGuideline.style.backgroundColor = backgroundColor;

            m_Selected = false;
            m_Content.MarkDirtyRepaint();
        }

        public System.Object Object
        {
            get { return marker; }
        }

        public Timeline Timeline { get; }

        bool m_Selected = false;

        internal static void SelectMarkerElement(MarkerElement markerElement, bool multi)
        {
            // cycle selection
            // if already selected and no ctrl key then we should find other marker elements underneath, bring them to the top..
            if (markerElement.m_Selected && !multi)
            {
                foreach (var marker in markerElement.m_Track.GetMarkersAtTime(markerElement.marker.timeInSeconds))
                {
                    if (marker != markerElement)
                    {
                        SelectMarkerElement(marker, false);
                        return;
                    }
                }
            }

            markerElement.BringToFront();
            markerElement.m_Track.m_Owner.Select(markerElement, multi);
            markerElement.m_Content.style.backgroundColor = markerElement.m_BackgroundColor;
            markerElement.m_TimelineGuideline.style.backgroundColor = markerElement.m_BackgroundColor;
            markerElement.m_Selected = true;
            markerElement.m_Content.MarkDirtyRepaint();
        }

        protected override void ExecuteDefaultActionAtTarget(EventBase evt)
        {
            base.ExecuteDefaultActionAtTarget(evt);

            if (evt is KeyDownEvent keyDownEvt)
            {
                if (keyDownEvt.keyCode == KeyCode.Delete)
                {
                    m_Track.m_Owner.DeleteSelection();
                }
            }
        }

        void OnTwinKeyDownEvent(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Delete)
            {
                m_Track.m_Owner.DeleteSelection();
            }
        }

        class MarkerDragManipulator : MouseManipulator
        {
            bool m_Active;

            MarkerElement m_Parent;

            public MarkerDragManipulator(MarkerElement parent)
            {
                m_Parent = parent;
                activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse });
                activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse, modifiers = EventModifiers.Control });
            }

            protected override void RegisterCallbacksOnTarget()
            {
                target.RegisterCallback<MouseDownEvent>(OnMouseDownEvent);
                target.RegisterCallback<MouseMoveEvent>(OnMouseMoveEvent);
                target.RegisterCallback<MouseUpEvent>(OnMouseUpEvent);
            }

            protected override void UnregisterCallbacksFromTarget()
            {
                target.UnregisterCallback<MouseDownEvent>(OnMouseDownEvent);
                target.UnregisterCallback<MouseMoveEvent>(OnMouseMoveEvent);
                target.UnregisterCallback<MouseUpEvent>(OnMouseUpEvent);
            }

            bool m_StartingDrag = false;
            bool m_MarkerDragged = false;

            void OnMouseDownEvent(MouseDownEvent evt)
            {
                if (m_Active)
                {
                    return;
                }

                if (!CanStartManipulation(evt))
                {
                    return;
                }

                m_Active = true;
                m_StartingDrag = true;
                m_MarkerDragged = false;
                m_Parent.ShowManipulationLabel();

                if (!m_Parent.m_Selected)
                {
                    SelectMarkerElement(m_Parent, evt.ctrlKey);
                }

                target.CaptureMouse();
                evt.StopPropagation();
            }

            void OnMouseMoveEvent(MouseMoveEvent evt)
            {
                if (!m_Active || !target.HasMouseCapture() || EditorApplication.isPlaying)
                {
                    return;
                }

                if (m_StartingDrag)
                {
                    Undo.RecordObject(m_Parent.m_Track.m_Owner.TargetAsset, "Moving marker");
                    m_StartingDrag = false;
                }

                float framerate = m_Parent.m_Track.Clip.SampleRate;
                var newTime = (float)TimelineUtility.RoundToFrame(m_Parent.m_Track.m_Owner.WorldPositionToTime(evt.mousePosition.x), framerate);

                if (!FloatComparer.s_ComparerWithDefaultTolerance.Equals(m_Parent.value, newTime))
                {
                    m_MarkerDragged = true;
                    m_Parent.value = newTime;
                    m_Parent.ShowManipulationLabel();
                    m_Parent.m_ManipulateLabel.text = TimelineUtility.GetTimeString(m_Parent.m_Track.m_Owner.ViewMode, newTime, (int)m_Parent.m_Track.Clip.SampleRate);
                }

                evt.StopPropagation();
            }

            void OnMouseUpEvent(MouseUpEvent evt)
            {
                if (!m_Active || !target.HasMouseCapture() || !CanStopManipulation(evt))
                {
                    return;
                }

                if (!m_MarkerDragged)
                {
                    SelectMarkerElement(m_Parent, evt.ctrlKey);
                }

                m_MarkerDragged = false;
                m_Active = false;
                m_StartingDrag = false;
                m_Parent.HideManipulationLabel();
                target.ReleaseMouse();
                evt.StopPropagation();
            }
        }
    }
}
