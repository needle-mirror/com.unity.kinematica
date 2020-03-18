using Unity.Burst;
using Unity.Collections;
using Unity.SnapshotDebugger;
using UnityEngine;
using UnityEngine.Animations;

namespace Unity.Kinematica
{
    public partial class Kinematica : SnapshotProvider
    {
        [BurstCompile]
        struct Job : IAnimationJob, System.IDisposable
        {
            NativeArray<TransformStreamHandle> transforms;
            NativeArray<bool> boundJoints;

            MemoryRef<MotionSynthesizer> synthesizer;

            PropertySceneHandle deltaTime;

            bool transformBufferValid;

            public bool Setup(Animator animator, Transform[] transforms, ref MotionSynthesizer synthesizer)
            {
                this.synthesizer = MemoryRef<MotionSynthesizer>.Create(ref synthesizer);

                int numJoints = synthesizer.Binary.numJoints;
                this.transforms = new NativeArray<TransformStreamHandle>(numJoints, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

                boundJoints = new NativeArray<bool>(numJoints, Allocator.Persistent, NativeArrayOptions.ClearMemory);

                // Root joint is always first transform, and names don't need to match contrary to other joints
                this.transforms[0] = animator.BindStreamTransform(transforms[0]);
                boundJoints[0] = true;

                for (int i = 1; i < transforms.Length; i++)
                {
                    int jointNameIndex = synthesizer.Binary.GetStringIndex(transforms[i].name);
                    int jointIndex = (jointNameIndex >= 0) ? synthesizer.Binary.animationRig.GetJointIndexForNameIndex(jointNameIndex) : -1;
                    if (jointIndex >= 0)
                    {
                        this.transforms[jointIndex] = animator.BindStreamTransform(transforms[i]);
                        boundJoints[jointIndex] = true;
                    }
                }

                string missingJointsNames = "";
                for (int i = 0; i < numJoints; ++i)
                {
                    if (!boundJoints[i])
                    {
                        missingJointsNames = missingJointsNames + "\"" + synthesizer.Binary.GetString(synthesizer.Binary.animationRig.bindPose[i].nameIndex) + "\" ";
                    }
                }

                if (missingJointsNames.Length > 0)
                {
                    // if some joints from the rig were not found in the character, we just send warning but still continue simulation with the existing joints
                    Debug.LogWarning($"Joints {missingJointsNames} not bound on character {transforms[0].name}");
                }

                deltaTime = animator.BindSceneProperty(animator.gameObject.transform, typeof(Kinematica), "_deltaTime");

                return true;
            }

            public void Dispose()
            {
                transforms.Dispose();
                boundJoints.Dispose();
            }

            public void ProcessRootMotion(AnimationStream stream)
            {
                ref MotionSynthesizer synthesizer = ref this.synthesizer.Ref;

                transformBufferValid = synthesizer.Update(deltaTime.GetFloat(stream));
            }

            public void ProcessAnimation(AnimationStream stream)
            {
                ref MotionSynthesizer synthesizer = ref this.synthesizer.Ref;

                if (transformBufferValid)
                {
                    int numTransforms = synthesizer.LocalSpaceTransformBuffer.Length;
                    for (int i = 1; i < numTransforms; ++i)
                    {
                        if (!boundJoints[i])
                        {
                            continue;
                        }

                        transforms[i].SetLocalPosition(stream, synthesizer.LocalSpaceTransformBuffer[i].t);
                        transforms[i].SetLocalRotation(stream, synthesizer.LocalSpaceTransformBuffer[i].q);
                    }
                }
            }
        }
    }
}
