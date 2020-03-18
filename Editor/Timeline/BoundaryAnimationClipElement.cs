using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Kinematica.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Timeline
{
    class BoundaryAnimationClipElement : Button
    {
        const string k_DefaultLabel = "Select boundary clip";
        readonly List<TaggedAnimationClip> m_Clips = new List<TaggedAnimationClip>();
        TaggedAnimationClip m_Selection;

        public TaggedAnimationClip Selection
        {
            get { return m_Selection; }
        }

        public BoundaryAnimationClipElement()
        {
            AddToClassList("boundaryClip");
            clickable = null;

            var selectClipContextManipulator = new ContextualMenuManipulator(evt =>
            {
                if (m_Clips != null)
                {
                    foreach (var clip in m_Clips)
                    {
                        var statusCallback = clip == m_Selection ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal;
                        evt.menu.AppendAction(clip.ClipName, dropDownMenuAction =>
                        {
                            Select(dropDownMenuAction.userData as TaggedAnimationClip);
                            SelectionChanged?.Invoke(m_Selection);
                        }, e => statusCallback, clip);
                    }
                }
            });

            selectClipContextManipulator.activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse});

            this.AddManipulator(selectClipContextManipulator);

            Reset();
        }

        public event Action<TaggedAnimationClip> SelectionChanged;

        public void Select(AnimationClip selection)
        {
            var found = m_Clips.FirstOrDefault(tc => tc.AnimationClip == selection);
            m_Selection = found;
            UpdateLabel();
        }

        internal void Select(TaggedAnimationClip selection)
        {
            if (!m_Clips.Contains(selection))
            {
                selection = null;
            }

            m_Selection = selection;
        }

        public event Action LabelUpdated;
        void UpdateLabel()
        {
            if (m_Selection == null || string.IsNullOrEmpty(m_Selection.ClipName))
            {
                text = k_DefaultLabel;
            }
            else
            {
                text = m_Selection.ClipName;
            }

            EditorApplication.delayCall += () => { LabelUpdated?.Invoke(); };
        }

        public void SetClips(List<TaggedAnimationClip> newClips)
        {
            int index = m_Clips.FindIndex(clip =>
            {
                if (!string.IsNullOrEmpty(clip.ClipName))
                {
                    return clip.ClipName == text;
                }

                return false;
            });

            m_Clips.Clear();
            m_Clips.AddRange(newClips);
            if (index >= 0)
            {
                if (m_Clips.All(t =>
                {
                    if (!string.IsNullOrEmpty(t.ClipName))
                    {
                        return t.ClipName != text;
                    }

                    return true;
                }))
                {
                    if (m_Clips.Count >= index)
                    {
                        if (!string.IsNullOrEmpty(m_Clips[index].ClipName))
                        {
                            text = m_Clips[index].ClipName;
                        }
                    }
                }
            }
        }

        public void Reset()
        {
            m_Selection = null;
            m_Clips.Clear();
            text = k_DefaultLabel;
        }
    }
}
