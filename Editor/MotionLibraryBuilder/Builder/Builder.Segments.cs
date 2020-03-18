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

                    int firstFrame = animationClip.TimeInSecondsToIndex(startTime);
                    int numFrames = animationClip.TimeInSecondsToIndex(duration);

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
                    if (metric.TagTypes.Contains(tag.Name))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        void CheckSegmentsAreLongEnough()
        {
            ref Binary binary = ref Binary;

            int numBoundaryFrames = Missing.truncToInt(asset.TimeHorizon * asset.SampleRate);

            var invalidSegments = new List<Tuple<int, int>>();

            for (int i = 0; i < binary.numSegments; ++i)
            {
                if (!DoesSegmentCoverMetric(segments[i]))
                {
                    continue;
                }

                int leftBorder = binary.segments[i].previousSegment.IsValid ? 0 : 1;
                int rightBorder = binary.segments[i].nextSegment.IsValid ? 0 : 1;

                int minSegmentAssetFrames = (leftBorder + rightBorder) * numBoundaryFrames + 1;

                int minSegmentClipFrames = segments[i].clip.AssetFramesToClipFrames(asset, minSegmentAssetFrames);

                if (binary.segments[i].source.NumFrames < minSegmentClipFrames)
                {
                    invalidSegments.Add(new Tuple<int, int>(i, minSegmentClipFrames));
                }
            }

            if (invalidSegments.Count > 0)
            {
                foreach (var invalidSegment in invalidSegments)
                {
                    ref var segment = ref binary.segments[invalidSegment.Item1];
                    TaggedAnimationClip clip = segments[invalidSegment.Item1].clip;
                    int minSegmentFrames = invalidSegment.Item2;


                    Debug.LogError($"Segment [{segment.source.FirstFrame},{segment.source.OnePastLastFrame - 1}] from clip {clip.ClipName} is too short. It should be at least {minSegmentFrames} frames but is only {segment.source.NumFrames}");
                }

                throw new Exception($"One or more segments are too short.");
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

            CheckSegmentsAreLongEnough();
        }
    }
}
