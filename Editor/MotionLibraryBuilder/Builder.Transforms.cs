using System;

using UnityEngine.Assertions;

using UnityEditor;

using Unity.Mathematics;

namespace Unity.Kinematica.Editor
{
    internal partial class Builder
    {
        public void BuildTransforms()
        {
            //
            // Now re-sample all animations according to the target
            // sample rate and adjust the segment information to
            // reference the transforms array.
            //

            var transforms = GenerateTransforms();

            int numTransforms = transforms.Length;

            ref Binary binary = ref Binary;

            allocator.Allocate(numTransforms, ref binary.transforms);

            for (int i = 0; i < numTransforms; ++i)
            {
                binary.transforms[i] = transforms[i];
            }
        }

        AffineTransform[] GenerateTransforms()
        {
            //
            // Calculate total number of poses to be generated
            // (based on previously generated segments)
            //

            int numJoints = rig.NumJoints;
            if (numJoints < 2)
            {
                throw new ArgumentException($"Rig does not have enough joints. Only {numJoints} present.");
            }

            if (segments.NumFrames == 0)
            {
                throw new Exception("No animation frame to process, please make sure there is at least one single non-empty tag in your Kinematica asset.");
            }

            int numTransforms = segments.NumFrames * numJoints;

            var transforms = new AffineTransform[numTransforms];

            ref AffineTransform JointTransform(int jointIndex, int frameIndex)
            {
                Assert.IsTrue(jointIndex < numJoints);
                Assert.IsTrue(frameIndex < segments.NumFrames);

                return ref transforms[jointIndex * segments.NumFrames + frameIndex];
            }

            int destinationIndex = 0;

            AnimationSampler sampler = null;

            foreach (var segment in segments.ToArray())
            {
                sampler = GetAnimationSampler(segment, sampler);

                var clip = sampler.AnimationClip;

                float sourceSampleRate = clip.frameRate;
                float targetSampleRate = asset.SampleRate;
                float sampleRateRatio = sourceSampleRate / targetSampleRate;

                int firstFrame = segment.source.FirstFrame;
                int numFramesSource = segment.source.NumFrames;
                int numFramesDestination =
                    Missing.roundToInt(numFramesSource / sampleRateRatio);

                Assert.IsTrue(segment.destination.FirstFrame == destinationIndex);
                Assert.IsTrue(segment.destination.NumFrames == numFramesDestination);

                float baseSampleTimeInSeconds = firstFrame / sourceSampleRate;
                float clipDuration = segment.clip.DurationInSeconds;
                int numFrames = Missing.truncToInt(clip.frameRate * clipDuration);
                int lastKeyFrame = numFrames - 1;
                float maximumSampleTimeInSeconds = numFrames / sourceSampleRate;

                float SampleTimeInSeconds(float sampleTimeInSeconds)
                {
                    Assert.IsTrue(sampleTimeInSeconds <= maximumSampleTimeInSeconds);
                    int sampleKeyFrame = Missing.truncToInt(sampleTimeInSeconds * sourceSampleRate);
                    Assert.IsTrue(sampleKeyFrame < numFrames);
                    if (sampleKeyFrame == lastKeyFrame)
                    {
                        return sampleKeyFrame / sourceSampleRate;
                    }
                    return sampleTimeInSeconds;
                }

                ref TransformBuffer transformBuffer = ref sampler.TransformBuffer;

                for (int i = 0; i < numFramesDestination; ++i)
                {
                    float progress = (float)destinationIndex / (float)segments.NumFrames;
                    EditorUtility.DisplayProgressBar("Motion Synthesizer Asset",
                        "Building joint transforms", progress);

                    float offsetSampleTimeInSeconds = i * sampleRateRatio / sourceSampleRate;
                    float sampleTimeInSeconds = SampleTimeInSeconds(
                        baseSampleTimeInSeconds + offsetSampleTimeInSeconds);

                    sampler.SamplePose(sampleTimeInSeconds);

                    //
                    // Now accumulate all transforms in the transform buffer
                    // and output them into the final transforms array.
                    //

                    JointTransform(0, destinationIndex) = transformBuffer[0];

                    transformBuffer[0] = AffineTransform.identity;
                    AccumulateTransforms(ref transformBuffer, rig);

                    for (int k = 1; k < numJoints; ++k)
                    {
                        JointTransform(k, destinationIndex) = transformBuffer[k];
                    }

                    destinationIndex++;
                }
            }

            sampler.Dispose();

            Assert.IsTrue(destinationIndex == segments.NumFrames);

            return transforms;
        }

        AnimationSampler GetAnimationSampler(Segment segment, AnimationSampler sampler)
        {
            var avatar = segment.GetSourceAvatar(asset);

            Assert.IsTrue(factories.ContainsKey(avatar));

            var factory = factories[avatar];

            var animationClip = segment.clip.AnimationClip;

            if (sampler == null || sampler.AnimationClip != animationClip)
            {
                if (sampler != null)
                {
                    sampler.Dispose();
                }

                sampler = factory.Create(animationClip);
            }

            return sampler;
        }

        static void AccumulateTransforms(ref TransformBuffer transformBuffer, AnimationRig rig)
        {
            Assert.IsTrue(transformBuffer.Length == rig.NumJoints);
            Assert.IsTrue(rig.GetParentJointIndex(0) == -1);

            for (int i = 1; i < rig.NumJoints; ++i)
            {
                int parentJointIndex = rig.GetParentJointIndex(i);
                Assert.IsTrue(parentJointIndex < i);

                AffineTransform src = transformBuffer[i];
                AffineTransform dst = transformBuffer[parentJointIndex];

                transformBuffer[i] = new AffineTransform(
                    dst.t + Missing.rotateVector(dst.q, src.t),
                    math.mul(dst.q, src.q));
            }
        }
    }
}
