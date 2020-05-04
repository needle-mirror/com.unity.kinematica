using System;
using System.Collections.Generic;

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

        Label m_ManipulateStartLabel;
        Label m_ManipulateEndLabel;

        readonly Color m_BackgroundColor;
        const float k_BackgroundAlpha = .5f;

        public TagElement(Track track, TaggedAnimationClip clip, TagAnnotation tag) : base(track)
        {
            m_Clip = clip;
            m_Tag = tag;
            focusable = true;

            AddToClassList("clipTagRoot");

            m_ManipulateStartLabel = new Label();
            m_ManipulateStartLabel.AddToClassList("tagManipulateStartLabel");
            m_ManipulateStartLabel.AddToClassList("tagManipulateLabel");

            m_ManipulateEndLabel = new Label();
            m_ManipulateEndLabel.AddToClassList("tagManipulateEndLabel");
            m_ManipulateEndLabel.AddToClassList("tagManipulateLabel");

            m_BackgroundColor = AnnotationAttribute.GetColor(m_Tag.Type);
            var background = m_BackgroundColor;
            background.a = k_BackgroundAlpha;
            style.backgroundColor = background;
            int hash = new Random(m_Tag.payload.GetHashedData()).Next();
            Color colorFromValueHash = ColorUtility.FromHtmlString("#"  + Convert.ToString(hash, 16));
            Color borderColor = background;
            borderColor.r = (borderColor.r + colorFromValueHash.r) / 2;
            borderColor.g = (borderColor.g + colorFromValueHash.g) / 2;
            borderColor.b = (borderColor.b + colorFromValueHash.b) / 2;
            style.borderLeftColor = borderColor;
            style.borderBottomColor = borderColor;
            style.borderRightColor = borderColor;

            VisualElement startHandle = CreateHandle(TagManipulator.Mode.StartTime);
            startHandle.style.left = -4;
            Insert(0, startHandle);
            startHandle.Add(m_ManipulateStartLabel);

            m_LabelContainer.AddManipulator(new TagManipulator(this, TagManipulator.Mode.Body));
            m_Label.text = string.IsNullOrEmpty(m_Tag.name) ? m_Tag.Name : m_Tag.name;

            VisualElement endHandle = CreateHandle(TagManipulator.Mode.FinishTime);
            endHandle.style.left = 4;
            Add(endHandle);
            endHandle.Add(m_ManipulateEndLabel);

            var contextMenuManipulator = new ContextualMenuManipulator(OpenTagRemoveMenu);
            this.AddManipulator(contextMenuManipulator);

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            m_Tag.Changed += Resize;
            m_Tag.Changed += Timeline.ReloadMetrics;

            Resize();
        }

        public void Select(bool multi)
        {
            bool increment = Selection.activeObject != Timeline.SelectionContainer;

            Timeline.Select(this, multi);
            style.backgroundColor = m_BackgroundColor;

            if (increment)
            {
                // This prevents an Undo of a tag value change from also changing the selection
                Undo.IncrementCurrentGroup();
            }
        }

        public override void Unselect()
        {
            Color background = m_BackgroundColor;
            background.a = k_BackgroundAlpha;
            style.backgroundColor = background;
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
            m_Tag.Changed -= Timeline.ReloadMetrics;
        }

        class TagManipulator : MouseManipulator
        {
            // TODO - read this value from the style
            internal const float k_TagHandleMinWidth = 12f;
            const float k_MinDistanceForTagCreation = 1.5f;

            public enum Mode { StartTime, FinishTime, Body, None };

            Mode m_Mode;
            Mode m_OverrideMode;

            TagElement m_TagElement;

            Vector2 m_MouseDownPosition;
            Vector2 m_MousePreviousPosition;
            float m_OffsetAtMouseDown;
            bool m_Active;

            public TagManipulator(TagElement tagElement, Mode mode)
            {
                m_TagElement = tagElement;
                m_Mode = mode;
                m_OverrideMode = Mode.None;

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

                m_TagElement.Select(evt.ctrlKey);

                m_Active = true;
                m_MouseDownPosition = evt.mousePosition;
                m_MousePreviousPosition = m_MouseDownPosition;
                m_OffsetAtMouseDown = m_TagElement.PositionToTime(m_MouseDownPosition.x) - m_TagElement.m_Tag.startTime;

                m_TagElement.m_ManipulateStartLabel.style.visibility = Visibility.Visible;
                m_TagElement.m_ManipulateEndLabel.style.visibility = Visibility.Visible;

                target.CaptureMouse();
                evt.StopPropagation();

                UpdateManipulatorLabels();
            }

            void OnMouseMoveEvent(MouseMoveEvent evt)
            {
                var taggedAnimationClip = m_TagElement.m_Clip;
                if (!m_Active || !target.HasMouseCapture() || !taggedAnimationClip.Valid || EditorApplication.isPlaying)
                {
                    return;
                }

                if (Math.Abs(m_MousePreviousPosition.x - evt.mousePosition.x) < k_MinDistanceForTagCreation)
                {
                    if (Math.Abs(m_MouseDownPosition.y - evt.mousePosition.y) >= k_MinDistanceForTagCreation)
                    {
                        OnMouseMoveReorderEvent(evt);
                    }

                    return;
                }

                m_MousePreviousPosition = evt.mousePosition;

                float framerate = taggedAnimationClip.SampleRate;
                float newTime = m_TagElement.PositionToTime(evt.mousePosition.x);

                if (m_TagElement.Timeline.TimelineUnits == TimelineViewMode.frames)
                {
                    newTime = (float)TimelineUtility.RoundToFrame(newTime, framerate);
                }

                var tag = m_TagElement.m_Tag;

                Asset asset = m_TagElement.m_Clip.Asset;

                Mode mode = m_Mode;
                if (m_OverrideMode == Mode.None)
                {
                    float mousePos = evt.mousePosition.x;
                    float fromStart = Math.Abs(mousePos - m_TagElement.worldBound.x);
                    float fromEnd = Math.Abs(mousePos - m_TagElement.worldBound.xMax);
                    // If the tag element is this small it will be too difficult to accurately grab the center and even accurately pick either end
                    // we'll figure out which side of the tag is closest and assume the user meant to click that side.
                    if (m_TagElement.layout.width <= 14f)
                    {
                        if (fromStart <= fromEnd)
                        {
                            mode = Mode.StartTime;
                        }
                        else
                        {
                            mode = Mode.FinishTime;
                        }

                        m_OverrideMode = mode;
                    }
                }
                else
                {
                    mode = m_OverrideMode;
                }

                switch (mode)
                {
                    case Mode.StartTime:
                        float endTime = tag.startTime + tag.duration;
                        Undo.RecordObject(asset, "Drag Tag start");

                        float delta = newTime - tag.startTime;
                        if (tag.duration - delta < m_TagElement.MinTagDuration)
                        {
                            tag.startTime = endTime - m_TagElement.MinTagDuration;
                            tag.duration = m_TagElement.MinTagDuration;
                        }
                        else
                        {
                            tag.startTime = newTime;
                            tag.duration = endTime - tag.startTime;
                        }

                        m_TagElement.Timeline.ShowStartGuideline(tag.startTime);

                        break;

                    case Mode.FinishTime:
                        Undo.RecordObject(asset, "Drag Tag end");
                        float newDuration = newTime - tag.startTime;
                        if (newDuration <= m_TagElement.MinTagDuration)
                        {
                            tag.duration = m_TagElement.MinTagDuration;
                        }
                        else
                        {
                            tag.duration = newDuration;
                        }

                        m_TagElement.Timeline.ShowEndGuideline(tag.EndTime);
                        break;

                    case Mode.Body:
                        Undo.RecordObject(asset, "Drag Tag");
                        if (m_TagElement.Timeline.TimelineUnits == TimelineViewMode.frames)
                        {
                            newTime = (float)TimelineUtility.RoundToFrame(newTime - m_OffsetAtMouseDown, framerate);
                        }
                        else
                        {
                            newTime -= m_OffsetAtMouseDown;
                        }

                        tag.startTime = newTime;

                        m_TagElement.Timeline.ShowGuidelines(tag.startTime, tag.EndTime);

                        break;
                }

                tag.NotifyChanged();
                m_TagElement.Timeline.SendTagModified();

                evt.StopPropagation();

                UpdateManipulatorLabels();
            }

            void OnMouseMoveReorderEvent(MouseMoveEvent evt)
            {
                if (m_Mode == Mode.Body)
                {
                    if (!m_TagElement.ContainsPoint(m_TagElement.WorldToLocal(evt.mousePosition)) &&
                        Math.Abs(m_MousePreviousPosition.y - evt.mousePosition.y) >= (m_TagElement.layout.height / 2f) + 3f)
                    {
                        bool draggingUp = evt.mousePosition.y < m_MouseDownPosition.y;
                        Undo.RecordObject(m_TagElement.m_Clip.Asset, "Reorder Tag");
                        if (m_TagElement.Timeline.ReorderTimelineElements(m_TagElement, draggingUp ? -1 : 1))
                        {
                            m_MousePreviousPosition = evt.mousePosition;
                            m_MouseDownPosition = evt.mousePosition;
                        }
                    }
                }
            }

            void OnMouseUpEvent(MouseUpEvent evt)
            {
                if (!m_Active || !target.HasMouseCapture() || !CanStopManipulation(evt))
                {
                    m_Active = false;
                    return;
                }

                m_TagElement.Select(evt.ctrlKey);

                m_Active = false;
                m_OverrideMode = Mode.None;
                target.ReleaseMouse();

                m_TagElement.m_ManipulateStartLabel.style.visibility = Visibility.Hidden;
                m_TagElement.m_ManipulateEndLabel.style.visibility = Visibility.Hidden;

                m_TagElement.Timeline.HideGuidelines();

                evt.StopPropagation();
            }

            void UpdateManipulatorLabels()
            {
                float sampleRate = m_TagElement.m_Clip.SampleRate;
                string start = TimelineUtility.GetTimeString(m_TagElement.Timeline.ViewMode, m_TagElement.m_Tag.startTime, (int)sampleRate);
                string end = TimelineUtility.GetTimeString(m_TagElement.Timeline.ViewMode, m_TagElement.m_Tag.EndTime, (int)sampleRate);

                m_TagElement.m_ManipulateStartLabel.text = start;

                float estimatedTextSize = TimelineUtility.EstimateTextSize(m_TagElement.m_ManipulateStartLabel);
                float controlWidth = Math.Max(float.IsNaN(estimatedTextSize) ? m_TagElement.m_ManipulateStartLabel.layout.width : estimatedTextSize, 8) + 6;

                m_TagElement.m_ManipulateStartLabel.style.left = -controlWidth;
                m_TagElement.m_ManipulateEndLabel.text = end;
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
            return Timeline.WorldPositionToTime(x);
        }

        public override void Resize()
        {
            float left = Timeline.TimeToLocalPos(m_Tag.startTime, Track);
            style.left = left;
            float right = Timeline.TimeToLocalPos(m_Tag.startTime + m_Tag.duration, Track);
            style.width = right - left;

            m_Label.text = string.IsNullOrEmpty(m_Tag.name) ? m_Tag.Name : m_Tag.name;
        }

        internal float MinTagDuration
        {
            get { return 2 * TagManipulator.k_TagHandleMinWidth / Timeline.WidthMultiplier; }
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
                    Timeline.DeleteSelection();
                }

                if (keyDownEvt.keyCode == KeyCode.F)
                {
                    Timeline.SetTimeRange(m_Tag.startTime, m_Tag.startTime + m_Tag.duration);
                }
            }
        }
    }
}
