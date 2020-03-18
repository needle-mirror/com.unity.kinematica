using System;
using System.Collections.Generic;
using System.Linq;

using Timeline;

using Unity.Kinematica.Temporary;

using UnityEditor;
using UnityEditor.UIElements;

using UnityEngine;
using UnityEngine.UIElements;

using Object = UnityEngine.Object;

namespace Unity.Kinematica.Editor
{
    partial class Timeline : VisualElement, IDisposable
    {
        const string k_TimelineUnitsPreferenceKey = "Unity.Kinematica.Editor.Timeline.ViewMode";
        const float k_TimelineLengthMultiplier = 2f;

        TimelineViewMode m_Mode = TimelineViewMode.frames;

        VisualElement m_TimelineWorkArea;
        TimeRuler m_TimeRuler;
        FloatField m_ActiveTimeField;
        Playhead m_ActiveTick;

        Playhead ActiveTick
        {
            get
            {
                if (m_ActiveTick == null)
                {
                    m_ActiveTick = new Playhead(false) { name = "activeTick" };
                    m_ActiveTick.AddManipulator(new PlayheadManipulator(this));
                }

                return m_ActiveTick;
            }
        }

        Playhead m_ActiveDebugTick;

        Playhead ActiveDebugTick
        {
            get
            {
                if (m_ActiveDebugTick == null)
                {
                    m_ActiveDebugTick = new Playhead(true) { name = "activeDebugTick" };
                    m_ActiveDebugTick.AddToClassList("activeDebugTickElement");
                    m_ActiveDebugTick.Q<VisualElement>("handle").AddToClassList("activeDebugTickHandle");
                    m_ActiveDebugTick.AddManipulator(new PlayheadManipulator(this));
                }

                return m_ActiveDebugTick;
            }
        }

        VisualElement m_EndOfClipLine;
        VisualElement m_ClipLengthBar;
        VisualElement m_ClipArea;
        VisualElement m_Tracks;
        List<Track> m_TrackElements;
        MetricsTrack m_MetricsTrack;
        AnnotationsTrack m_AnnotationsTrack;
        BoundaryAnimationClipElement m_PreBoundaryClipElement;
        BoundaryAnimationClipElement m_PostBoundaryClipElement;

        ToolbarToggle m_PreviewToggle;
        Image m_PreviewWarning;

        ZoomManipulator m_ZoomManipulator;

        TimelineSelectionContainer m_SelectionContainer;

        public Timeline(GameObject previewTarget, Asset asset, TaggedAnimationClip selection)
        {
            UIElements.UIElementsUtils.CloneTemplateInto(k_Template, this);
            UIElements.UIElementsUtils.ApplyStyleSheet(k_Stylesheet, this);
            AddToClassList("flexGrowClass");

            Button button = this.Q<Button>(classes: "viewMode");
            button.clickable = null;
            var manipulator = new ContextualMenuManipulator(ViewToggleMenu);
            manipulator.activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse });
            button.AddManipulator(manipulator);

            m_PreviewToggle = this.Q<ToolbarToggle>("previewToggle");
            //TODO - connect m_PreviewToggle.SetEnabled to the value of the prefabTarget
            m_PreviewToggle.RegisterValueChangedCallback(OnPreviewModeChanged);
            PreviewStatus = PreviewState.Enabled;

            m_PreviewWarning = this.Q<Image>("warningImage");
            m_PreviewWarning.style.display = DisplayStyle.None;
            m_PreviewWarning.tooltip = "Debugging using the Asset Builder requires setting a Preview Target before entering Play Mode";

            var previewTargetSelector = this.Q<ObjectField>("previewTarget");
            previewTargetSelector.objectType = typeof(GameObject);
            PreviewTarget = previewTarget;
            previewTargetSelector.value = PreviewTarget;
            previewTargetSelector.RegisterValueChangedCallback(OnPreviewTargetSelectorChanged);

            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);

            m_TimelineWorkArea = this.Q<VisualElement>("timelineWorkArea");

            m_PreBoundaryClipElement = new BoundaryAnimationClipElement();
            m_PreBoundaryClipElement.LabelUpdated += RepositionBoundaryClips;
            m_PostBoundaryClipElement = new BoundaryAnimationClipElement();
            m_PostBoundaryClipElement.LabelUpdated += RepositionBoundaryClips;

            m_TimelineWorkArea.Add(m_PreBoundaryClipElement);
            m_TimelineWorkArea.Add(m_PostBoundaryClipElement);

            m_TimelineScrollableArea = this.Q<VisualElement>("scrollableTimeArea");
            m_TimelineScrollableArea.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);

            m_ZoomManipulator = new ZoomManipulator(m_TimelineScrollableArea);
            m_ZoomManipulator.ScaleChangeEvent += OnTimelineMouseScroll;
            var panManipulator = new PanManipulator(m_TimelineScrollableArea);
            panManipulator.HorizontalOnly = true;
            panManipulator.Panned += OnTimelinePanned;

            m_ScrollViewContainer = this.Q<VisualElement>("trackScrollViewContainer");
            m_ScrollViewContainer.AddManipulator(panManipulator);
            m_ScrollViewContainer.AddManipulator(m_ZoomManipulator);
            m_ScrollViewContainer.AddManipulator(new TagCreationManipulator(this));

            m_ActiveTimeField = this.Q<FloatField>("frameField");
            m_ActiveTimeField.RegisterValueChangedCallback(OnActiveTimeFieldValueChanged);

            UpdateTimeRange();

            var addTagButton = this.Q<Button>("addTagButton");
            addTagButton.clickable.clicked += () => { ShowTagMenu(addTagButton.worldBound); };

            addTagButton.style.alignSelf = Align.FlexStart;

            m_Tracks = m_TimelineScrollableArea.Q<VisualElement>("tracks");
            m_TrackElements = new List<Track>();
            CreateBuiltInTracks();

            if (!EditorApplication.isPlaying)
            {
                SetActiveTime(0f);
            }

            m_EndOfClipLine = new VisualElement();
            m_EndOfClipLine.AddToClassList("endOfClip");
            m_EndOfClipLine.style.display = DisplayStyle.None;
            var endOfClipHead = new VisualElement();
            endOfClipHead.AddToClassList("endOfClipHead");
            m_EndOfClipLine.Add(endOfClipHead);

            m_ClipArea = new VisualElement();
            m_ClipArea.AddToClassList("clipArea");

            m_TimelineWorkArea.Add(m_EndOfClipLine);
            ScrollView sv = m_ScrollViewContainer.Q<ScrollView>();
            sv.Insert(0, m_ClipArea);
            m_TimelineWorkArea.Add(ActiveTick);
            m_TimelineWorkArea.Add(ActiveDebugTick);

            m_ClipLengthBar = this.Q<VisualElement>("clipLength");

            var betterTimeRuler = this.Q<VisualElement>("betterTimeRuler");
            m_TimeRuler = new TimeRuler(this, panManipulator, m_ZoomManipulator);
            betterTimeRuler.Add(m_TimeRuler);

            betterTimeRuler.AddManipulator(new PlayheadManipulator(this));

            SetClip(asset, selection);
            UpdateTimeRange();

            string storedViewMode = EditorPrefs.GetString(k_TimelineUnitsPreferenceKey);

            UpdateTimeRange();

            if (storedViewMode == string.Empty)
            {
                TimelineUnits = TimelineViewMode.frames;
            }
            else
            {
                int intVal;
                if (int.TryParse(storedViewMode, out intVal))
                {
                    TimelineUnits = (TimelineViewMode)intVal;
                }
                else
                {
                    TimelineUnits = TimelineViewMode.frames;
                }
            }

            Undo.undoRedoPerformed += OnUndoRedoPerformed;

            focusable = true;
            RegisterCallback<KeyDownEvent>(OnKeyDownEvent);

            if (EditorApplication.isPlaying)
            {
                DisableEdit();
            }
            else
            {
                EnableEdit();
            }
        }

        public void Dispose()
        {
            UnsubFromClip();
            UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            m_ZoomManipulator.ScaleChangeEvent -= OnTimelineMouseScroll;
            m_TimelineScrollableArea.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            var previewToggle = this.Q<ToolbarToggle>("Preview");
            previewToggle.UnregisterValueChangedCallback(OnPreviewModeChanged);

            DisposePreviews();

            if (m_SelectionContainer != null)
            {
                if (Selection.activeObject == m_SelectionContainer)
                {
                    Selection.activeObject = TargetAsset;
                }

                Object.DestroyImmediate(m_SelectionContainer);
                m_SelectionContainer = null;
            }

            TargetAsset = null;
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;

            if (m_PreBoundaryClipElement != null)
            {
                m_PreBoundaryClipElement.LabelUpdated -= RepositionBoundaryClips;
            }

            if (m_PostBoundaryClipElement != null)
            {
                m_PostBoundaryClipElement.LabelUpdated -= RepositionBoundaryClips;
            }
        }

        void SetPreBoundaryClip(TaggedAnimationClip boundary)
        {
            if (TaggedClip != null)
            {
                TaggedClip.TaggedPreBoundaryClip = boundary;
                if (Previewing)
                {
                    PreviewActiveTime();
                }
            }
        }

        void SetPostBoundaryClip(TaggedAnimationClip boundary)
        {
            if (TaggedClip != null)
            {
                TaggedClip.TaggedPostBoundaryClip = boundary;
                if (Previewing)
                {
                    PreviewActiveTime();
                }
            }
        }

        void CreateBuiltInTracks()
        {
            m_MetricsTrack = new MetricsTrack(this);
            m_AnnotationsTrack = new AnnotationsTrack(this);
            AddTrack(m_MetricsTrack);
            AddTrack(m_AnnotationsTrack);
        }

        void OnAddSelection(DropdownMenuAction a)
        {
            OnAddSelection(a.userData as Type);
        }

        void OnAddSelection(Type type)
        {
            if (type != null && TaggedClip != null)
            {
                if (TagAttribute.IsTagType(type))
                {
                    OnAddTagSelection(type, ActiveTime);
                }
                else if (MarkerAttribute.IsMarkerType(type))
                {
                    TaggedClip.AddMarker(type, ActiveTime);
                    TargetAsset.MarkDirty();
                }

                TaggedClip.NotifyChanged();
            }
        }

        void ShowTagMenu(Rect pos, string prefix = "")
        {
            var menu = new GenericMenu();
            foreach (var tagType in TagAttribute.GetVisibleTypesInInspector())
            {
                if (EditorApplication.isPlaying)
                {
                    menu.AddDisabledItem(new GUIContent($"{prefix}{TagAttribute.GetDescription(tagType)}"));
                }
                else
                {
                    menu.AddItem(new GUIContent($"{prefix}{TagAttribute.GetDescription(tagType)}"),
                        false,
                        () => OnAddSelection(tagType));
                }
            }

            menu.DropDown(pos);
        }

        void AddMarkerMenu(ContextualMenuPopulateEvent evt, string prefix = "")
        {
            foreach (var markerType in MarkerAttribute.GetMarkerTypes())
            {
                evt.menu.AppendAction($"{prefix}{MarkerAttribute.GetDescription(markerType)}", OnAddSelection, DropdownMenuAction.AlwaysEnabled, markerType);
            }
        }

        void ViewToggleMenu(ContextualMenuPopulateEvent evt)
        {
            evt.menu.AppendAction("Frames", a => { TimelineUnits = TimelineViewMode.frames; },
                a => TimelineUnits == TimelineViewMode.frames ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            evt.menu.AppendAction("Seconds", a => { TimelineUnits = TimelineViewMode.seconds; },
                a => TimelineUnits == TimelineViewMode.seconds ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            evt.menu.AppendAction("Seconds : Frames", a => { TimelineUnits = TimelineViewMode.secondsFrames; },
                a => TimelineUnits == TimelineViewMode.secondsFrames ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
        }

        void OnAssetDeserialized(Asset asset)
        {
            EditorApplication.delayCall += () =>
            {
                foreach (var track in m_TrackElements)
                {
                    track.OnAssetModified();
                }
            };
        }

        void UnsubFromClip()
        {
            if (TaggedClip != null)
            {
                SelectionContainer.Clear();
                TaggedClip.DataChanged -= UpdateBoundaryClips;
            }
        }

        void OnTimelineMouseScroll(float scaleChange, Vector2 focalPoint)
        {
            if (TaggedClip == null)
            {
                return;
            }

            float currentMaximumRange = m_TimeRange.MaximumTime - m_TimeRange.MinimumTime;
            float currentRange = m_TimeRange.EndTime - m_TimeRange.StartTime;
            float currentScale = currentMaximumRange / currentRange;

            float focalPointToWidth = focalPoint.x / m_TimelineScrollableArea.layout.width;
            float focalTime = focalPointToWidth * currentMaximumRange + m_TimeRange.MinimumTime;
            float focalRatio = (focalTime - m_TimeRange.StartTime) / currentRange;

            float newRange = currentRange / scaleChange;
            float newStartTime = m_TimeRange.Limit(focalTime - (newRange * focalRatio));
            float newEndTime = m_TimeRange.Limit(newStartTime + newRange);

            float actualNewRange = newEndTime - newStartTime;

            m_TimeRange.SetTimeRange(new Vector2(newStartTime, newEndTime));
        }

        void OnTimelinePanned(Vector2 from, Vector2 to)
        {
            if (float.IsNaN(m_TimelineScrollableArea.layout.x))
            {
                return;
            }

            if (TaggedClip == null)
            {
                return;
            }

            float timeChange = WorldPositionToTime(from.x) - WorldPositionToTime(to.x);
            if (m_TimeRange.PanTimeRange(timeChange))
            {
                OnGeometryChanged();
            }
        }

        void ResetWidthToTimelineArea()
        {
            Vector3 newPosition = m_TimelineScrollableArea.transform.position;
            m_TimelineScrollableArea.style.width = m_TimelineWorkArea.layout.width;
            newPosition.x = 0f;
            m_TimelineScrollableArea.transform.position = newPosition; // Reset the panning position
        }

        void OnGeometryChanged(GeometryChangedEvent evt)
        {
            OnGeometryChanged();
        }

        void OnGeometryChanged()
        {
            if (m_TimelineScrollableArea.layout.width < m_TimelineWorkArea.layout.width)
            {
                ResetWidthToTimelineArea();
            }
            else
            {
                var newTimelinePosition = m_TimelineScrollableArea.transform.position;
                if (newTimelinePosition.x + m_TimelineScrollableArea.layout.width < m_TimelineWorkArea.layout.width)
                {
                    newTimelinePosition.x = m_TimelineWorkArea.layout.width - m_TimelineScrollableArea.layout.width;
                    m_TimelineScrollableArea.transform.position = newTimelinePosition;
                }

                ResizeContents();
            }

            foreach (var track in m_TrackElements)
            {
                track.RepositionLabels();
            }

            ResetTimeRuler();

            m_TimelineScrollableArea.style.minHeight = m_ScrollViewContainer.layout.height;
        }

        void OnActiveTimeFieldValueChanged(ChangeEvent<float> evt)
        {
            if (TaggedClip == null)
            {
                return;
            }

            if (m_Mode == TimelineViewMode.frames)
            {
                SetActiveTime(evt.newValue / TaggedClip.SampleRate, false);
            }
            else
            {
                SetActiveTime(evt.newValue, false);
            }
        }

        void ResetTimeRuler()
        {
            var containerStart = m_TimelineWorkArea.worldBound.x;
            m_TimeRuler.m_TimelineWidget.RangeStart = WorldPositionToTime(containerStart);
            m_TimeRuler.m_TimelineWidget.RangeWidth = WorldPositionToTime(containerStart + m_TimelineWorkArea.layout.width) - m_TimeRuler.m_TimelineWidget.RangeStart;
            m_TimeRuler.SampleRate = TaggedClip != null ? (int)TaggedClip.SampleRate : 60;
        }

        Asset m_Target;

        float TimeToWorldPos(float t)
        {
            int horizontalResolution = Screen.currentResolution.width;
            return (((t + SecondsBeforeZero) * WidthMultiplier + m_TimelineScrollableArea.worldBound.x) * horizontalResolution + 0.5f) / horizontalResolution;
        }

        float TimeToLocalPos(float time, VisualElement localElement)
        {
            float zeroWorldPos = TimeToWorldPos(time);
            float pos = localElement.WorldToLocal(new Vector2(zeroWorldPos, 0f)).x;
            if (m_Mode != TimelineViewMode.seconds)
            {
                pos = (float)Math.Round(pos);
            }

            return pos;
        }

        internal void DeleteSelection()
        {
            m_SelectionContainer.DeleteSelectionFromClip();
        }

        internal void OnAssetModified()
        {
            foreach (var track in m_TrackElements)
            {
                track.OnAssetModified();
            }
        }

        void ReloadTagElements()
        {
            foreach (var track in m_TrackElements)
            {
                track.ReloadElements();
            }
        }

        public bool ReorderTimelineElements(ITimelineElement element, int direction)
        {
            foreach (var tt in m_TrackElements.OfType<AnnotationsTrack>())
            {
                if (tt.ReorderTagElement(element, direction))
                {
                    SendTagModified();
                    return true;
                }
            }

            return false;
        }

        void UpdatePreviewWarningLabel()
        {
            m_PreviewWarning.style.display = DisplayStyle.None;
            // Delay checking if we are now in play mode to show warning about preview target
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlaying)
                {
                    Kinematica component = null;
                    if (PreviewTarget != null)
                    {
                        component = PreviewTarget.GetComponent<Kinematica>();
                    }

                    if (component == null)
                    {
                        m_PreviewWarning.style.display = DisplayStyle.Flex;
                    }
                }
            };
        }

        void SetFPSLabelText()
        {
            if (TaggedClip != null && TaggedClip.Valid)
            {
                var clip = TaggedClip.AnimationClip;
                if (ActiveTick.style.display == DisplayStyle.Flex)
                {
                    if (TimelineUnits == TimelineViewMode.frames)
                    {
                        m_ActiveTimeField.SetValueWithoutNotify(ActiveTime * clip.frameRate);
                    }
                    else
                    {
                        m_ActiveTimeField.SetValueWithoutNotify(ActiveTime);
                    }
                }
            }
        }

        void OnUndoRedoPerformed()
        {
            if (Selection.activeObject != SelectionContainer)
            {
                ClearSelection();
            }

            UpdateBoundaryClips();
        }

        void UpdateBoundaryClips()
        {
            if (TaggedClip == null || TargetAsset == null)
            {
                m_PreBoundaryClipElement.Reset();
                m_PreBoundaryClipElement.Reset();
            }
            else
            {
                m_PreBoundaryClipElement.SetClips(TargetAsset.AnimationLibrary);
                m_PreBoundaryClipElement.Select(TaggedClip.PreBoundaryClip);
                m_PostBoundaryClipElement.SetClips(TargetAsset.AnimationLibrary);
                m_PostBoundaryClipElement.Select(TaggedClip.PostBoundaryClip);
            }
        }

        void OnKeyDownEvent(KeyDownEvent keyDownEvt)
        {
            if (m_TaggedClip != null)
            {
                if (keyDownEvt.keyCode == KeyCode.F)
                {
                    if (m_SelectionContainer.m_FullClipSelection)
                    {
                        SetTimeRange(0f, m_TaggedClip.DurationInSeconds);
                    }
                    else
                    {
                        var tag = m_SelectionContainer.Tags.FirstOrDefault();
                        if (tag != null)
                        {
                            SetTimeRange(tag.startTime, tag.startTime + tag.duration);
                        }
                        else
                        {
                            SetTimeRange(0f, m_TaggedClip.DurationInSeconds);
                        }
                    }

                    keyDownEvt.StopPropagation();
                    keyDownEvt.PreventDefault();
                }
                else if (keyDownEvt.keyCode == KeyCode.A)
                {
                    SetTimeRange(0f, m_TaggedClip.DurationInSeconds);
                    keyDownEvt.StopPropagation();
                    keyDownEvt.PreventDefault();
                }
            }
        }

        void HideDebugPlayhead()
        {
            ActiveDebugTick.style.display = DisplayStyle.None;
        }

        void ShowDebugPlayhead()
        {
            ActiveDebugTick.style.display = DisplayStyle.Flex;
        }

        void EnableEdit()
        {
            m_PreBoundaryClipElement.SetEnabled(true);
            m_PostBoundaryClipElement.SetEnabled(true);
        }

        void DisableEdit()
        {
            m_PreBoundaryClipElement.SetEnabled(false);
            m_PostBoundaryClipElement.SetEnabled(false);
        }
    }
}
