using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

using Object = System.Object;

namespace Unity.Kinematica.Editor
{
    class AnimationClipListView : ListView
    {
        class AnimationClipListAssetModificationProcessor : UnityEditor.AssetModificationProcessor
        {
            static readonly List<AnimationClipListView> k_ClipLists = new List<AnimationClipListView>();

            public static void AddListView(AnimationClipListView lv)
            {
                k_ClipLists.Add(lv);
            }

            public static void RemoveListView(AnimationClipListView lv)
            {
                k_ClipLists.Remove(lv);
            }

            static void OnWillCreateAsset(string assetName)
            {
                MarkClipListsForUpdate(k_ClipLists);
            }

            static AssetDeleteResult OnWillDeleteAsset(string s, RemoveAssetOptions options)
            {
                MarkClipListsForUpdate(k_ClipLists);
                return AssetDeleteResult.DidNotDelete;
            }
        }

        internal static void MarkClipListsForUpdate(IEnumerable<AnimationClipListView> listViews)
        {
            EditorApplication.delayCall += () =>
            {
                foreach (var list in listViews)
                {
                    list.Refresh();
                }
            };
        }

        public new class UxmlFactory : UxmlFactory<AnimationClipListView, UxmlTraits> {}

        public new class UxmlTraits : BindableElement.UxmlTraits {}

        public AnimationClipListView()
        {
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            onSelectionChanged += OnAnimationClipSelectionChanged;
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            RegisterCallback<DragUpdatedEvent>(OnDragUpdate);
            RegisterCallback<DragPerformEvent>(OnDragPerform);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);

            AnimationClipListAssetModificationProcessor.AddListView(this);
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            UnregisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            UnregisterCallback<DragUpdatedEvent>(OnDragUpdate);
            UnregisterCallback<DragPerformEvent>(OnDragPerform);

            AnimationClipListAssetModificationProcessor.RemoveListView(this);
        }

        public void SelectItem(Object obj)
        {
            if (itemsSource == null)
            {
                return;
            }

            var index = itemsSource.IndexOf(obj);
            if (index >= 0)
            {
                SetSelection(index);
            }
        }

        internal BuilderWindow m_Window;

        internal List<TaggedAnimationClip> m_ClipSelection = new List<TaggedAnimationClip>();

        void OnAnimationClipSelectionChanged(List<object> animationClips)
        {
            m_ClipSelection.Clear();
            foreach (var c in animationClips.Cast<TaggedAnimationClip>())
            {
                m_ClipSelection.Add(c);
            }
        }

        void OnDragUpdate(DragUpdatedEvent evt)
        {
            if (!EditorApplication.isPlaying && DragAndDrop.objectReferences.Any(x => x is AnimationClip))
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Move;
            }
        }

        void OnDragPerform(DragPerformEvent evt)
        {
            if (EditorApplication.isPlaying)
            {
                return;
            }

            var clips = DragAndDrop.objectReferences.OfType<AnimationClip>().ToList();
            var taggedClips = TaggedAnimationClip.BuildFromClips(clips, m_Window.Asset, ETagImportOption.AddDefaultTag);
            if (taggedClips.Any())
            {
                if (m_Window.Asset != null)
                {
                    m_Window.Asset.AddClips(taggedClips);
                    Refresh();
                }
            }
        }

        protected override void ExecuteDefaultActionAtTarget(EventBase evt)
        {
            base.ExecuteDefaultActionAtTarget(evt);

            if (evt is KeyDownEvent keyDownEvt)
            {
                if (keyDownEvt.keyCode == KeyCode.Delete && !EditorApplication.isPlaying)
                {
                    DeleteSelection();
                }
            }
        }

        internal void DeleteSelection()
        {
            int currentSelection = selectedIndex;
            m_Window.Asset.RemoveClips(m_ClipSelection);
            m_ClipSelection.Clear();
            ClearSelection();
            Refresh();
            while (currentSelection >= m_Window.Asset.AnimationLibrary.Count)
            {
                --currentSelection;
            }

            selectedIndex = currentSelection;
        }

        internal void UpdateSource(IList source)
        {
            itemsSource = source;
            int itemCount = 0;
            if (itemsSource != null)
            {
                itemCount = itemsSource.OfType<TaggedAnimationClip>().Count();
            }

            if (itemCount > 0)
            {
                selectedIndex = Mathf.Min(selectedIndex, itemCount - 1);
            }
        }
    }
}
