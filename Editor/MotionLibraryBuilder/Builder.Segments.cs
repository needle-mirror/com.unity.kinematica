using System.Linq;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Assertions;

using Unity.Mathematics;
using System;

namespace Unity.Kinematica.Editor
{
    internal partial class Builder
    {
        public class Segment
        {
            public int segmentIndex;

            public TaggedAnimationClip clip;

            public Interval source;
            public Interval destination;

            public int tagIndex;
            public int numTags;

            public int markerIndex;
            public int numMarkers;

            public int intervalIndex;
            public int numIntervals;

            public List<TagAnnotation> tags = new List<TagAnnotation>();

            public int Remap(int frameIndex)
            {
                Assert.IsTrue(frameIndex >= source.FirstFrame);
                Assert.IsTrue(frameIndex <= source.OnePastLastFrame);
                int sourceOffset = frameIndex - source.FirstFrame;
                float ratio = sourceOffset / (float)source.NumFrames;
                int destinationOffset = Missing.roundToInt(ratio * destination.NumFrames);
                Assert.IsTrue(destinationOffset <= destination.NumFrames);
                return destination.FirstFrame + destinationOffset;
            }

            public Avatar GetSourceAvatar(Asset asset)
            {
                return clip.GetSourceAvatar(asset.DestinationAvatar);
            }
        }

        public class Segments
        {
            List<Segment> segments = new List<Segment>();

            public static Segments Create()
            {
                return new Segments();
            }

            public int NumFrames
            {
                get; private set;
            }

            public int NumSegments => segments.Count;

            public Segment this[int index] => segments[index];

            public Segment[] ToArray() => segments.ToArray();

            public void GenerateSegments(Asset asset)
            {
                int destinationIndex = 0;
                int numFrames = 0;

                foreach (var animationClip in asset.GetAnimationClips())
                {
                    var segments = GenerateSegments(asset, animationClip);

                    foreach (var segment in segments)
                    {
                        Assert.IsTrue(segment.source.FirstFrame >= 0);
                        Assert.IsTrue(segment.source.OnePastLastFrame <= animationClip.NumFrames);

                        int numFramesSource = segment.source.NumFrames;
                        int numFramesDestination = animationClip.ClipFramesToAssetFrames(asset, numFramesSource);

                        int onePastLastFrame = destinationIndex + numFramesDestination;

                        numFrames += numFramesDestination;

                        segment.destination =
                            new Interval(destinationIndex, onePastLastFrame);

                        destinationIndex += numFramesDestination;
                    }

                    this.segments.AddRange(segments);
                }

                NumFrames = numFrames;
            }

            static List<Segment> GenerateSegments(Asset asset, TaggedAnimationClip animationClip)
            {
                var segments = new List<Segment>();

                var tags = animationClip.Tags.ToList();

                tags.Sort((x, y) => x.startTime.CompareTo(y.startTime));

                foreach (TagAnnotation tag in tags)
                {
                    float startTime = math.max(0.0f, tag.startTime);
                    float endTime = math.min(animationClip.DurationInSeconds, tag.EndTime);
                    float duration = endTime - startTime;

                    if (duration <= 0.0f)
                    {
                        Debug.LogWarning($"One '{tag.Name}' tag is laying entirely outside of '{animationClip.ClipName}' clip range, it will therefore be ignored. (Tag starting at {tag.startTime:0.00} seconds, whose duration is {tag.duration:0.00} seconds).");
                        continue;
                    }

                    int firstFrame = animationClip.ClampedTimeInSecondsToIndex(startTime);
                    int numFrames = animationClip.ClampedDurationInSecondsToFrames(duration);

                    int onePastLastFrame =
                        math.min(firstFrame + numFrames,
                            animationClip.NumFrames);

                    var interval = new Interval(firstFrame, onePastLastFrame);

                    var segment = new Segment
                    {
                        clip = animationClip,
                        source = interval,
                        destination = Interval.Empty
                    };

                    segment.tags.Add(tag);

                    segments.Add(segment);
                }

                Coalesce(segments);

                return segments;
            }

            static void Coalesce(List<Segment> segments)
            {
                int numSegmentsMinusOne = segments.Count - 1;

                for (int i = 0; i < numSegmentsMinusOne; ++i)
                {
                    if (segments[i].source.OverlapsOrAdjacent(segments[i + 1].source))
                    {
                        segments[i].tags.AddRange(segments[i + 1].tags);
                        segments[i].source.Union(segments[i + 1].source);

                        segments.RemoveAt(i + 1);

                        numSegmentsMinusOne--;
                        i--;
                    }
                }
            }
        }

        bool DoesSegmentCoverMetric(Segment segment)
        {
            foreach (TagAnnotation tag in segment.tags)
            {
                foreach (Asset.Metric metric in asset.Metrics)
                {
                    string tagName = TagAnnotation.GetTagTypeFullName(tag);
                    if (metric.TagTypes.Contains(tagName))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        void CheckTagsAreLongEnough()
        {
            ref Binary binary = ref Binary;

            int numBoundaryFrames = Missing.truncToInt(asset.TimeHorizon * asset.SampleRate);

            bool hasShortTag = false;

            for (int i = 0; i < binary.numSegments; ++i)
            {
                Segment segment = segments[i];

                if (!DoesSegmentCoverMetric(segment))
                {
                    continue;
                }

                int segmentFirstFrame = segment.clip.ClipFramesToAssetFrames(asset, segment.source.FirstFrame);
                int segmentOnePastLastFrame = segment.clip.ClipFramesToAssetFrames(asset, segment.source.OnePastLastFrame);

                int prevSegmentFrames = 0;
                Binary.SegmentIndex prevSegmentIndex = binary.segments[i].previousSegment;
                if (prevSegmentIndex.IsValid)
                {
                    Segment prevSegment = segments[prevSegmentIndex];
                    if (DoesSegmentCoverMetric(prevSegment))
                    {
                        prevSegmentFrames = segment.clip.ClipFramesToAssetFrames(asset, prevSegment.source.NumFrames);
                    }
                }

                int nextSegmentFrames = 0;
                Binary.SegmentIndex nextSegmentIndex = binary.segments[i].nextSegment;
                if (nextSegmentIndex.IsValid)
                {
                    Segment nextSegment = segments[nextSegmentIndex];
                    if (DoesSegmentCoverMetric(nextSegment))
                    {
                        nextSegmentFrames = segment.clip.ClipFramesToAssetFrames(asset, nextSegment.source.NumFrames);
                    }
                }

                segmentFirstFrame += math.max(numBoundaryFrames - prevSegmentFrames, 0);
                segmentOnePastLastFrame -= math.max(numBoundaryFrames - nextSegmentFrames, 0);

                for (int j = 0; j < segment.tags.Count; ++j)
                {
                    TagAnnotation tag = segment.tags[j];

                    int firstFrame = segment.clip.ClipFramesToAssetFrames(asset, segment.clip.ClampedTimeInSecondsToIndex(tag.startTime));
                    int onePastLastFrame = firstFrame + segment.clip.ClipFramesToAssetFrames(asset, segment.clip.ClampedDurationInSecondsToFrames(tag.duration));

                    int firstValidFrame = math.max(firstFrame, segmentFirstFrame);
                    int onePastLastValidFrame = math.min(onePastLastFrame, segmentOnePastLastFrame);

                    if (firstValidFrame >= onePastLastValidFrame)
                    {
                        int tagNumFrames = onePastLastFrame - firstFrame;
                        int missingFrames = firstValidFrame + 1 - onePastLastValidFrame;

                        Debug.LogError($"Tag [{firstFrame},{onePastLastFrame - 1}] from clip {segment.clip.ClipName} is too short. It should be at least {tagNumFrames + missingFrames} frames but is only {tagNumFrames}");

                        hasShortTag = true;
                    }
                }
            }

            if (hasShortTag)
            {
                throw new Exception($"One or more tags are too short.");
            }
        }

        int FindFirstSegment(TaggedAnimationClip taggedAnimationClip)
        {
            int segmentIndex = Binary.SegmentIndex.Invalid;

            int frameIndex = int.MaxValue;

            int numSegments = segments.NumSegments;

            for (int i = 0; i < numSegments; ++i)
            {
                if (segments[i].clip == taggedAnimationClip)
                {
                    if (segments[i].destination.FirstFrame < frameIndex)
                    {
                        frameIndex = segments[i].destination.FirstFrame;
                        segmentIndex = i;
                    }
                }
            }

            return segmentIndex;
        }

        int FindLastSegment(TaggedAnimationClip taggedAnimationClip)
        {
            int segmentIndex = Binary.SegmentIndex.Invalid;

            int frameIndex = -1;

            int numSegments = segments.NumSegments;

            for (int i = 0; i < numSegments; ++i)
            {
                if (segments[i].clip == taggedAnimationClip)
                {
                    if (segments[i].destination.FirstFrame > frameIndex)
                    {
                        frameIndex = segments[i].destination.FirstFrame;
                        segmentIndex = i;
                    }
                }
            }

            return segmentIndex;
        }

        public void BuildSegments()
        {
            segments.GenerateSegments(asset);

            int numSegments = segments.NumSegments;

            ref Binary binary = ref Binary;

            allocator.Allocate(numSegments, ref binary.segments);

            for (int i = 0; i < numSegments; ++i)
            {
                var animationClip = segments[i].clip.AnimationClip;

                var nameIndex = stringTable.RegisterString(animationClip.name);

                var guid =
                    SerializableGuidUtility.GetSerializableGuidFromAsset(
                        animationClip);

                binary.segments[i].nameIndex = nameIndex;
                binary.segments[i].guid = guid;
                binary.segments[i].source.firstFrame = segments[i].source.FirstFrame;
                binary.segments[i].source.numFrames = segments[i].source.NumFrames;
                binary.segments[i].destination.firstFrame = segments[i].destination.FirstFrame;
                binary.segments[i].destination.numFrames = segments[i].destination.NumFrames;

                binary.segments[i].previousSegment = Binary.SegmentIndex.Invalid;
                binary.segments[i].nextSegment = Binary.SegmentIndex.Invalid;

                segments[i].segmentIndex = i;
            }

            //
            // The builder UI only supports connections
            // based on animation clips, so unfortunately this is the best I can do.
            //

            for (int i = 0; i < numSegments; ++i)
            {
                var taggedAnimationClip = segments[i].clip;

                var preBoundaryClip = taggedAnimationClip.TaggedPreBoundaryClip;
                var postBoundaryClip = taggedAnimationClip.TaggedPostBoundaryClip;

                binary.segments[i].previousSegment = FindLastSegment(preBoundaryClip);
                binary.segments[i].nextSegment = FindFirstSegment(postBoundaryClip);
            }

            CheckTagsAreLongEnough();
        }
    }
}
