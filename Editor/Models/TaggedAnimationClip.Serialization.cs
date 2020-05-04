namespace Unity.Kinematica.Editor
{
    partial class TaggedAnimationClip
    {
        internal void ConvertToV2()
        {
            if (!m_AnimationClipReference.isBroken)
            {
                SerializeGuidStr(m_AnimationClipReference.asset, ref m_AnimationClipGuid);
            }

            if (m_PreBoundaryClip != null)
            {
                SerializeGuidStr(m_PreBoundaryClip, ref m_PreBoundaryClipGuid);
            }

            if (m_PostBoundaryClip != null)
            {
                SerializeGuidStr(m_PostBoundaryClip, ref m_PostBoundaryClipGuid);
            }
        }
    }
}
