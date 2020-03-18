using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Unity.SnapshotDebugger.Editor
{
    internal class DebuggerWindow : EditorWindow
    {
        [MenuItem(("Window/Analysis/Snapshot Debugger"))]
        public static void ShowWindow()
        {
            GetWindow<DebuggerWindow>("Debug recorder");
        }

        TimelineWidget m_DebuggerTimeline;
        List<ITimelineDebugDrawer> m_CustomDrawers;

        public void OnEnable()
        {
            m_DebuggerTimeline = new TimelineWidget();
            m_CustomDrawers = new List<ITimelineDebugDrawer>();

            InitializeCustomDrawers();
        }

        void InitializeCustomDrawers()
        {
            var drawerTypes = TypeCache.GetTypesDerivedFrom<ITimelineDebugDrawer>();
            foreach (var type in drawerTypes)
            {
                var drawer = Activator.CreateInstance(type);
                m_CustomDrawers.Add(drawer as ITimelineDebugDrawer);
            }
        }

        public void OnDisable()
        {
            m_CustomDrawers.Clear();
        }

        public void OnGUI()
        {
            var debugger = Debugger.instance;

            GUI.enabled = !Application.isPlaying;

            debugger.state = (Debugger.State)
                EditorGUILayout.EnumPopup(new GUIContent("Recorder", "Debugger mode."), debugger.state);

            GUI.enabled = true;

            debugger.capacityInSeconds =
                EditorGUILayout.FloatField(new GUIContent("Capacity in seconds", "Maximum time recorder will keep."),
                    debugger.capacityInSeconds);

            int memorySize = debugger.memorySize;

            EditorGUILayout.LabelField(
                "Memory size", MemorySize.ToString(memorySize));

            m_DebuggerTimeline.SelectedRangeStart = debugger.startTimeInSeconds;
            m_DebuggerTimeline.SelectedRangeEnd = debugger.endTimeInSeconds;

            GUILayout.Space(5);

            DrawDebuggerTimeline();

            GUILayout.Space(5);

            foreach (var aggregate in Debugger.registry.aggregates)
            {
                var gameObject = aggregate.gameObject;

                var label = string.Empty;

                foreach (var provider in aggregate.providers)
                {
                    label += $" {provider.GetType().Name} [{(int)provider.identifier}]";
                }

                EditorGUILayout.LabelField(
                    $"{gameObject.name} [{(int)aggregate.identifier}]", label);
            }

            Repaint();
        }

        Color m_BackgroundColor = Color.white;

        void DrawDebuggerTimeline()
        {
            EditorGUILayout.LabelField("Debugger Timeline");

            var lastRect = GUILayoutUtility.GetLastRect();
            Rect rt = GUILayoutUtility.GetRect(lastRect.width, 54);
            foreach (var drawer in m_CustomDrawers)
            {
                rt.height += drawer.GetHeight();
            }

            GUI.BeginGroup(rt);
            {
                Rect rect = new Rect(0, 0, rt.width, rt.height);

                if (m_BackgroundColor == Color.white)
                {
                    m_BackgroundColor = EditorGUIUtility.isProSkin ? new Color(0.12f, 0.12f, 0.12f, 0.78f) : new Color(0.66f, 0.66f, 0.66f, 0.78f);
                }

                GUI.color = m_BackgroundColor;
                GUI.DrawTexture(rect, EditorGUIUtility.whiteTexture);

                TimelineWidget.DrawInfo drawInfo = new TimelineWidget.DrawInfo();

                drawInfo.rangeStart = m_DebuggerTimeline.RangeStart;
                drawInfo.rangeWidth = m_DebuggerTimeline.RangeWidth;

                drawInfo.scale = m_DebuggerTimeline.RangeWidth;
                drawInfo.inverseScale = 1.0f / drawInfo.scale;

                foreach (var drawer in m_CustomDrawers)
                {
                    drawer.Draw(rect, drawInfo);
                }

                TimelineWidget.DrawNotations(rect, drawInfo);

                m_DebuggerTimeline.Update(rect);

                var debugger = Debugger.instance;

                if (Application.isPlaying)
                {
                    TimelineWidget.DrawRange(rect, drawInfo,
                        m_DebuggerTimeline.SelectedRangeStart, m_DebuggerTimeline.SelectedRangeEnd,
                        new Color(0.0f, 0.25f, 0.5f, 0.4f));

                    Color cursorColor = new Color(0.5f, 0.5f, 0.0f, 1.0f);

                    TimelineWidget.DrawLineAtTime(
                        rect, drawInfo, debugger.rewindTime,
                        cursorColor);
                }

                foreach (var drawer in m_CustomDrawers)
                {
                    drawer.DrawTooltip();
                }

                if (debugger.IsState(Debugger.State.Record))
                {
                    if (rect.Contains(Event.current.mousePosition))
                    {
                        var e = Event.current;

                        if (e.button == 0)
                        {
                            if (e.type == EventType.MouseDrag || e.type == EventType.MouseDown || e.type == EventType.MouseUp)
                            {
                                e.Use();

                                m_DebuggerTimeline.Repaint = true;
                                EditorGUIUtility.AddCursorRect(rect, MouseCursor.Arrow);

                                float currentTime =
                                    m_DebuggerTimeline.GetCurrentPositionFromMouse(
                                        rect, Event.current.mousePosition);

                                float start = m_DebuggerTimeline.SelectedRangeStart;
                                float end = m_DebuggerTimeline.SelectedRangeEnd;

                                debugger.rewindTime = Mathf.Clamp(currentTime, start, end);

                                debugger.rewind = currentTime <= end;
                            }
                        }
                    }
                }

                GUI.EndGroup();
            }

            GUILayout.Space(5);
        }
    }
}
