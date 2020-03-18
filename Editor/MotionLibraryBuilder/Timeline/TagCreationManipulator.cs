using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Kinematica.Editor
{
    class TagCreationManipulator : MouseManipulator
    {
        const float k_MinDistance = 5f;

        Timeline m_Timeline;

        VisualElement m_Element;

        Vector3 m_MouseDownLocalPos;

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

            var startWorldPos = m_Timeline.m_ScrollViewContainer.LocalToWorld(m_MouseDownLocalPos);

            if (evt.mousePosition.x < startWorldPos.x || evt.mousePosition.x - startWorldPos.x < k_MinDistance)
            {
                m_Element.style.visibility = Visibility.Hidden;
                return;
            }

            m_Element.style.height = 20f;

            m_Element.style.visibility = Visibility.Visible;
            var localPos = m_Timeline.m_ScrollViewContainer.WorldToLocal(evt.mousePosition);

            m_Element.style.width = localPos.x - m_MouseDownLocalPos.x;

            float time = m_Timeline.WorldPositionToTime(evt.mousePosition.x);
            if (m_Timeline.CanPreview())
            {
                m_Timeline.SetActiveTime(time);
            }
        }

        void OnMouseUpEvent(MouseUpEvent evt)
        {
            if (!m_Active || !target.HasMouseCapture() || !CanStopManipulation(evt))
            {
                return;
            }

            target.ReleaseMouse();
            evt.StopPropagation();

            var startWorldPos = m_Timeline.m_ScrollViewContainer.LocalToWorld(m_MouseDownLocalPos);

            if (evt.mousePosition.x - startWorldPos.x >= k_MinDistance && !EditorApplication.isPlaying)
            {
                float startTime = m_Timeline.WorldPositionToTime(startWorldPos.x);
                float duration = m_Timeline.WorldPositionToTime(evt.mousePosition.x) - startTime;

                var menu = new GenericMenu();

                foreach (var tagType in TagAttribute.GetVisibleTypesInInspector())
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

            m_Element.style.visibility = Visibility.Hidden;
            m_Active = false;
        }
    }
}
