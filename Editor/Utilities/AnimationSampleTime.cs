using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Unity.Kinematica.Editor
{
    internal struct AnimationSampleTime
    {
        public static AnimationSampleTime CreateInvalid()
        {
            return new AnimationSampleTime()
            {
                clip = null,
                sampleTimeInSeconds = 0.0f
            };
        }

        public static AnimationSampleTime CreateFromTimeIndex(ref Binary binary, TimeIndex timeIndex, IEnumerable<AnimationClip> clips)
        {
            AnimationSampleTimeIndex animSampleTime = binary.GetAnimationSampleTimeIndex(timeIndex);
            if (animSampleTime.IsValid)
            {
                foreach (AnimationClip clip in clips)
                {
                    if (clip == null)
                    {
                        continue;
                    }

                    SerializableGuid clipGuid = SerializableGuidUtility.GetSerializableGuidFromAsset(clip);

                    if (clipGuid == animSampleTime.clipGuid)
                    {
                        var inverseSampleRate =
                            math.rcp(clip.frameRate);

                        var sampleTimeInSeconds =
                            animSampleTime.animFrameIndex * inverseSampleRate;

                        return new AnimationSampleTime
                        {
                            clip = clip,
                            sampleTimeInSeconds = sampleTimeInSeconds
                        };
                    }
                }
            }

            return CreateInvalid();
        }

        public TimeIndex GetTimeIndex(ref Binary binary)
        {
            int animFrameIndex = Missing.truncToInt(sampleTimeInSeconds * clip.frameRate);
            var clipGuid = SerializableGuidUtility.GetSerializableGuidFromAsset(clip);

            return binary.GetTimeIndexFromAnimSampleTime(new AnimationSampleTimeIndex()
            {
                clipGuid = clipGuid,
                clipName = clip.name,
                animFrameIndex = animFrameIndex
            });
        }

        public bool IsValid => clip != null;

        public AnimationClip    clip;
        public float            sampleTimeInSeconds;
    }
}
