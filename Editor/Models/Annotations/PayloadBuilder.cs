using Unity.Mathematics;

namespace Unity.Kinematica.Editor
{
    public interface PayloadBuilder
    {
        int GetJointIndexForName(string jointName);

        AffineTransform GetRootTransform();

        AffineTransform GetJointTransformCharacterSpace(string jointName);

        AffineTransform GetTrajectoryTransform();

        AffineTransform GetJointTransform(int jointIndex);

        AffineTransform GetTrajectoryTransform(int frameIndex);

        AffineTransform GetJointTransform(int frameIndex, int jointIndex);

        int FrameIndex { get; }

        float SourceToTargetScale { get; }
    }
}
