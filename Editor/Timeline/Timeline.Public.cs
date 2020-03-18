using System;
using System.Linq;
using Unity.SnapshotDebugger;
using Unity.SnapshotDebugger.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Kinematica.Editor
{
    partial class Timeline
    {
        const string k_Template = "Timeline.uxml";
        public const string k_Stylesheet = "Timeline.uss";
        public const string k_timelineCellStyleKey = "timelineCell";

        public enum PreviewState
        {
            Disabled,
            Enabled,
            Active
        }

        public readonly VisualElement m_TimelineScrollableArea;
        public readonly VisualElement m_ScrollViewContainer;

        TaggedAnimationClip m_TaggedClip;

        public TaggedAnimationClip TaggedClip
        {
            get { return m_TaggedClip; }
            set
            {
                if (m_TaggedClip != value)
                {
                    if (m_TaggedClip != null)
                    {
                        m_PreBoundaryClipElement.SelectionChanged -= SetPreBoundaryClip;
                        m_PostBoundaryClipElement.SelectionChanged -= SetPostBoundaryClip;
                        UnsubFromClip();
                    }

                    m_TaggedClip = value;

                    if (m_TaggedClip != null)
                    {
                        m_PreBoundaryClipElement.SelectionChanged += SetPreBoundaryClip;
                        m_PostBoundaryClipElement.SelectionChanged += SetPostBoundaryClip;

                        m_PreBoundaryClipElement.Select(m_TaggedClip);
                        m_PostBoundaryClipElement.Select(m_TaggedClip);

                        m_TaggedClip.DataChanged += UpdateBoundaryClips;
                    }
                }
            }
        }

        public float ActiveTime
        {
            get
            {
                if (m_TimeRange != null)
                {
                    return m_TimeRange.ActiveTime;
                }

                return -1;
            }
            set
            {
                if (m_TimeRange != null)
                {
                    m_TimeRange.SetActiveTime(value, m_Mode == TimelineViewMode.frames);
                }
            }
        }

        public float DebugTime
        {
            get
            {
                if (m_TimeRange != null)
                {
                    return m_TimeRange.DebugTime;
                }

                return -1;
            }
            set
            {
                if (m_TimeRange != null)
                {
                    m_TimeRange.SetDebugTime(value, m_Mode == TimelineViewMode.frames);
                }
            }
        }

        public bool Previewing
        {
            get { return m_IsPreviewing; }
            private set
            {
                if (m_IsPreviewing != value)
                {
                    m_IsPreviewing = value;

                    if (!m_IsPreviewing)
                    {
                        DisposePreviews();
                    }
                    else
                    {
                        UpdatePreview();
                    }

                    UpdatePreviewStateBasedOnPreviewStatus();
                }
            }
        }

        public void AddTrack(Track t)
        {
            m_Tracks.Add(t);
            m_TrackElements.Add(t);
        }

        public bool CanAddTag()
        {
            return TaggedClip != null && TaggedClip.Valid;
        }

        public void Reset()
        {
            SetClip(null, null);
        }

        public void SetClip(Asset target, TaggedAnimationClip taggedClip, bool updateTimeRange = true)
        {
            ClearSelection();

            m_EndOfClipLine.style.display = DisplayStyle.None;
            m_ClipLengthBar.style.display = DisplayStyle.None;
            m_ActiveTimeField.SetEnabled(false);

            TargetAsset = target;

            UnsubFromClip();

            TaggedClip = taggedClip;

            m_MetricsTrack.Clear();

            foreach (var tt in m_TrackElements.OfType<AnnotationsTrack>())
            {
                tt.SetClip(TaggedClip);
            }

            UpdateBoundaryClips();

            if (TaggedClip == null || TargetAsset == null)
            {
                Previewing = false;
                SetFPSLabelText();
                SyncPreviewStatusToCanPreview();

                m_PreBoundaryClipElement.visible = false;
                m_PostBoundaryClipElement.visible = false;

                return;
            }

            m_PreBoundaryClipElement.visible = true;
            m_PostBoundaryClipElement.visible = true;

            if (updateTimeRange)
            {
                UpdateTimeRange();
            }

            SelectionContainer.Select(TaggedClip); // display specific information from the clip in the inspector, like the retarget source avatar, even if no tags/markers have been selected yet
            Selection.activeObject = SelectionContainer;

            ScrollView sv = this.Q<ScrollView>("trackScrollView");
            sv.AddManipulator(m_ZoomManipulator);

            if (!TaggedClip.Valid)
            {
                SetFPSLabelText();
                return;
            }

            ReloadTagElements();

            if (Previewing)
            {
                PreviewActiveTime();
            }

            SetFPSLabelText();

            AdjustTicks();
            SyncPreviewStatusToCanPreview();

            m_EndOfClipLine.style.display = DisplayStyle.Flex;
            m_ClipLengthBar.style.display = DisplayStyle.Flex;

            m_ActiveTimeField.SetEnabled(true);

            ResetTimeRuler();
        }

        internal void ReSelectCurrentClip()
        {
            SetClip(TargetAsset, m_TaggedClip, false);
        }

        public bool CanPreview()
        {
            if (TaggedClip == null || TaggedClip.AnimationClip == null || TargetAsset == null || TargetAsset.DestinationAvatar == null || PreviewTarget == null || EditorApplication.isPlaying)
            {
                return false;
            }

            return true;
        }

        public TimelineViewMode TimelineUnits
        {
            get { return m_Mode; }
            private set
            {
                if (m_Mode != value)
                {
                    m_Mode = value;
                    ResizeContents();
                    UpdatePlayheadPositions();
                    AdjustTicks();

                    EditorPrefs.SetString(k_TimelineUnitsPreferenceKey, ((int)TimelineUnits).ToString());
                }
            }
        }

        public void OnAddTagSelection(Type tagType, float startTime, float duration = -1f)
        {
            if (m_Mode == TimelineViewMode.frames)
            {
                startTime = (float)TimelineUtility.RoundToFrame(startTime, TaggedClip.SampleRate);
                if (duration >= 0)
                {
                    duration = (float)TimelineUtility.RoundToFrame(duration, TaggedClip.SampleRate);
                }
            }

            TaggedClip.AddTag(tagType, startTime, duration);
            TargetAsset.MarkDirty();
        }

        public Asset TargetAsset
        {
            get { return m_Target; }
            set
            {
                if (m_Target != value)
                {
                    if (m_Target != null)
                    {
                        m_Target.AvatarModified -= SyncPreviewStatusToCanPreview;
                        m_Target.AssetWasDeserialized -= OnAssetDeserialized;
                    }

                    m_Target = null;
                    Reset();
                    m_Target = value;
                    if (m_Target != null)
                    {
                        m_Target.AvatarModified += SyncPreviewStatusToCanPreview;
                        m_Target.AssetWasDeserialized += OnAssetDeserialized;
                    }
                }
            }
        }

        public void OnPlayModeStateChanged(PlayModeStateChange stateChange, GameObject target)
        {
            PreviewTarget = target;
            if (stateChange == PlayModeStateChange.ExitingPlayMode || stateChange == PlayModeStateChange.EnteredEditMode)
            {
                HideDebugPlayhead();
                ActiveTick.ShowHandle = true;
                EnableEdit();
            }
            else if (stateChange == PlayModeStateChange.EnteredPlayMode || stateChange == PlayModeStateChange.ExitingEditMode)
            {
                ActiveTick.ShowHandle = false;
                DisableEdit();
            }
        }

        public event Action<GameObject> PreviewTargetChanged;
        public GameObject PreviewTarget
        {
            get { return m_PreviewTarget; }
            private set
            {
                if (m_PreviewTarget != value)
                {
                    m_PreviewTarget = value;
                    ManipulatorGizmo.Instance.SetPreviewTarget(m_PreviewTarget);

                    SyncPreviewStatusToCanPreview();

                    if (m_Preview != null)
                    {
                        m_Preview.SetPreviewTarget(m_PreviewTarget);
                    }
                    if (m_PreviewTarget == null)
                    {
                        SetDebugTime(-1);
                    }

                    PreviewTargetChanged?.Invoke(m_PreviewTarget);
                }

                UpdatePreviewWarningLabel();
            }
        }

        public TimelineSelectionContainer SelectionContainer
        {
            get
            {
                if (m_SelectionContainer == null)
                {
                    m_SelectionContainer = ScriptableObject.CreateInstance<TimelineSelectionContainer>();
                    m_SelectionContainer.Clip = TaggedClip;
                }

                return m_SelectionContainer;
            }
        }

        public float SecondsBeforeZero
        {
            get
            {
                if (TaggedClip == null)
                {
                    return 0;
                }

                return TicksBeforeZero / TaggedClip.SampleRate;
            }
        }

        public int TicksBeforeZero
        {
            get
            {
                if (TaggedClip != null)
                {
                    return TaggedClip.NumFrames;
                }

                return 10;
            }
        }

        public float WorldPositionToTime(float x)
        {
            return (x - m_TimelineScrollableArea.worldBound.x) / WidthMultiplier - SecondsBeforeZero;
        }

        public void SendTagModified()
        {
            TargetAsset.MarkDirty();
            ReloadMetrics();
        }

        public void ReloadMetrics()
        {
            m_MetricsTrack.ReloadElements();
        }

        public void SetTimeRange(float startTime, float endTime)
        {
            float sampleRate = TaggedClip != null ? TaggedClip.SampleRate : 60f;
            m_TimeRange.SetTimeRange(new Vector2(startTime - 5 / sampleRate, endTime + 5 / sampleRate));
        }

        public void SetActiveTime(float time, bool propagateToLabel = true)
        {
            Previewing = CanPreview();

            SetActiveTickVisible(true);

            ActiveTime = time;

            UpdatePlayheadPositions(propagateToLabel);
            PreviewActiveTime();
        }

        public void SetDebugTime(float time)
        {
            if (Debugger.instance.rewind && TaggedClip != null && EditorApplication.isPlaying)
            {
                float duration = TaggedClip.AnimationClip.length;

                time = Mathf.Clamp(time, 0f, duration);

                ShowDebugPlayhead();

                DebugTime = time;

                UpdatePlayheadPositions();
            }
            else
            {
                HideDebugPlayhead();
            }
        }

        public void ClearSelection()
        {
            SelectionContainer.Clear();
        }

        public void Select(ITimelineElement selection, bool multi)
        {
            if (!multi)
            {
                ClearSelection();
                SelectionContainer.Clear();
            }

            SelectionContainer.Select(selection, TaggedClip);
            Selection.activeObject = SelectionContainer;
        }

        public void SetActiveTickVisible(bool visible)
        {
            ActiveTick.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public float GetFreeVerticalHeight()
        {
            return m_AnnotationsTrack.EndOfTags + m_AnnotationsTrack.layout.y;
        }
    }
}
