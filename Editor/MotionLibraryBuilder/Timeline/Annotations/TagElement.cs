using System;
using System.Collections.Generic;
using System.Reflection;

using Unity.SnapshotDebugger.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

using Random = System.Random;
using ColorUtility = Unity.SnapshotDebugger.ColorUtility;

namespace Unity.Kinematica.Editor
{
    class TagElement : TimeRangeElement
    {
        public readonly TagAnnotation m_Tag;
        readonly TaggedAnimationClip m_Clip;

        Timeline m_Timeline;

        readonly VisualElement m_ValueColors;

        public TagElement(Timeline timeline, TaggedAnimationClip clip, TagAnnotation tag, float widthMultiplier) : base(widthMultiplier)
        {
            m_Timeline = timeline;
            m_Clip = clip;
            m_Tag = tag;
            focusable = true;

            AddToClassList("clipTagRoot");
            m_Contents.AddToClassList("clipTag");
            m_Contents.style.backgroundColor = TagAttribute.GetColor(tag.Type);

            VisualElement leftHandle = CreateHandle(TagManipulator.Mode.StartTime);
            m_Contents.Insert(0, leftHandle);

            m_LabelContainer.AddManipulator(new TagManipulator(this, TagManipulator.Mode.Body));
            m_ValueColors = new VisualElement();
            m_ValueColors.AddToClassList("tagValueColorHash");
            Add(m_ValueColors);

            m_Label.text = string.IsNullOrEmpty(tag.name) ? tag.Name : tag.name;

            VisualElement rightHandle = CreateHandle(TagManipulator.Mode.FinishTime);
            m_Contents.Add(rightHandle);

            var contextMenuManipulator = new ContextualMenuManipulator(OpenTagRemoveMenu);
            this.AddManipulator(contextMenuManipulator);

            m_Tag.Changed += Resize;
            m_Tag.Changed += m_Timeline.ReloadMetrics;

            Resize(widthMultiplier);

            Undo.undoRedoPerformed += OnUndoRedoPerformed;
        }

        void OnUndoRedoPerformed()
        {
            RecomputeColorHash();
        }

        internal const string k_SelectedStyleClass = "clipTag--checked";

        public void Select(bool multi)
        {
            bool increment = Selection.activeObject != m_Timeline.SelectionContainer;

            m_Timeline.Select(this, multi);
            m_Contents.AddToClassList(k_SelectedStyleClass);
            if (increment)
            {
                // This prevents an Undo of a tag value change from also changing the selection
                Undo.IncrementCurrentGroup();
            }
        }

        public override void Unselect()
        {
            m_Contents.RemoveFromClassList(k_SelectedStyleClass);
        }

        public override System.Object Object
        {
            get { return m_Tag; }
        }

        void OpenTagRemoveMenu(ContextualMenuPopulateEvent evt)
        {
            evt.menu.AppendAction($"Remove : {m_Tag.Name}",
                action => { m_Clip.RemoveTag(m_Tag); },
                EditorApplication.isPlaying ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);

            evt.StopPropagation();
        }

        protected override void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            base.OnDetachFromPanel(evt);

            m_Tag.Changed -= Resize;
            m_Tag.Changed -= m_Timeline.ReloadMetrics;
        }

        class TagManipulator : MouseManipulator
        {
            public enum Mode { StartTime, FinishTime, Body };

            Mode m_Mode;

            TagElement m_Parent;

            Vector2 m_MouseDownPosition;
            float m_OffsetAtMouseDown;
            bool m_Active;

            public TagManipulator(TagElement parent, Mode mode)
            {
                m_Parent = parent;
                m_Mode = mode;

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
                m_MouseDownPosition = evt.mousePosition;
                m_OffsetAtMouseDown = m_Parent.PositionToTime(m_MouseDownPosition.x) - m_Parent.m_Tag.startTime;
                target.CaptureMouse();
                evt.StopPropagation();
            }

            void OnMouseMoveEvent(MouseMoveEvent evt)
            {
                var taggedAnimationClip = m_Parent.m_Clip;
                if (!m_Active || !target.HasMouseCapture() || !taggedAnimationClip.Valid || EditorApplication.isPlaying)
                {
                    return;
                }

                m_Parent.Select(evt.ctrlKey);

                float framerate = taggedAnimationClip.SampleRate;
                float newTime = m_Parent.PositionToTime(evt.mousePosition.x);

                if (m_Parent.m_Timeline.TimelineUnits == TimelineViewMode.frames)
                {
                    newTime = (float)TimelineUtility.RoundToFrame(newTime, framerate);
                }

                var tag = m_Parent.m_Tag;

                Asset asset = m_Parent.m_Clip.Asset;

                switch (m_Mode)
                {
                    case Mode.StartTime:
                        float endTime = tag.startTime + tag.duration;
                        Undo.RecordObject(asset, "Drag Tag start");

                        float delta = newTime - tag.startTime;
                        if (tag.duration - delta < m_Parent.MinTagDuration)
                        {
                            tag.startTime = endTime - m_Parent.MinTagDuration;
                            tag.duration = m_Parent.MinTagDuration;
                        }
                        else
                        {
                            tag.startTime = newTime;
                            tag.duration = endTime - tag.startTime;
                        }
                        break;

                    case Mode.FinishTime:
                        Undo.RecordObject(asset, "Drag Tag end");
                        float newDuration = newTime - tag.startTime;
                        if (newDuration <= m_Parent.MinTagDuration)
                        {
                            tag.duration = m_Parent.MinTagDuration;
                        }
                        else
                        {
                            tag.duration = newDuration;
                        }
                        break;

                    case Mode.Body:
                        Undo.RecordObject(asset, "Drag Tag");

                        if (!m_Parent.ContainsPoint(m_Parent.WorldToLocal(evt.mousePosition)) &&
                            Math.Abs(m_MouseDownPosition.y - evt.mousePosition.y) >= (m_Parent.layout.height / 2f) + 5f)
                        {
                            bool draggingUp = evt.mousePosition.y < m_MouseDownPosition.y;
                            if (m_Parent.m_Timeline.ReorderTimelineElements(m_Parent, draggingUp ? -1 : 1))
                            {
                                m_MouseDownPosition = evt.mousePosition;
                            }

                            return;
                        }

                        if (m_Parent.m_Timeline.TimelineUnits == TimelineViewMode.frames)
                        {
                            newTime = (float)TimelineUtility.RoundToFrame(newTime - m_OffsetAtMouseDown, framerate);
                        }
                        else
                        {
                            newTime -= m_OffsetAtMouseDown;
                        }

                        tag.startTime = newTime;

                        break;
                }

                m_Parent.Resize();
                m_Parent.m_Timeline.SendTagModified();

                evt.StopPropagation();
            }

            // TODO - read this value from the style
            internal const float k_TagHandleMinWidth = 12f;

            void OnMouseUpEvent(MouseUpEvent evt)
            {
                if (!m_Active || !target.HasMouseCapture() || !CanStopManipulation(evt))
                {
                    return;
                }

                m_Parent.Select(evt.ctrlKey);

                m_Active = false;
                target.ReleaseMouse();
                evt.StopPropagation();
            }
        }

        readonly List<VisualElement> m_Handles = new List<VisualElement>();

        VisualElement CreateHandle(TagManipulator.Mode m)
        {
            var handle = new VisualElement();
            handle.AddManipulator(new TagManipulator(this, m));
            handle.AddToClassList("clipTagDragHandle");
            m_Handles.Add(handle);
            return handle;
        }

        internal float PositionToTime(float x)
        {
            return m_Timeline.WorldPositionToTime(x);
        }

        protected override void Resize()
        {
            style.left = (m_Tag.startTime + m_Timeline.SecondsBeforeZero) * m_WidthMultiplier;
            style.width = (m_Tag.duration) * m_WidthMultiplier;
            m_Label.text = string.IsNullOrEmpty(m_Tag.name) ? m_Tag.Name : m_Tag.name;
            RecomputeColorHash();
        }

        public void RecomputeColorHash()
        {
            int hash = new Random(m_Tag.payload.GetHashedData()).Next();

            Color colorFromValueHash = ColorUtility.FromHtmlString("#"  + Convert.ToString(hash, 16));

            var backgroundColor = m_Contents.style.backgroundColor.value;
            backgroundColor.r = (backgroundColor.r + colorFromValueHash.r) / 2;
            backgroundColor.g = (backgroundColor.g + colorFromValueHash.g) / 2;
            backgroundColor.b = (backgroundColor.b + colorFromValueHash.b) / 2;

            m_ValueColors.style.backgroundColor = backgroundColor;
        }

        internal float MinTagDuration
        {
            get { return 2 * TagManipulator.k_TagHandleMinWidth / m_WidthMultiplier; }
        }

        public void Split(float time)
        {
            if (time < m_Tag.startTime)
            {
                return;
            }

            if (time > m_Tag.startTime + m_Tag.duration)
            {
                return;
            }

            //Undo.RecordObject(m_Target, "Splice tag");

            //var newTag = new Tag(m_Tag);
            //newTag.startTime = m_Tag.startTime;
            //newTag.duration = time - newTag.startTime;

            //m_Tag.duration -= time - m_Tag.startTime;
            //m_Tag.startTime = time;

            //m_Clip.AddTag(newTag);
            //EditorUtility.SetDirty(m_Target);
        }

        protected override void ExecuteDefaultActionAtTarget(EventBase evt)
        {
            base.ExecuteDefaultActionAtTarget(evt);

            if (evt is KeyDownEvent keyDownEvt)
            {
                if (keyDownEvt.keyCode == KeyCode.Delete && !EditorApplication.isPlaying)
                {
                    m_Timeline.DeleteSelection();
                }

                if (keyDownEvt.keyCode == KeyCode.F)
                {
                    m_Timeline.SetTimeRange(m_Tag.startTime, m_Tag.startTime + m_Tag.duration);
                }
            }
        }
    }
}
