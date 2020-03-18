using Unity.Kinematica.Editor.TimelineUtils;
using Unity.Kinematica.Temporary;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions.Comparers;
using UnityEngine.UIElements;

namespace Unity.Kinematica.Editor
{
    internal partial class TimeRuler : VisualElement
    {
        public readonly TimelineWidget m_TimelineWidget;
        TimelineWidget.DrawInfo m_DrawInfo;

        readonly PanManipulator m_PanManipulator;
        readonly ZoomManipulator m_ZoomManipulator;

        VisualElement m_TimeArea;
        IMGUIContainer m_IMGUIContainer;

        int m_SampleRate = 60;

        public int SampleRate
        {
            get { return m_SampleRate; }
            set
            {
                if (m_SampleRate != value)
                {
                    m_SampleRate = value;
                    m_TickHandler.SetTickModulosForFrameRate(m_SampleRate);
                }
            }
        }

        public TimeRuler(Timeline timeline, PanManipulator panManipulator, ZoomManipulator zoomManipulator)
        {
            m_TimelineWidget = new TimelineWidget();
            m_DrawInfo.scale = 1f;
            m_DrawInfo.inverseScale = 1.0f / m_DrawInfo.scale;

            m_PanManipulator = panManipulator;
            m_ZoomManipulator = zoomManipulator;
            m_TimeArea = timeline.m_TimelineScrollableArea;

            m_IMGUIContainer = new IMGUIContainer(() =>
            {
                GUILayout.Space(5);
                var lastRect = GUILayoutUtility.GetLastRect();
                Rect rt = GUILayoutUtility.GetRect(lastRect.width, 36);

                GUI.BeginGroup(rt);

                Rect rect = new Rect(0, 0, rt.width, rt.height);

                if (rect.width > 0 && rect.height > 0)
                {
                    if (!FloatComparer.s_ComparerWithDefaultTolerance.Equals(m_DrawInfo.rangeStart, m_TimelineWidget.RangeStart) ||
                        !FloatComparer.s_ComparerWithDefaultTolerance.Equals(m_DrawInfo.rangeWidth, m_TimelineWidget.RangeWidth))
                    {
                        m_DrawInfo.rangeStart = m_TimelineWidget.RangeStart;
                        m_DrawInfo.rangeWidth = m_TimelineWidget.RangeWidth;
                        m_DrawInfo.scale = m_TimelineWidget.RangeWidth;
                        m_DrawInfo.inverseScale = 1.0f / m_DrawInfo.scale;
                    }
                    else
                    {
                        if (timeline.TimelineUnits != TimelineViewMode.seconds)
                        {
                            DrawRuler(m_IMGUIContainer.layout, m_SampleRate, timeline.TimelineUnits);
                        }
                        else
                        {
                            //TODO - do we want to use the above ruler for all display modes?
                            TimelineWidget.DrawNotations(rect, m_DrawInfo, m_SampleRate, 20f, true, timeline.TimelineUnits);
                        }
                    }

                    ForwardEvents(rect);
                }

                GUI.EndGroup();
            });

            Add(m_IMGUIContainer);

            m_TickHandler = new TickHandler();
            float[] modulos =
            {
                0.0000001f, 0.0000005f, 0.000001f, 0.000005f, 0.00001f, 0.00005f, 0.0001f, 0.0005f,
                0.001f, 0.005f, 0.01f, 0.05f, 0.1f, 0.5f, 1, 5, 10, 50, 100, 500,
                1000, 5000, 10000, 50000, 100000, 500000, 1000000, 5000000, 10000000
            };
            m_TickHandler.SetTickModulos(modulos);

            AddToClassList(".timeRuler");
        }

        Vector2 m_Start;
        Vector2 m_Last;

        bool m_Active = false;

        void ForwardEvents(Rect rect)
        {
            var e = Event.current;
            if (rect.Contains(e.mousePosition))
            {
                if (e.type == EventType.ScrollWheel)
                {
                    EditorGUIUtility.AddCursorRect(rect, MouseCursor.Zoom);

                    float wheelDelta = e.alt ? -e.delta.x * 0.1f : e.delta.y;
                    float scale = Mathf.Clamp(1f - wheelDelta * ZoomManipulator.k_ZoomStep, 0.05f, 100000.0f);
                    Vector2 zoomCenter = this.ChangeCoordinatesTo(m_TimeArea, e.mousePosition);
                    m_ZoomManipulator.Zoom(zoomCenter, scale);

                    e.Use();
                }

                if (e.type == EventType.MouseDown && e.button == (int)MouseButton.MiddleMouse)
                {
                    m_Start = m_Last = e.mousePosition;
                    m_Active = true;
                    e.Use();
                }
                else if (m_Active)
                {
                    if (e.type == EventType.MouseUp)
                    {
                        m_Active = false;
                        e.Use();
                        return;
                    }

                    EditorGUIUtility.AddCursorRect(rect, MouseCursor.Pan);
                    if (e.type == EventType.MouseDrag)//|| e.type == EventType.MouseDown || e.type == EventType.MouseUp)
                    {
                        m_PanManipulator.Pan(m_Last, e.mousePosition);
                        m_Last = e.mousePosition;

                        e.Use();
                    }
                }
            }
        }
    }
}
