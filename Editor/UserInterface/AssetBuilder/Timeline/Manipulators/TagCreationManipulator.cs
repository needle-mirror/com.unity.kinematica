using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions.Comparers;
using UnityEngine.UIElements;

namespace Unity.Kinematica.Editor
{
    class TagCreationManipulator : MouseManipulator
    {
        const float k_MinDistance = 5f;

        Timeline m_Timeline;

        VisualElement m_Element;

        Label m_ManipulateStartLabel;
        Label m_ManipulateEndLabel;

        Vector3 m_MouseDownLocalPos;
        Vector3 m_MouseDownWorldPos;

        float m_MouseDownTime;

        bool m_Active;

        public TagCreationManipulator(Timeline timeline)
        {
            m_Timeline = timeline;
            activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse });

            m_Element = new VisualElement();
            m_Element.AddToClassList("creationManipulator");
            m_Element.name = "tagCreator";
            m_Timeline.m_ScrollViewContainer.Add(m_Element);
            m_Element.style.visibility = Visibility.Hidden;
            m_Element.style.height = 20f;

            m_ManipulateStartLabel = new Label();
            m_ManipulateStartLabel.AddToClassList("tagManipulateStartLabel");
            m_ManipulateStartLabel.AddToClassList("tagManipulateLabel");

            m_ManipulateEndLabel = new Label();
            m_ManipulateEndLabel.AddToClassList("tagManipulateEndLabel");
            m_ManipulateEndLabel.AddToClassList("tagManipulateLabel");

            m_Element.Add(m_ManipulateStartLabel);
            m_Element.Add(m_ManipulateEndLabel);
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<MouseDownEvent>(OnMouseDownEvent);
            target.RegisterCallback<MouseMoveEvent>(OnMouseMoveEvent);
            target.RegisterCallback<MouseUpEvent>(OnMouseUpEvent);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.RegisterCallback<MouseDownEvent>(OnMouseDownEvent);
            target.RegisterCallback<MouseMoveEvent>(OnMouseMoveEvent);
            target.RegisterCallback<MouseUpEvent>(OnMouseUpEvent);
        }

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

            if (!m_Timeline.CanAddTag())
            {
                return;
            }

            if (target == null)
            {
                m_Active = false;
                return;
            }

            m_MouseDownWorldPos = evt.mousePosition;
            m_MouseDownTime = m_Timeline.WorldPositionToTime(m_MouseDownWorldPos.x);
            m_MouseDownLocalPos = m_Timeline.m_ScrollViewContainer.WorldToLocal(evt.mousePosition);
            m_MouseDownLocalPos.y = m_Timeline.GetFreeVerticalHeight();
            m_Element.transform.position = m_MouseDownLocalPos;

            m_Active = true;

            target.CaptureMouse();
            evt.StopPropagation();
        }

        void OnMouseMoveEvent(MouseMoveEvent evt)
        {
            if (!m_Active || !target.HasMouseCapture() || EditorApplication.isPlaying)
            {
                return;
            }

            if (Math.Abs(evt.mousePosition.x - m_MouseDownWorldPos.x) < k_MinDistance)
            {
                m_Element.style.visibility = Visibility.Hidden;
                m_ManipulateStartLabel.style.visibility = Visibility.Hidden;
                m_ManipulateEndLabel.style.visibility = Visibility.Hidden;
                m_Timeline.HideGuidelines();
                return;
            }

            m_Element.style.visibility = Visibility.Visible;

            float mouseMoveTime = m_Timeline.WorldPositionToTime(evt.mousePosition.x);
            if (m_Timeline.CanPreview())
            {
                m_Timeline.SetActiveTime(mouseMoveTime);
            }

            float startTime = Math.Min(m_MouseDownTime, mouseMoveTime);
            float endTime = Math.Max(m_MouseDownTime, mouseMoveTime);

            Vector2 localPos = m_Timeline.m_ScrollViewContainer.WorldToLocal(evt.mousePosition);

            Vector3 elementPosition = m_MouseDownTime < endTime ? m_MouseDownLocalPos : new Vector3(localPos.x, m_MouseDownLocalPos.y, m_MouseDownLocalPos.z);
            m_Element.transform.position = elementPosition;
            float width = Math.Abs(localPos.x - m_MouseDownLocalPos.x);
            m_Element.style.width = width;

            if (m_Timeline.CanPreview())
            {
                if (FloatComparer.s_ComparerWithDefaultTolerance.Equals(mouseMoveTime, startTime))
                {
                    m_Timeline.ShowEndGuideline(endTime);
                }
                else
                {
                    m_Timeline.ShowStartGuideline(startTime);
                }
            }
            else
            {
                m_Timeline.ShowGuidelines(startTime, endTime);
            }

            m_ManipulateStartLabel.text = TimelineUtility.GetTimeString(m_Timeline.ViewMode, startTime, (int)m_Timeline.TaggedClip.SampleRate);
            m_ManipulateStartLabel.style.visibility = Visibility.Visible;
            float estimatedTextSize = TimelineUtility.EstimateTextSize(m_ManipulateStartLabel);
            float controlWidth = Math.Max(float.IsNaN(estimatedTextSize) ? m_ManipulateStartLabel.layout.width : estimatedTextSize, 8) + 6;
            m_ManipulateStartLabel.style.left = -controlWidth;

            m_ManipulateEndLabel.text = TimelineUtility.GetTimeString(m_Timeline.ViewMode, endTime, (int)m_Timeline.TaggedClip.SampleRate);
            m_ManipulateEndLabel.style.left = width + 8f;
            m_ManipulateEndLabel.style.visibility = Visibility.Visible;
        }

        void OnMouseUpEvent(MouseUpEvent evt)
        {
            if (!m_Active || !target.HasMouseCapture() || !CanStopManipulation(evt))
            {
                return;
            }

            target.ReleaseMouse();
            evt.StopPropagation();

            float distanceFromMouseDown = Math.Abs(evt.mousePosition.x - m_MouseDownWorldPos.x);

            if (distanceFromMouseDown >= k_MinDistance && !EditorApplication.isPlaying)
            {
                float mouseUpTime = m_Timeline.WorldPositionToTime(evt.mousePosition.x);

                float startTime = Math.Min(m_MouseDownTime, mouseUpTime);
                float duration = Math.Max(m_MouseDownTime, mouseUpTime) - startTime;

                var menu = new GenericMenu();

                foreach (Type tagType in TagAttribute.GetVisibleTypesInInspector())
                {
                    menu.AddItem(new GUIContent($"{TagAttribute.GetDescription(tagType)}"),
                        false,
                        () => { m_Timeline.OnAddTagSelection(tagType, startTime, duration); });
                }

                menu.DropDown(new Rect(evt.mousePosition, Vector2.zero));
            }
            else
            {
                m_Timeline.ReSelectCurrentClip();
            }

            m_Timeline.HideGuidelines();
            m_ManipulateStartLabel.style.visibility = Visibility.Hidden;
            m_ManipulateEndLabel.style.visibility = Visibility.Hidden;
            m_Element.style.visibility = Visibility.Hidden;
            m_Active = false;
        }
    }
}
