using System;
using System.Collections.Generic;
using System.Linq;
using Unity.SnapshotDebugger.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions.Comparers;
using UnityEngine.UIElements;

namespace Unity.Kinematica.Editor
{
    class MarkerTimelineElement : VisualElement, ITimelineElement, INotifyValueChanged<float>
    {
        const string k_MarkerStyleClass = "marker";
        const string k_MultiMarkerStyleClass = "multi-marker";
        const string k_MarkerSelectedStyleClass = k_MarkerStyleClass + "-selected";

        public readonly MarkerAnnotation marker;

        float m_TimeScale;

        AnnotationsTrack m_Track;

        VisualElement m_MultipleMarkerIcon;

        public MarkerTimelineElement(MarkerAnnotation marker, AnnotationsTrack track)
        {
            AddToClassList(k_MarkerStyleClass);

            m_Track = track;
            this.marker = marker;
            focusable = true;

            this.AddManipulator(new MarkerDragManipulator(this));

            marker.Changed += Reposition;
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            style.backgroundColor = MarkerAttribute.GetColor(marker.payload.Type);


            var contextManipulator = new ContextualMenuManipulator((obj) =>

            {
                SelectMarkerMenu(obj);
                obj.menu.AppendSeparator();
            });

            m_MultipleMarkerIcon = new VisualElement();
            m_MultipleMarkerIcon.AddToClassList(k_MultiMarkerStyleClass);
            m_MultipleMarkerIcon.style.visibility = Visibility.Hidden;
            Add(m_MultipleMarkerIcon);

            this.AddManipulator(contextManipulator);
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
        }

        public void Reposition(float timeScale)
        {
            m_TimeScale = timeScale;
            Reposition();
        }

        internal event Action<float, float> MarkerTimelineElementMoved;

        float m_PreviousTime;
        public void Reposition()
        {
            style.display = DisplayStyle.Flex;
            var clip = m_Track.m_TaggedClip;
            if (clip == null)
            {
                return;
            }

            float space = (clip.DurationInSeconds * m_TimeScale) / clip.NumFrames;
            style.width = space;
            float leftBefore = style.left.value.value;
            style.left = (value + m_Track.m_Owner.SecondsBeforeZero) * m_TimeScale;
            if (!FloatComparer.s_ComparerWithDefaultTolerance.Equals(leftBefore, style.left.value.value))
            {
                MarkerTimelineElementMoved?.Invoke(m_PreviousTime, value);
                m_PreviousTime = value;
            }
        }

        public void SetValueWithoutNotify(float value)
        {
            Undo.RecordObject(m_Track.m_TaggedClip.Asset, "Drag Marker time");
            marker.timeInSeconds = value;
            Reposition();
        }

        public float value
        {
            get
            {
                return marker.timeInSeconds;
            }
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
            RemoveFromClassList(k_MarkerSelectedStyleClass);
            m_Selected = false;
        }

        public System.Object Object
        {
            get { return marker; }
        }

        bool m_Selected = false;

        internal static void SelectMarkerElement(MarkerTimelineElement markerElement, bool multi)
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
            markerElement.AddToClassList(k_MarkerSelectedStyleClass);
            markerElement.m_Selected = true;
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

        class MarkerDragManipulator : MouseManipulator
        {
            bool m_Active;

            MarkerTimelineElement m_Parent;

            public MarkerDragManipulator(MarkerTimelineElement parent)
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

                if (!m_Parent.m_Selected)
                {
                    SelectMarkerElement(m_Parent, false);
                }

                float framerate = m_Parent.m_Track.m_TaggedClip.SampleRate;
                var newTime = (float)TimelineUtility.RoundToFrame(m_Parent.m_Track.m_Owner.WorldPositionToTime(evt.mousePosition.x), framerate);
                if (!FloatComparer.s_ComparerWithDefaultTolerance.Equals(m_Parent.value, newTime))
                {
                    m_MarkerDragged = true;
                    m_Parent.value = newTime;
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
                target.ReleaseMouse();
                evt.StopPropagation();
            }
        }
    }
}
