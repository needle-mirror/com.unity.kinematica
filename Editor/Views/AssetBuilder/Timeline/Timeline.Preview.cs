using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Kinematica.Editor
{
    partial class Timeline
    {
        bool m_IsPreviewing;

        bool m_PreviewEnabled;

        public event Action<bool> PreviewEnabledChangeEvent;

        public bool PreviewEnabled
        {
            get { return m_PreviewEnabled; }
            set
            {
                if (m_PreviewEnabled != value)
                {
                    m_PreviewEnabled = value;
                    OnPreviewSettingChanged();
                    PreviewEnabledChangeEvent?.Invoke(PreviewEnabled);
                }
            }
        }

        GameObject m_PreviewTarget;

        Preview m_Preview;

        public event Action PreviewDisposed;

        void PreviewTargetInvalidated()
        {
            if (m_Preview != null)
            {
                m_Preview.PreviewInvalidated -= PreviewTargetInvalidated;
            }

            DisposePreviews();
            PreviewDisposed?.Invoke();
        }

        void DisposePreviews()
        {
            if (m_Preview != null)
            {
                m_Preview.Dispose();
                m_Preview = null;
            }

            m_BoundaryClipTrack?.DisposePreviews();
        }

        void OnPreviewSettingChanged()
        {
            if (PreviewEnabled && CanPreview())
            {
                UpdatePreview();
            }
            else
            {
                DisposePreviews();
            }
        }

        void OnPreviewTargetSelectorChanged(ChangeEvent<GameObject> evt)
        {
            PreviewTarget = evt.newValue;
        }

        void UpdatePreview()
        {
            if (PreviewEnabled && CanPreview())
            {
                if (m_Preview == null)
                {
                    try
                    {
                        m_Preview = Preview.CreatePreview(TargetAsset, m_PreviewTarget);
                        m_Preview.PreviewInvalidated += PreviewTargetInvalidated;
                        PreviewActiveTime();
                    }
                    catch (Exception e)
                    {
                        Debug.Log(e.Message);
                    }
                }
                else
                {
                    PreviewActiveTime();
                }
            }
        }

        internal void PreviewActiveTime()
        {
            if (CanPreview())
            {
                PreviewEnabled = true;
                m_BoundaryClipTrack.ValidateBoundaryPreviews();

                m_Preview?.DisableDisplayTrajectory();

                if (!m_BoundaryClipTrack.Preview(ActiveTime))
                {
                    m_Preview?.PreviewTime(TaggedClip, ActiveTime);
                }
            }
        }
    }
}
