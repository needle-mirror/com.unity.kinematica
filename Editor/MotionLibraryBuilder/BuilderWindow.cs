using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Kinematica.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

using UnityEditor;
using UnityEditor.UIElements;

using Object = UnityEngine.Object;
using Unity.SnapshotDebugger;

namespace Unity.Kinematica.Editor
{
    internal class BuilderWindow : EditorWindow
    {
        static class Styles
        {
            //TODO - this style value gets repeated frequently in trunk, there might be a more global way of setting/getting it
            public static GUIStyle lockButton = "IN LockButton";
        }

        [SerializeField]
        internal bool isLocked;

        const string k_AssetSettingsViewName = "settingsView";
        const string k_AnimationClipsViewName = "clipsView";

        enum LayoutMode
        {
            CreateAsset,
            ConfigureAndBuildAsset
        }

        // UIElements filenames

        // Templates
        const string k_MainLayout = "KinematicaWindow.uxml";
        const string k_AssetInput = "Builder.uxml";
        //Styles
        internal const string k_Stylesheet = "KinematicaWindow.uss";
        const string k_ToolbarStyle = "Toolbar.uss";
        const string k_AnimationLibraryStyle = "AnimationClipLibrary.uss";

        const string k_WindowTitle = "Kinematica Asset Builder";

        VisualElement m_Toolbar;
        VisualElement m_MainLayout;

        VisualElement m_AssetCreateElement;
        VisualElement m_ClipAndSettingsInput;

        Button m_EditAssetButton;
        VisualElement m_AssetDirtyWarning;
        Button m_BuildButton;

        AnimationClipListView m_AnimationLibraryInput;
        static List<string> k_SupportedTags;

        List<string> m_PlayedClips = new List<string>();

        [SerializeField]
        GameObject m_PreviewTarget;

        [SerializeField]
        TaggedAnimationClip m_Selection;

        Timeline m_Timeline;


        VisualElement m_MainInputLayout;

        bool m_UpdateTitle = false;

        [UnityEditor.Callbacks.OnOpenAsset(1)]
        static bool OnOpenAsset(int instanceID, int line)
        {
            var kinematicaAsset = EditorUtility.InstanceIDToObject(instanceID) as Asset;
            if (kinematicaAsset)
            {
                BuilderWindow window = GetWindow<BuilderWindow>();
                window.Asset = kinematicaAsset;
                return true; //catch open file
            }

            return false; // let unity open the file
        }

        public static List<string> SupportedTags => k_SupportedTags ?? (k_SupportedTags = TagAttribute.GetAllDescriptions());

        [MenuItem(("Window/Animation/Kinematica Asset Builder"))]
        public static void ShowWindow()
        {
            var wnd = GetWindow(typeof(BuilderWindow)) as BuilderWindow;
            wnd.UpdateTitle();
        }

        class AssetChangedProcessor : UnityEditor.AssetModificationProcessor
        {
            internal static Asset k_ActiveAsset;
            internal static BuilderWindow k_Window;
            static string[] OnWillSaveAssets(string[] paths)
            {
                //TODO - remove this before shipping, we won't tell users when changes are not saved
                if (k_Window != null && k_ActiveAsset != null)
                {
                    if (paths.Contains(AssetDatabase.GetAssetPath(k_ActiveAsset)))
                    {
                        k_Window.m_UpdateTitle = true;
                    }
                }

                return paths;
            }

            static AssetDeleteResult OnWillDeleteAsset(string s, RemoveAssetOptions options)
            {
                if (k_ActiveAsset != null && k_Window != null)
                {
                    if (s.Equals(AssetDatabase.GetAssetPath(k_ActiveAsset)))
                    {
                        k_Window.SetAsset(null);
                        k_Window.TargetCurrentSelection();
                    }
                }

                return AssetDeleteResult.DidNotDelete;
            }
        }

        //TODO - remove functionality
        void UpdateTitle()
        {
            bool dirty = Asset != null && EditorUtility.IsDirty(Asset);
            titleContent = new GUIContent(k_WindowTitle + (dirty ? " *" : string.Empty));
        }

        protected virtual void OnEnable()
        {
            LoadTemplate();
            //m_TargetGameObject = null;
            if (Asset == null)
            {
                TargetCurrentSelection();
            }
            else
            {
                SetAsset(Asset);
                ChangeLayoutMode(LayoutMode.ConfigureAndBuildAsset);
            }

            Selection.selectionChanged += OnSelectionChanged;

            AssetChangedProcessor.k_Window = this;

            if (m_Selection != null)
            {
                m_AnimationLibraryInput.SelectItem(m_Selection);
            }

            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        void OnDisable()
        {
            AssetChangedProcessor.k_Window = null;

            // In case we en up closing the window before the layout has loaded
            if (m_MainLayout == null)
            {
                return;
            }

            m_EditAssetButton.clickable.clicked -= EditAsset;
            m_BuildButton.clickable.clicked -= BuildAsset;

            var createButton = m_MainLayout.Q<Button>("createButton");
            createButton.clickable.clicked -= CreateButtonClicked;

            Selection.selectionChanged -= OnSelectionChanged;
            m_AnimationLibraryInput.onSelectionChanged -= OnLibrarySelectionChanged;

            if (m_Timeline != null)
            {
                m_PreviewTarget = m_Timeline.PreviewTarget;
                m_Timeline.Dispose();
                m_Timeline = null;
            }

            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            m_PlayedClips.Clear();

            // Hide clip highlights
            foreach (var clip in m_AnimationLibraryInput.Children())
            {
                clip.ElementAt(k_ClipHighlight).style.visibility = Visibility.Hidden;
            }

            m_Timeline.OnPlayModeStateChanged(state, m_PreviewTarget);

            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredPlayMode)
            {
                m_BuildButton.SetEnabled(false);
                ShowOrHideAssetDirtyWarning(true);
            }
            else
            {
                m_BuildButton.SetEnabled(true);
                ShowOrHideAssetDirtyWarning(false);
            }
        }

        void Update()
        {
            if (m_UpdateTitle)
            {
                if (m_Asset != null)
                {
                    m_Asset.OnSave();
                }

                UpdateTitle();
                m_UpdateTitle = false;
            }

            //
            // It would probably be a more symmetric architecture
            // to push the current time from the synthesizer to the
            // builder window.
            //
            // The two other use cases (highlight current task time
            // and retrieving the override time index) use a pull
            // model.
            //

            if (Application.isPlaying && m_PreviewTarget != null)
            {
                var kinematica = m_PreviewTarget.GetComponent<Kinematica>();

                if (kinematica != null)
                {
                    ref var synthesizer = ref kinematica.Synthesizer.Ref;

                    var samplingTime = synthesizer.Time;

                    HighlightTimeIndex(ref synthesizer, samplingTime.timeIndex);
                }
                else
                {
                    HighlightAnimationClip(null);
                    HighlightCurrentSamplingTime(null, 0.0f);
                }
            }
        }

        public TimeIndex RetrieveDebugTimeIndex(ref Binary binary)
        {
            if (Debugger.instance.rewind)
            {
                var taggedClip = m_Timeline.TaggedClip;

                if (taggedClip != null)
                {
                    var animationClip = taggedClip.AnimationClip;

                    var sampleTimeInSeconds = m_Timeline.DebugTime;

                    if (sampleTimeInSeconds >= 0.0f)
                    {
                        AnimationSampleTime animSampleTime = new AnimationSampleTime()
                        {
                            clip = animationClip,
                            sampleTimeInSeconds = sampleTimeInSeconds
                        };

                        return animSampleTime.GetTimeIndex(ref binary);
                    }
                }
            }

            return TimeIndex.Invalid;
        }

        public void HighlightTimeIndex(ref MotionSynthesizer synthesizer, TimeIndex timeIndex, bool debug = false)
        {
            AnimationSampleTime animSampleTime = AnimationSampleTime.CreateFromTimeIndex(
                ref synthesizer.Binary,
                timeIndex,
                m_AnimationLibraryInput.Children().Select(x => (x.userData as TaggedAnimationClip)?.AnimationClip));

            if (animSampleTime.IsValid)
            {
                HighlightAnimationClip(animSampleTime.clip);
                HighlightCurrentSamplingTime(animSampleTime.clip, animSampleTime.sampleTimeInSeconds, debug);
            }
        }

        void HighlightCurrentSamplingTime(AnimationClip animationClip, float sampleTimeInSeconds, bool debug = false)
        {
            if (m_Selection == null)
            {
                return;
            }

            var taggedClip = m_Timeline.TaggedClip;
            if (animationClip != null && taggedClip != null && taggedClip.AnimationClip == animationClip)
            {
                if (debug)
                {
                    m_Timeline.SetActiveTickVisible(false);
                    m_Timeline.SetDebugTime(sampleTimeInSeconds);
                }
                else
                {
                    m_Timeline.SetActiveTime(sampleTimeInSeconds);
                    m_Timeline.SetActiveTickVisible(true);
                }
            }
            else
            {
                m_Timeline.SetActiveTickVisible(false);
            }
        }

        void HighlightAnimationClip(AnimationClip animationClip)
        {
            foreach (var clip in m_AnimationLibraryInput.Children())
            {
                if (!(clip.userData is TaggedAnimationClip taggedClip))
                {
                    continue;
                }

                var clipStyle = clip.ElementAt(k_ClipHighlight).style;

                if (taggedClip.AnimationClip == animationClip)
                {
                    clipStyle.visibility = Visibility.Visible;
                    clipStyle.opacity = new StyleFloat(1f);
                }
                else
                {
                    clipStyle.visibility = Visibility.Hidden;
                }
            }
        }

        protected virtual void ShowButton(Rect r)
        {
            EditorGUI.BeginChangeCheck();
            //TODO - stole code from internal EditorGUIUtility....
            bool newLock = GUI.Toggle(r, isLocked, GUIContent.none, Styles.lockButton);

            if (EditorGUI.EndChangeCheck())
            {
                if (newLock != isLocked)
                {
                    isLocked = !isLocked;
                    if (!isLocked)
                    {
                        TargetCurrentSelection();
                    }
                }
            }
        }

        void LoadTemplate()
        {
            rootVisualElement.Clear();

            UIElementsUtils.ApplyStyleSheet(k_Stylesheet, rootVisualElement);
            UIElementsUtils.ApplyStyleSheet(k_ToolbarStyle, rootVisualElement);

            UIElementsUtils.CloneTemplateInto(k_MainLayout, rootVisualElement);

            VisualElement outerElement = rootVisualElement.Q<VisualElement>("kinematica");
            SetupToolbar(outerElement);

            m_MainLayout = outerElement.Q<VisualElement>("windowContent");

            // Input for Build
            {
                UIElementsUtils.CloneTemplateInto(k_AssetInput, m_MainLayout);

                m_MainInputLayout = outerElement.Q<VisualElement>("inputLayout");
                m_ClipAndSettingsInput = m_MainLayout.Q<VisualElement>("inputArea");

                //Profile and Asset creation
                {
                    m_AssetCreateElement = m_MainLayout.Q<VisualElement>(classes: "createLayout");

                    var createButton = m_AssetCreateElement.Q<Button>("createButton");
                    createButton.clickable.clicked += CreateButtonClicked;
                    createButton.text = "Create";
                }

                m_EditAssetButton = rootVisualElement.Q<Button>("editAssetButton");
                m_EditAssetButton.clickable.clicked += EditAsset;

                m_AssetDirtyWarning = rootVisualElement.Q<VisualElement>("assetDirtyWarning");

                m_BuildButton = rootVisualElement.Q<Button>("buildButton");
                m_BuildButton.clickable.clicked += BuildAsset;

                SetToolbarEnable(m_Asset != null);

                var assetSelector = m_Toolbar.Q<ObjectField>("asset");
                assetSelector.objectType = typeof(Asset);
                assetSelector.RegisterValueChangedCallback(OnAssetSelectionChanged);

                m_AnimationLibraryInput = m_ClipAndSettingsInput.Q<AnimationClipListView>("animationLibrary");
                m_AnimationLibraryInput.m_Window = this;
                m_AnimationLibraryInput.selectionType = SelectionType.Multiple;
                m_AnimationLibraryInput.makeItem = MakeAnimationItem;
                m_AnimationLibraryInput.bindItem = BindAnimationItem;
                m_AnimationLibraryInput.itemHeight = 18;
                UIElementsUtils.ApplyStyleSheet(k_AnimationLibraryStyle, m_ClipAndSettingsInput.Q<VisualElement>("clipsArea"));

                var timelineContainer = rootVisualElement.Q<VisualElement>("timeline");
                m_Timeline = new Timeline(m_PreviewTarget, Asset, m_Selection);
                m_Timeline.PreviewTargetChanged += OnTimelinePreviewTargetChanged;
                timelineContainer.Add(m_Timeline);
                m_AnimationLibraryInput.onSelectionChanged += OnLibrarySelectionChanged;
            }

            TargetCurrentSelection();
        }

        void OnTimelinePreviewTargetChanged(GameObject previewTarget)
        {
            m_PreviewTarget = previewTarget;
        }

        void OnLibrarySelectionChanged(List<object> selection)
        {
            m_Selection = null;


            var selectedClips = selection.OfType<TaggedAnimationClip>().ToList();
            if (selectedClips.Count() != 1)
            {
                m_Timeline.SetClip(null, null);
                m_Selection = null;
                Selection.activeObject = Asset;
                return;
            }

            m_Selection = selectedClips.First();
            m_Timeline.SetClip(Asset, m_Selection);
        }

        void OnTagSelectionClicked(DropdownMenuAction a)
        {
            var tagType = a.userData as Type;
            foreach (var clip in m_AnimationLibraryInput.m_ClipSelection)
            {
                clip.AddTag(tagType);
            }
        }

        void SetupToolbar(VisualElement parent)
        {
            m_Toolbar = parent.Q<VisualElement>("toolbar");
        }

        void OnAssetSelectionChanged(ChangeEvent<Object> e)
        {
            SetTarget(e.newValue as Asset);
        }

        void OnSelectionChanged()
        {
            if (isLocked)
            {
                //TODO - if the target object is deleted we need to prevent entering an invalid state.
                return;
            }

            //m_TargetGameObject = null;
            TargetCurrentSelection();
        }

        void CreateButtonClicked()
        {
            if (!CreateNewAsset())
            {
                return;
            }

            //TODO - MotionSynthesizer component has been moved to the project, we will revisit generating our own GameObject and adding a component later...
            /* if (m_TargetGameObject == null)
            {
                CreateGameObject();
            }
            */
        }

        void TargetCurrentSelection()
        {
            var asset = Selection.activeObject as Asset;
            if (asset != null)
            {
                SetTarget(asset);
            }
            else if (Asset == null)
            {
                m_Timeline.Reset();
                ChangeLayoutMode(LayoutMode.CreateAsset);
            }
        }

        void ChangeLayoutMode(LayoutMode mode)
        {
            switch (mode)
            {
                case LayoutMode.CreateAsset:
                    var label = m_AssetCreateElement.Q<Label>("createLabel");
                    /*
                    if (m_TargetGameObject == null)
                    {
                        label.text = "Create a new Kinematica Asset.";
                        //label.text = "Create a new Kinematica Asset and GameObject with a Kinematica Component.";
                    }
                    else
                    */
                    {
                        //TODO - MotionSynthesizer is no longer a component, need to update this section
                        /*
                        var cmp = m_TargetGameObject.GetComponent<MotionSynthesizer>();
                        string labelText = string.Empty;
                        if (KinematicaLibrarySettings == null)
                        {
                            labelText = "Create a new Kinematica Asset";
                        }

                        if (cmp == null)
                        {
                            labelText += $" and component on \"{m_TargetGameObject.name}\"";
                        }

                        label.text = labelText;
                        */
                    }

                    m_AssetCreateElement.style.display = DisplayStyle.Flex;
                    m_MainInputLayout.style.display = DisplayStyle.None;
                    break;
                case LayoutMode.ConfigureAndBuildAsset:
                    m_AssetCreateElement.style.display = DisplayStyle.None;
                    m_MainInputLayout.style.display = DisplayStyle.Flex;
                    break;
            }
        }

        [SerializeField]
        Asset m_Asset;

        public Asset Asset
        {
            get
            {
                return m_Asset;
            }
            set
            {
                if (m_Asset != value)
                {
                    SetAsset(value);
                }
            }
        }

        void SetAsset(Asset value)
        {
            if (m_Asset != null)
            {
                m_Asset.MarkedDirty -= UpdateTitle;
                m_Asset.AssetWasDeserialized -= OnAssetDeserialized;
            }

            m_Asset = value;
            AssetChangedProcessor.k_ActiveAsset = m_Asset;

            if (m_Asset != null)
            {
                m_AnimationLibraryInput.UpdateSource(m_Asset.AnimationLibrary);
                m_Asset.MarkedDirty += UpdateTitle;
                m_Asset.AssetWasDeserialized += OnAssetDeserialized;
            }
            else
            {
                m_AnimationLibraryInput.UpdateSource(null);
            }

            SetToolbarEnable(m_Asset != null);

            m_Toolbar.Q<ObjectField>("asset").value = m_Asset;

            //Ensure Timeline is refreshed
            m_Timeline.TargetAsset = null;
            m_Timeline.TargetAsset = m_Asset;
            UpdateTitle();
        }

        internal void SetTarget(Asset asset)
        {
            Asset = asset;

            if (asset == null)
            {
                m_Timeline.Reset();
                ChangeLayoutMode(LayoutMode.CreateAsset);
                return;
            }

            ChangeLayoutMode(LayoutMode.ConfigureAndBuildAsset);
        }

        const int k_ClipHighlight = 0;
        const int k_ClipFieldIndex = 1;
        const int k_ClipWarningIndex = 2;

        // For custom display item
        void BindAnimationItem(VisualElement e, int i)
        {
            if (Asset == null)
            {
                return;
            }

            var taggedClips = Asset.AnimationLibrary;
            if (taggedClips.Count <= i)
            {
                return;
            }

            var taggedClip = taggedClips[i];

            //TODO - remove tooltip and reference to AnimationClip?
            AnimationClip clip = taggedClip.AnimationClip;
            var clipValid = taggedClip.Valid;

            (e.ElementAt(k_ClipFieldIndex) as Label).text = taggedClip.ClipName;
            e.ElementAt(k_ClipWarningIndex).style.display = clipValid ? DisplayStyle.None : DisplayStyle.Flex;
            if (clipValid)
            {
                e.tooltip = $"Duration {clip.length:F2}s/{Mathf.RoundToInt(clip.length * clip.frameRate)} frames";
            }
            else
            {
                e.tooltip = TaggedAnimationClip.k_MissingClipText;
            }

            e.userData = taggedClip;
        }

        VisualElement MakeAnimationItem()
        {
            var ve = new VisualElement();

            var highlight = new VisualElement();
            highlight.AddToClassList("animationClipHighlight");
            highlight.style.visibility = Visibility.Hidden;
            ve.Add(highlight);

            var clipLabel = new Label();
            clipLabel.AddToClassList("animationClipLabel");
            clipLabel.text = "Anim";
            ve.Add(clipLabel);

            var warningIcon = new Image();
            warningIcon.AddToClassList("warningImage");
            warningIcon.tooltip = TaggedAnimationClip.k_MissingClipText;
            warningIcon.style.display = DisplayStyle.Flex;
            ve.Add(warningIcon);

            ve.style.flexDirection = FlexDirection.Row;
            ve.style.justifyContent = Justify.SpaceBetween;

            ContextualMenuManipulator itemContext = new ContextualMenuManipulator(evt => BuildAnimationClipItemMenu(evt, ve));
            ve.AddManipulator(itemContext);
            return ve;
        }

        void BuildAnimationClipItemMenu(ContextualMenuPopulateEvent evt, VisualElement ve)
        {
            var clip = ve.userData as TaggedAnimationClip;
            if (clip == null)
            {
                evt.menu.AppendSeparator("Missing Clip");
                return;
            }

            if (m_AnimationLibraryInput.m_ClipSelection.Count > 0)
            {
                if (!m_AnimationLibraryInput.m_ClipSelection.Contains(clip))
                {
                    // We can't change the selection of the list from inside a context menu event
                    return;
                }

                evt.menu.AppendAction("Delete Selected Clip(s)", action => m_AnimationLibraryInput.DeleteSelection(), DropdownMenuAction.AlwaysEnabled);
                evt.menu.AppendSeparator();
                foreach (var tagType in TagAttribute.GetVisibleTypesInInspector())
                {
                    evt.menu.AppendAction($"Tag Selected Clip(s)/{TagAttribute.GetDescription(tagType)}", OnTagSelectionClicked, DropdownMenuAction.AlwaysEnabled, tagType);
                }
            }
            else
            {
                evt.menu.AppendAction("Delete Clip", action =>
                {
                    m_Asset.RemoveClips(new[] { clip });
                    m_AnimationLibraryInput.Refresh();
                }, DropdownMenuAction.AlwaysEnabled);

                evt.menu.AppendSeparator();
                foreach (var tagType in TagAttribute.GetVisibleTypesInInspector())
                {
                    evt.menu.AppendAction($"Tag Clip/{TagAttribute.GetDescription(tagType)}",
                        action => clip.AddTag(tagType),
                        DropdownMenuAction.AlwaysEnabled, tagType);
                }
            }
        }

        public const string k_KinematicaBinaryExtension = "asset";

        bool CreateNewAsset()
        {
            string assetPathAndName = EditorUtility.SaveFilePanelInProject("Choose Kinematica Asset filename",
                string.Empty, k_KinematicaBinaryExtension, string.Empty,
                ScriptableObjectUtility.GetSelectedPathOrFallback());

            if (!string.IsNullOrEmpty(assetPathAndName))
            {
                assetPathAndName = Path.ChangeExtension(assetPathAndName, ".asset");

                var asset = CreateInstance<Asset>();

                AssetDatabase.CreateAsset(asset, assetPathAndName);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorUtility.FocusProjectWindow();

                SetTarget(asset);

                return true;
            }

            return false;
        }

        /*
        void CreateGameObject()
        {
            m_TargetGameObject = new GameObject();
            AddRequiredComponents(m_TargetGameObject);
            SelectGameObject(m_TargetGameObject);
        }
        */

        void SetToolbarEnable(bool enable)
        {
            bool playing = EditorApplication.isPlaying;
            m_EditAssetButton.SetEnabled(enable);
            m_BuildButton.SetEnabled(enable && !playing);
            ShowOrHideAssetDirtyWarning(playing);
        }

        void ShowOrHideAssetDirtyWarning(bool isOrEnteringPlayMode)
        {
            if (isOrEnteringPlayMode && Asset != null && !Asset.BinaryUpToDate)
            {
                m_AssetDirtyWarning.style.display = DisplayStyle.Flex;
            }
            else
            {
                m_AssetDirtyWarning.style.display = DisplayStyle.None;
            }
        }

        void EditAsset()
        {
            Debug.Assert(Asset != null);
            Selection.activeObject = Asset;
        }

        void BuildAsset()
        {
            Debug.Assert(Asset != null);
            Asset.Build();
        }

        /*
        void AddRequiredComponents(GameObject obj)
        {
            if (KinematicaLibrarySettings == null)
            {
                return;
            }

            //TODO - MotionSynthesizer is no longer a component, need to update this section
            /*
            var synthesizer = obj.GetComponent<MotionSynthesizer>();
            if (synthesizer == null)
            {
                synthesizer = obj.AddComponent<MotionSynthesizer>();
            }

            synthesizer.kinematicaAsset = KinematicaLibrarySettings;
        }
            */

        void OnAssetDeserialized(Asset asset)
        {
            AnimationClipListView.MarkClipListsForUpdate(new[] { m_AnimationLibraryInput});
            if (asset != null)
            {
                m_Timeline?.OnAssetModified();
            }
        }

        static string GetPrefKeyName(string propName)
        {
            return $"Kinematica_{nameof(BuilderWindow)}_{propName}";
        }
    }
}
