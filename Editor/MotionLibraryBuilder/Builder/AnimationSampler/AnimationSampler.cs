using System;

using UnityEngine;
using UnityEditor;

using Unity.Mathematics;
using Unity.Collections;

namespace Unity.Kinematica.Editor
{
    internal class AnimationSampler : IDisposable
    {
        AnimationClip animationClip;

        AnimationSamplerFactory factory;

        MemoryHeader<TransformBuffer> transformBuffer;

        public struct Transform
        {
            public struct Position
            {
                public AnimationCurve x;
                public AnimationCurve y;
                public AnimationCurve z;

                public float3 Evaluate(float sampleTimeInSeconds, float3 t)
                {
                    return new float3(
                        x != null ? x.Evaluate(sampleTimeInSeconds) : t.x,
                        y != null ? y.Evaluate(sampleTimeInSeconds) : t.y,
                        z != null ? z.Evaluate(sampleTimeInSeconds) : t.z);
                }
            }

            public struct Rotation
            {
                public AnimationCurve x;
                public AnimationCurve y;
                public AnimationCurve z;
                public AnimationCurve w;

                public quaternion Evaluate(float sampleTimeInSeconds, quaternion q)
                {
                    return new quaternion(
                        x != null ? x.Evaluate(sampleTimeInSeconds) : q.value.x,
                        y != null ? y.Evaluate(sampleTimeInSeconds) : q.value.y,
                        z != null ? z.Evaluate(sampleTimeInSeconds) : q.value.z,
                        w != null ? w.Evaluate(sampleTimeInSeconds) : q.value.w);
                }
            }

            public Position position;
            public Rotation rotation;

            public AffineTransform Evaluate(float sampleTimeInSeconds, AffineTransform defaultTransform)
            {
                return new AffineTransform(
                    position.Evaluate(sampleTimeInSeconds, defaultTransform.t),
                    rotation.Evaluate(sampleTimeInSeconds, defaultTransform.q));
            }

            public void MapEditorCurve(string curveName, string posCurvePrefix, string rotCurvePrefix, AnimationCurve curve)
            {
                string[] curveNameStrings = curveName.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
                if (curveNameStrings.Length != 2)
                {
                    return;
                }

                string curvePrefix = curveNameStrings[0];
                string curvePostfix = curveNameStrings[1];

                if (curvePrefix == posCurvePrefix)
                {
                    if (curvePostfix == "x")
                    {
                        position.x = curve;
                    }
                    else if (curvePostfix == "y")
                    {
                        position.y = curve;
                    }
                    else if (curvePostfix == "z")
                    {
                        position.z = curve;
                    }
                }
                else if (curvePrefix == rotCurvePrefix)
                {
                    if (curvePostfix == "x")
                    {
                        rotation.x = curve;
                    }
                    else if (curvePostfix == "y")
                    {
                        rotation.y = curve;
                    }
                    else if (curvePostfix == "z")
                    {
                        rotation.z = curve;
                    }
                    else if (curvePostfix == "w")
                    {
                        rotation.w = curve;
                    }
                }
            }
        }

        Transform[] transforms;

        public ref TransformBuffer TransformBuffer
        {
            get => ref transformBuffer.Ref;
        }

        public AnimationClip AnimationClip
        {
            get => animationClip;
        }

        public void Dispose()
        {
            if (transformBuffer.IsValid)
            {
                transformBuffer.Dispose();
            }
        }

        public void SamplePose(float sampleTimeInSeconds)
        {
            var targetRig = factory.TargetRig;

            if (factory.NeedsRetargeting)
            {
                var retargeter = factory.Retargeter;

                var sourceRig = retargeter.SourceAvatarRig;

                var sourceTransformBuffer =
                    TransformBuffer.Create(
                        sourceRig.NumJoints, Allocator.Temp);

                SamplePoseInternal(sampleTimeInSeconds,
                    sourceRig, ref sourceTransformBuffer.Ref);

                retargeter.RetargetPose(ref sourceTransformBuffer.Ref, ref transformBuffer.Ref);

                sourceTransformBuffer.Dispose();
            }
            else
            {
                SamplePoseInternal(sampleTimeInSeconds,
                    targetRig, ref transformBuffer.Ref);
            }
        }

        public AffineTransform SampleTrajectory(float sampleTimeInSeconds)
        {
            if (factory.NeedsRetargeting)
            {
                var retargeter = factory.Retargeter;

                var sourceRig = retargeter.SourceAvatarRig;

                AffineTransform trajectory = SampleTrajectoryInternal(sampleTimeInSeconds, sourceRig);

                return retargeter.RetargetTrajectory(trajectory);
            }
            else
            {
                return SampleTrajectoryInternal(sampleTimeInSeconds, factory.TargetRig);
            }
        }

        public float SourceToTargetScale
        {
            get
            {
                if (factory.NeedsRetargeting)
                {
                    return factory.Retargeter.GetSourceToTargetScale();
                }

                return 1.0f;
            }
        }

        public AffineTransform GetJointCharacterSpace(string sourceJointName, float sampleTimeInSeconds)
        {
            if (factory.NeedsRetargeting)
            {
                var retargeter = factory.Retargeter;

                var sourceRig = retargeter.SourceAvatarRig;

                int sourceJointIndex = sourceRig.GetJointIndexFromName(sourceJointName);
                if (sourceJointIndex < 0)
                {
                    Debug.LogError($"Error sampling animation, joint {sourceJointName} not found in rig");
                    return AffineTransform.identity;
                }

                AffineTransform sourceOffset = AffineTransform.identity;

                int targetJointIndex = retargeter.GetSourceToTargetJointIndex(sourceJointIndex);
                while (targetJointIndex < 0)
                {
                    // if the source joint is not mapped to target rig, we navigate along source joint hierarchy upward until we find a parent source joint that is mapped to target rig (or we reach root)
                    // We accumulate a source offset in the meantime, that will be scaled and added to the closest mapped target joint

                    AffineTransform sourceJointPS = GetSourceJointParentSpace(sourceJointIndex, sampleTimeInSeconds);
                    sourceOffset = sourceJointPS * sourceOffset;
                    int parentSourceJointIndex = sourceRig.GetParentJointIndex(sourceJointIndex);

                    if (parentSourceJointIndex == 0)
                    {
                        return sourceOffset * retargeter.GetSourceToTargetScale();
                    }

                    sourceJointIndex = parentSourceJointIndex;
                    targetJointIndex = retargeter.GetSourceToTargetJointIndex(sourceJointIndex);
                }

                return GetTargetJointCharacterSpace(
                    targetJointIndex, sampleTimeInSeconds) * (sourceOffset * retargeter.GetSourceToTargetScale());
            }
            else
            {
                var targetRig = factory.TargetRig;

                int targetJointIndex = targetRig.GetJointIndexFromName(sourceJointName);
                if (targetJointIndex < 0)
                {
                    Debug.LogError($"Error sampling animation, joint {sourceJointName} not found in rig");
                    return AffineTransform.identity;
                }

                return GetTargetJointCharacterSpace(targetJointIndex, sampleTimeInSeconds);
            }
        }

        public AffineTransform GetRootTransform()
        {
            return transformBuffer.Ref[0];
        }

        AffineTransform GetSourceJointParentSpace(int sourceJointIndex, float sampleTimeInSeconds)
        {
            var retargeter = factory.Retargeter;

            var rig = factory.NeedsRetargeting ? retargeter.SourceAvatarRig : factory.TargetRig;

            return transforms[sourceJointIndex].Evaluate(
                sampleTimeInSeconds, rig.Joints[sourceJointIndex].localTransform);
        }

        AffineTransform GetTargetJointCharacterSpace(int targetJointIndex, float sampleTimeInSeconds)
        {
            var targetRig = factory.TargetRig;

            AffineTransform jointTransform = AffineTransform.identity;

            while (targetJointIndex > 0)
            {
                jointTransform = transformBuffer.Ref[targetJointIndex] * jointTransform;
                targetJointIndex = targetRig.GetParentJointIndex(targetJointIndex);
            }

            return jointTransform;
        }

        void SamplePoseInternal(float sampleTimeInSeconds, AnimationRig rig, ref TransformBuffer transformBuffer)
        {
            for (int i = 0; i < rig.NumJoints; ++i)
            {
                transformBuffer[i] =
                    transforms[i].Evaluate(
                        sampleTimeInSeconds,
                        rig.Joints[i].localTransform);
            }

            transformBuffer[0] = ApplyImportOptionsOnTrajectory(transformBuffer[0]);

            if (!IsRootInTrajectorySpace)
            {
                //
                // Body and trajectory transform are both in world space.
                // Adjust body transform to be relative to trajectory transform.
                //

                // There can be joints in the hierarchy between the trajectory (first joint) and the body joint. We accumulate the transforms of those in-between joints
                // in order to compute correctly the transform of the body joint relative to the trajectory. i.e.we compute the new body transform `body'` relative to trajectory so that
                // trajectory * bodyOffset * body' = bodyOffset * body
                AffineTransform bodyOffset = AffineTransform.identity;
                int jointIndex = rig.Joints[rig.BodyJointIndex].parentIndex;
                while (jointIndex != 0)
                {
                    bodyOffset = transformBuffer[jointIndex] * bodyOffset;

                    jointIndex = rig.Joints[jointIndex].parentIndex;
                }

                transformBuffer[rig.BodyJointIndex] =
                    (transformBuffer[0] * bodyOffset).inverseTimes(
                        bodyOffset * transformBuffer[rig.BodyJointIndex]);
            }
        }

        AffineTransform SampleTrajectoryInternal(float sampleTimeInSeconds, AnimationRig rig)
        {
            AffineTransform trajectory = transforms[0].Evaluate(sampleTimeInSeconds, rig.Joints[0].localTransform);
            return ApplyImportOptionsOnTrajectory(trajectory);
        }

        internal AnimationSampler(AnimationSamplerFactory factory, AnimationClip animationClip)
        {
            this.factory = factory;

            this.animationClip = animationClip;

            int numJoints = factory.NumJointsTarget;

            transforms = new Transform[numJoints];

            var bindings = AnimationUtility.GetCurveBindings(animationClip);

            foreach (EditorCurveBinding binding in bindings)
            {
                int jointIndex = factory.GetSourceJointIndex(binding.path);

                if (jointIndex >= 0)
                {
                    var curve = AnimationUtility.GetEditorCurve(animationClip, binding);

                    if (jointIndex == 0 && animationClip.hasMotionCurves)
                    {
                        if (binding.propertyName.Contains("Motion"))
                        {
                            transforms[jointIndex].MapEditorCurve(
                                binding.propertyName, "MotionT", "MotionQ", curve);
                        }
                    }
                    else if (jointIndex == 0 && animationClip.hasRootCurves)
                    {
                        if (binding.propertyName.Contains("Root"))
                        {
                            transforms[jointIndex].MapEditorCurve(
                                binding.propertyName, "RootT", "RootQ", curve);
                        }
                    }
                    else
                    {
                        transforms[jointIndex].MapEditorCurve(
                            binding.propertyName, "m_LocalPosition", "m_LocalRotation", curve);
                    }
                }
            }

            transformBuffer = TransformBuffer.Create(numJoints, Allocator.Persistent);
        }

        bool IsRootInTrajectorySpace
        {
            // root transform is already in trajectory space if animation has ONLY motion curves
            get => animationClip.hasMotionCurves && !animationClip.hasRootCurves;
        }

        static ModelImporterClipAnimation GetImporterFromClip(AnimationClip clip)
        {
            string assetPath = AssetDatabase.GetAssetPath(clip);

            var modelImporter = AssetImporter.GetAtPath(assetPath) as ModelImporter;

            if (modelImporter == null)
            {
                return null;
            }

            foreach (ModelImporterClipAnimation clipImporter in modelImporter.clipAnimations)
            {
                if (clipImporter.name == clip.name)
                {
                    return clipImporter;
                }
            }

            return null;
        }

        AffineTransform ApplyImportOptionsOnTrajectory(AffineTransform trajectory)
        {
            if (animationClip.hasRootCurves && !animationClip.hasMotionCurves)
            {
                var clipImporter = GetImporterFromClip(animationClip);

                if (clipImporter != null)
                {
                    var targetRig = factory.TargetRig;

                    AffineTransform trajectoryStart =
                        transforms[0].Evaluate(0.0f,
                            targetRig.Joints[0].localTransform);

                    trajectory = trajectory.alignHorizontally();

                    AffineTransform offset = AffineTransform.identity;

                    offset.t.y = clipImporter.heightOffset;
                    offset.q = quaternion.RotateY(math.radians(clipImporter.rotationOffset));

                    if (clipImporter.lockRootPositionXZ)
                    {
                        trajectory.t.x = trajectoryStart.t.x;
                        trajectory.t.z = trajectoryStart.t.z;
                    }

                    if (clipImporter.lockRootHeightY)
                    {
                        trajectory.t.y = trajectoryStart.t.y;
                    }

                    if (clipImporter.lockRootRotation)
                    {
                        trajectory.q = trajectoryStart.q;
                    }

                    if (clipImporter.keepOriginalPositionY)
                    {
                        offset.t.y -= trajectoryStart.t.y;
                    }

                    if (clipImporter.keepOriginalPositionXZ)
                    {
                        offset.t.x -= trajectoryStart.t.x;
                        offset.t.z -= trajectoryStart.t.z;
                    }

                    if (clipImporter.keepOriginalOrientation)
                    {
                        offset.q = math.mul(offset.q, math.conjugate(trajectoryStart.q));
                    }

                    trajectory.t = offset.t + trajectory.t;
                    trajectory.q = math.mul(offset.q, trajectory.q);
                }
            }

            return trajectory;
        }
    }
}
