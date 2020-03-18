using System;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Unity.Kinematica.Editor
{
    partial class Timeline
    {
        bool m_IsPreviewing;

        GameObject m_PreviewTarget;

        Preview m_Preview;
        Preview m_PreBoundaryPreview;
        Preview m_PostBoundaryPreview;

        PreviewState m_PreviewStatus;
        PreviewState PreviewStatus
        {
            get { return m_PreviewStatus; }
            set
            {
                if (m_PreviewStatus != value)
                {
                    m_PreviewStatus = value;
                    UpdateToolbarToggle(m_PreviewStatus);
                }
            }
        }

        void DisposePreviews()
        {
            if (m_Preview != null)
            {
                m_Preview.Dispose();
                m_Preview = null;
            }

            if (m_PreBoundaryPreview != null)
            {
                m_PreBoundaryPreview.Dispose();
                m_PreBoundaryPreview = null;
            }

            if (m_PostBoundaryPreview != null)
            {
                m_PostBoundaryPreview.Dispose();
                m_PostBoundaryPreview = null;
            }
        }

        void OnPreviewModeChanged(ChangeEvent<bool> evt)
        {
            Previewing = evt.newValue;
            PreviewStatus = evt.newValue ? PreviewState.Active : PreviewState.Enabled;
        }

        void SyncPreviewStatusToCanPreview()
        {
            if (CanPreview())
            {
                if (!Previewing)
                {
                    PreviewStatus = PreviewState.Enabled;
                }
            }
            else
            {
                Previewing = false;
                PreviewStatus = PreviewState.Disabled;
            }
        }

        void OnPreviewTargetSelectorChanged(ChangeEvent<Object> evt)
        {
            PreviewTarget = evt.newValue as GameObject;
        }

        void UpdatePreview()
        {
            if (m_IsPreviewing)
            {
                if (m_Preview == null)
                {
                    //TODO - check that prefabTarget isn't null here
                    if (TargetAsset.DestinationAvatar == null)
                    {
                        Debug.Log("Cannot preview no target avatar selected!");
                        return;
                    }

                    try
                    {
                        m_Preview = Preview.CreatePreview(TargetAsset, m_PreviewTarget);
                    }
                    catch (Exception e)
                    {
                        Debug.Log(e.Message);
                    }

                    PreviewActiveTime();
                }
            }
        }

        void ValidateBoundaryPreviews()
        {
            if (m_PreBoundaryPreview == null)
            {
                if (TaggedClip.TaggedPreBoundaryClip != null)
                {
                    m_PreBoundaryPreview = Preview.CreatePreview(TargetAsset, PreviewTarget);
                }
            }
            else if (TaggedClip.TaggedPreBoundaryClip == null)
            {
                m_PreBoundaryPreview?.Dispose();
                m_PreBoundaryPreview = null;
            }

            if (m_PostBoundaryPreview == null)
            {
                if (TaggedClip.TaggedPostBoundaryClip != null)
                {
                    m_PostBoundaryPreview = Preview.CreatePreview(TargetAsset, PreviewTarget);
                }
            }
            else if (TaggedClip.TaggedPostBoundaryClip == null)
            {
                m_PostBoundaryPreview?.Dispose();
                m_PostBoundaryPreview = null;
            }
        }

        void PreviewActiveTime()
        {
            if (CanPreview())
            {
                ValidateBoundaryPreviews();

                m_PreBoundaryPreview?.DisableDisplayTrajectory();
                m_Preview?.DisableDisplayTrajectory();
                m_PostBoundaryPreview?.DisableDisplayTrajectory();

                if (ActiveTime < 0)
                {
                    if (m_PreBoundaryPreview != null)
                    {
                        m_PreBoundaryPreview.PreviewTime(m_PreBoundaryClipElement.Selection, ActiveTime + m_PreBoundaryClipElement.Selection.DurationInSeconds);
                    }
                }
                else if (ActiveTime > TaggedClip.DurationInSeconds)
                {
                    if (m_PostBoundaryPreview != null)
                    {
                        m_PostBoundaryPreview.PreviewTime(m_PostBoundaryClipElement.Selection, ActiveTime - TaggedClip.DurationInSeconds);
                    }
                }
                else
                {
                    m_Preview.PreviewTime(TaggedClip, ActiveTime);
                }
            }
        }

        void UpdatePreviewStateBasedOnPreviewStatus()
        {
            bool previewing = Previewing;
            if (previewing == false && PreviewStatus == PreviewState.Active)
            {
                PreviewStatus = PreviewState.Enabled;
            }
            else if (previewing == true && PreviewStatus == PreviewState.Enabled)
            {
                PreviewStatus = PreviewState.Active;
            }
        }

        void UpdateToolbarToggle(PreviewState state)
        {
            switch (state)
            {
                case PreviewState.Disabled:
                    m_PreviewToggle.SetEnabled(false);
                    break;
                case PreviewState.Enabled:
                    m_PreviewToggle.SetEnabled(true);
                    m_PreviewToggle.SetValueWithoutNotify(false);
                    break;
                case PreviewState.Active:
                    m_PreviewToggle.SetEnabled(true);
                    m_PreviewToggle.SetValueWithoutNotify(true);
                    break;
            }
        }
    }
}
