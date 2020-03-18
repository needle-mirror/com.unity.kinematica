using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Unity.Kinematica.Editor
{
    internal partial class TaggedAnimationClip
    {
        //TODO - these clips will eventually be applied on a per-segment basis. They are set in the timeline and can be null
        [SerializeField]
        AnimationClip m_PreBoundaryClip;

        public AnimationClip PreBoundaryClip
        {
            get
            {
                if (m_PreBoundaryClip == null && m_TaggedPreBoundaryClip != null)
                {
                    m_TaggedPreBoundaryClip = null; //Can become out of sync during undo/redo operations
                }

                return m_PreBoundaryClip;
            }
            set
            {
                m_PreBoundaryClip = value;
                NotifyChanged();
            }
        }

        [SerializeField]
        AnimationClip m_PostBoundaryClip;

        public AnimationClip PostBoundaryClip
        {
            get
            {
                if (m_PostBoundaryClip == null && m_TaggedPostBoundaryClip != null)
                {
                    m_TaggedPostBoundaryClip = null;//Can become out of sync during undo/redo operations
                }

                return m_PostBoundaryClip;
            }
            set
            {
                m_PostBoundaryClip = value;
                NotifyChanged();
            }
        }

        TaggedAnimationClip m_TaggedPreBoundaryClip;

        public TaggedAnimationClip TaggedPreBoundaryClip
        {
            get
            {
                if (m_TaggedPreBoundaryClip == null)
                {
                    if (Asset != null)
                    {
                        m_TaggedPreBoundaryClip = Asset.AnimationLibrary.FirstOrDefault(tc => tc.AnimationClip == m_PreBoundaryClip);
                    }
                }

                return m_TaggedPreBoundaryClip;
            }
            set
            {
                Undo.RecordObject(Asset, "Set Boundary Clip");
                m_TaggedPreBoundaryClip = value;
                PreBoundaryClip = m_TaggedPreBoundaryClip?.AnimationClip;
            }
        }

        TaggedAnimationClip m_TaggedPostBoundaryClip;

        public TaggedAnimationClip TaggedPostBoundaryClip
        {
            get
            {
                if (m_TaggedPostBoundaryClip == null)
                {
                    if (Asset != null)
                    {
                        m_TaggedPostBoundaryClip = Asset.AnimationLibrary.FirstOrDefault(tc => tc.AnimationClip == m_PostBoundaryClip);
                    }
                }

                return m_TaggedPostBoundaryClip;
            }
            set
            {
                Undo.RecordObject(Asset, "Set Boundary Clip");
                m_TaggedPostBoundaryClip = value;
                PostBoundaryClip = m_TaggedPostBoundaryClip?.AnimationClip;
            }
        }
    }
}
