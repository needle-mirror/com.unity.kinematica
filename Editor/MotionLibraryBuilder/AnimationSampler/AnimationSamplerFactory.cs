using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Assertions;

namespace Unity.Kinematica.Editor
{
    internal class AnimationSamplerFactory : IDisposable
    {
        AnimationRig targetRig;

        Dictionary<string, int> jointIndicesMap;

        AnimationRetargeter retargeter;

        public AnimationRig TargetRig
        {
            get => targetRig;
        }

        public int NumJointsTarget
        {
            get
            {
                return targetRig.NumJoints;
            }
        }

        public bool NeedsRetargeting
        {
            get => retargeter != null;
        }

        public AnimationRetargeter Retargeter
        {
            get => retargeter;
        }

        public int GetSourceJointIndex(string jointName)
        {
            int jointIndex = -1;
            if (string.IsNullOrEmpty(jointName))
            {
                jointIndex = 0;
            }
            else
            {
                int mapJointIndex = 0;
                if (jointIndicesMap.TryGetValue(jointName, out mapJointIndex))
                {
                    jointIndex = mapJointIndex;
                }
            }
            return jointIndex;
        }

        public static AnimationSamplerFactory Create(Avatar sourceAvatar, AnimationRig targetRig)
        {
            return new AnimationSamplerFactory(sourceAvatar, targetRig);
        }

        public void Dispose()
        {
            if (retargeter != null)
            {
                retargeter.Dispose();
            }
        }

        public AnimationSampler Create(AnimationClip animationClip)
        {
            return new AnimationSampler(this, animationClip);
        }

        AnimationSamplerFactory(Avatar sourceAvatar, AnimationRig targetRig)
        {
            int numJoints = targetRig.NumJoints;

            this.targetRig = targetRig;

            var targetAvatar = targetRig.Avatar;

            if (sourceAvatar == null)
            {
                sourceAvatar = targetAvatar;
            }

            string[] jointPaths;
            if (sourceAvatar != targetAvatar)
            {
                if (!sourceAvatar.isHuman || !targetAvatar.isHuman)
                {
                    throw new Exception($"{sourceAvatar.name} and {targetAvatar.name} must be humanoid to enable retargeting");
                }

                var sourceRig = AnimationRig.Create(sourceAvatar);
                retargeter = new HumanoidAnimationRetargeter(sourceRig, targetRig);
                jointPaths = retargeter.SourceAvatarRig.JointPaths;
            }
            else
            {
                jointPaths = targetRig.JointPaths;
            }

            jointIndicesMap = new Dictionary<string, int>();
            for (int i = 0; i < jointPaths.Length; ++i)
            {
                jointIndicesMap.Add(jointPaths[i], i);
            }
        }
    }
}
