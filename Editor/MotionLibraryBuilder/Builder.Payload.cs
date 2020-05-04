using Unity.Mathematics;

namespace Unity.Kinematica.Editor
{
    internal partial class Builder
    {
        internal class PayloadBuilder : Editor.PayloadBuilder
        {
            public PayloadBuilder(Builder builder, AnimationSampler sampler)
            {
                this.builder = builder;

                this.sampler = sampler;

                interval = Interval.Empty;
            }

            public Interval interval
            {
                get; internal set;
            }

            public AnimationSampler sampler
            {
                get; private set;
            }

            public float sampleTimeInSeconds
            {
                get; internal set;
            }

            public int GetJointIndexForName(string jointName)
            {
                var stringTable = builder.stringTable;
                int nameIndex = stringTable.GetStringIndex(jointName);
                if (nameIndex < 0)
                {
                    return -1;
                }

                ref Binary binary = ref builder.Binary;

                return binary.animationRig.GetJointIndexForNameIndex(nameIndex);
            }

            public int FrameIndex
            {
                get => interval.FirstFrame;
            }

            public float SourceToTargetScale
            {
                get
                {
                    return sampler.SourceToTargetScale;
                }
            }

            public AffineTransform GetRootTransform()
            {
                return sampler.GetRootTransform();
            }

            public AffineTransform GetJointTransformCharacterSpace(string jointName)
            {
                return sampler.GetJointCharacterSpace(jointName, sampleTimeInSeconds);
            }

            public AffineTransform GetTrajectoryTransform()
            {
                ref Binary binary = ref builder.Binary;

                return binary.GetTrajectoryTransform(FrameIndex);
            }

            public AffineTransform GetJointTransform(int jointIndex)
            {
                ref Binary binary = ref builder.Binary;

                return binary.GetJointTransform(jointIndex, FrameIndex);
            }

            public AffineTransform GetTrajectoryTransform(int frameIndex)
            {
                ref Binary binary = ref builder.Binary;

                return binary.GetTrajectoryTransform(frameIndex);
            }

            public AffineTransform GetJointTransform(int frameIndex, int jointIndex)
            {
                ref Binary binary = ref builder.Binary;

                return binary.GetJointTransform(jointIndex, frameIndex);
            }

            Builder builder;
        }
    }
}
