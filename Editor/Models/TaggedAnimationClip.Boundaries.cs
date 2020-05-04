using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Unity.Kinematica.Editor
{
    partial class TaggedAnimationClip
    {
        //TODO - these clips will eventually be applied on a per-segment basis. They are set in the timeline and can be null
        [SerializeField]
        AnimationClip m_PreBoundaryClip;
        [SerializeField]
        SerializableGuid m_PreBoundaryClipGuid;

        public AnimationClip PreBoundaryClip
        {
            get
            {
                if (m_PreBoundaryClip == null && m_TaggedPreBoundaryClip != null)
                {
                    m_TaggedPreBoundaryClip = null; //Can become out of sync during undo/redo operations
                }

                if (m_PreBoundaryClip == null)
                {
                    m_PreBoundaryClip = LoadAnimationClipFromGuid(m_PreBoundaryClipGuid);
                }
                else if (!m_PreBoundaryClipGuid.IsSet())
                {
                    SerializeGuidStr(m_PreBoundaryClip, ref m_PreBoundaryClipGuid);
                }

                return m_PreBoundaryClip;
            }
            set
            {
                m_PreBoundaryClip = value;
                SerializeGuidStr(m_PreBoundaryClip, ref m_PreBoundaryClipGuid);
                NotifyChanged();
            }
        }
        public SerializableGuid PreBoundaryClipGuid
        {
            get
            {
                return m_PreBoundaryClipGuid;
            }
        }

        [SerializeField]
        AnimationClip m_PostBoundaryClip;
        [SerializeField]
        SerializableGuid m_PostBoundaryClipGuid;

        public AnimationClip PostBoundaryClip
        {
            get
            {
                if (m_PostBoundaryClip == null && m_TaggedPostBoundaryClip != null)
                {
                    m_TaggedPostBoundaryClip = null;//Can become out of sync during undo/redo operations
                }

                if (m_PostBoundaryClip == null)
                {
                    m_PostBoundaryClip = LoadAnimationClipFromGuid(m_PostBoundaryClipGuid);
                }
                else if (!m_PostBoundaryClipGuid.IsSet())
                {
                    SerializeGuidStr(m_PostBoundaryClip, ref m_PostBoundaryClipGuid);
                }

                return m_PostBoundaryClip;
            }
            set
            {
                m_PostBoundaryClip = value;
                SerializeGuidStr(m_PostBoundaryClip, ref m_PostBoundaryClipGuid);
                NotifyChanged();
            }
        }

        public SerializableGuid PostBoundaryClipGuid
        {
            get
            {
                return m_PostBoundaryClipGuid;
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
                        m_TaggedPreBoundaryClip = Asset.AnimationLibrary.FirstOrDefault(tc => tc.AnimationClipGuid == m_PreBoundaryClipGuid);
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
                        m_TaggedPostBoundaryClip = Asset.AnimationLibrary.FirstOrDefault(tc => tc.AnimationClipGuid == m_PostBoundaryClipGuid);
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
