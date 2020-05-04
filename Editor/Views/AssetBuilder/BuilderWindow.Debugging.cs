using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.SnapshotDebugger;

namespace Unity.Kinematica.Editor
{
    partial class BuilderWindow
    {
        void PlayModeUpdate()
        {
            //
            // It would probably be a more symmetric architecture
            // to push the current time from the synthesizer to the
            // builder window.
            //
            // The two other use cases (highlight current task time
            // and retrieving the override time index) use a pull
            // model.
            //

            if (Application.isPlaying && m_PreviewTarget != null)
            {
                var kinematica = m_PreviewTarget.GetComponent<Kinematica>();

                if (kinematica != null)
                {
                    ref var synthesizer = ref kinematica.Synthesizer.Ref;

                    var samplingTime = synthesizer.Time;

                    HighlightTimeIndex(ref synthesizer, samplingTime.timeIndex);
                }
                else
                {
                    HighlightAnimationClip(null);
                    HighlightCurrentSamplingTime(null, 0.0f);
                }
            }
        }

        public TimeIndex RetrieveDebugTimeIndex(ref Binary binary)
        {
            if (Debugger.instance.rewind)
            {
                TaggedAnimationClip taggedClip = m_Timeline.TaggedClip;

                if (taggedClip != null)
                {
                    AnimationClip animationClip = taggedClip.AnimationClip;

                    float sampleTimeInSeconds = m_Timeline.DebugTime;

                    if (sampleTimeInSeconds >= 0.0f)
                    {
                        AnimationSampleTime animSampleTime = new AnimationSampleTime()
                        {
                            clip = animationClip,
                            sampleTimeInSeconds = sampleTimeInSeconds
                        };

                        return animSampleTime.GetTimeIndex(ref binary);
                    }
                }
            }

            return TimeIndex.Invalid;
        }

        public void HighlightTimeIndex(ref MotionSynthesizer synthesizer, TimeIndex timeIndex, bool debug = false)
        {
            AnimationSampleTime animSampleTime = AnimationSampleTime.CreateFromTimeIndex(
                ref synthesizer.Binary,
                timeIndex,
                m_AnimationLibraryListView.Children().Select(x => (x.userData as TaggedAnimationClip)?.AnimationClip));

            if (animSampleTime.IsValid)
            {
                HighlightAnimationClip(animSampleTime.clip);
                HighlightCurrentSamplingTime(animSampleTime.clip, animSampleTime.sampleTimeInSeconds, debug);
            }
        }

        void HighlightCurrentSamplingTime(AnimationClip animationClip, float sampleTimeInSeconds, bool debug = false)
        {
            TaggedAnimationClip taggedClip = m_Timeline.TaggedClip;

            if (animationClip != null && taggedClip != null && taggedClip.AnimationClip == animationClip)
            {
                if (debug)
                {
                    m_Timeline.SetActiveTickVisible(false);
                    m_Timeline.SetDebugTime(sampleTimeInSeconds);
                }
                else
                {
                    m_Timeline.SetActiveTime(sampleTimeInSeconds);
                    m_Timeline.SetActiveTickVisible(true);
                }
            }
            else
            {
                m_Timeline.SetActiveTickVisible(false);
            }
        }

        void HighlightAnimationClip(AnimationClip animationClip)
        {
            foreach (VisualElement clipElement in m_AnimationLibraryListView.Children())
            {
                if (!(clipElement.userData is TaggedAnimationClip taggedClip))
                {
                    continue;
                }

                IStyle clipStyle = clipElement.ElementAt(k_ClipHighlight).style;

                if (taggedClip.AnimationClip == animationClip)
                {
                    clipStyle.visibility = Visibility.Visible;
                    clipStyle.opacity = new StyleFloat(1f);
                }
                else
                {
                    clipStyle.visibility = Visibility.Hidden;
                }
            }
        }
    }
}
