using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;

using UnityEngine.Assertions;

using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst;

namespace Unity.Kinematica.Editor
{
    internal partial class Builder
    {
        public struct Marker
        {
            public int segmentIndex;
            public int frameIndex;
            public int traitIndex;
        }

        public class Tag
        {
            public Segment segment;

            public int traitIndex;
            public int tagIndex;

            public Interval interval;

            public bool CanCombineWith(Tag other)
            {
                if (other.interval.Equals(other.interval))
                {
                    if (traitIndex == other.traitIndex)
                    {
                        if (interval.OverlapsOrAdjacent(other.interval))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
        }

        public void BuildTags()
        {
            GenerateMarkers();

            GenerateTags();

            BuildTypes();

            int memoryRequirements = 0;
            int numTagIndices = 0;

            foreach (var trait in traits)
            {
                memoryRequirements += trait.byteArray.Length;
            }

            foreach (var tagList in tagLists)
            {
                numTagIndices += tagList.Length;
            }

            ref Binary binary = ref Binary;

            allocator.Allocate(tags.Count, ref binary.tags);
            allocator.Allocate(markers.Count, ref binary.markers);
            allocator.Allocate(traits.Count, ref binary.traits);
            allocator.Allocate(intervals.Count, ref binary.intervals);
            allocator.Allocate(numTagIndices, ref binary.tagIndices);
            allocator.Allocate(tagLists.Count, ref binary.tagLists);
            allocator.Allocate(memoryRequirements, ref binary.payloads);

            int tagIndex = 0;
            int traitIndex = 0;
            int intervalIndex = 0;
            int tagListsIndex = 0;
            int tagIndicesIndex = 0;
            int markerIndex = 0;
            int writeOffset = 0;

            unsafe
            {
                void* destination = binary.payloads.GetUnsafePtr();

                foreach (var trait in traits)
                {
                    var nameIndex =
                        stringTable.GetStringIndex(
                            NameFromType(trait.type));
                    Assert.IsTrue(nameIndex >= 0);
                    var hashCode =
                        BurstRuntime.GetHashCode32(trait.type);
                    var typeIndex = binary.GetTypeIndex(hashCode);
                    Assert.IsTrue(!typeIndex.Equals(Binary.TypeIndex.Invalid));

                    binary.traits[traitIndex].typeIndex = typeIndex;
                    binary.traits[traitIndex].payload = writeOffset;

                    fixed(void* source = &trait.byteArray[0])
                    {
                        int numBytes = trait.byteArray.Length;
                        UnsafeUtility.MemCpy(
                            (byte*)destination + writeOffset,
                            source, numBytes);
                        writeOffset += numBytes;
                    }

                    traitIndex++;
                }
            }

            foreach (var tag in tags)
            {
                var segmentIndex = tag.segment.segmentIndex;

                Assert.IsTrue(tag.interval.FirstFrame >= tag.segment.destination.FirstFrame);
                Assert.IsTrue(tag.interval.OnePastLastFrame <= tag.segment.destination.OnePastLastFrame);

                int relativeFirstFrame = tag.interval.FirstFrame - tag.segment.destination.FirstFrame;

                binary.tags[tagIndex].segmentIndex = segmentIndex;
                binary.tags[tagIndex].traitIndex = tag.traitIndex;
                binary.tags[tagIndex].firstFrame = relativeFirstFrame;
                binary.tags[tagIndex].numFrames = tag.interval.NumFrames;

                tagIndex++;
            }

            foreach (var interval in intervals)
            {
                VerifyInterval(interval);

                var segmentIndex = interval.segment.segmentIndex;

                Assert.IsTrue(interval.firstFrame >= interval.segment.destination.FirstFrame);
                Assert.IsTrue(interval.onePastLastFrame <= interval.segment.destination.OnePastLastFrame);

                int relativeFirstFrame = interval.firstFrame - interval.segment.destination.FirstFrame;

                binary.intervals[intervalIndex].segmentIndex = segmentIndex;
                binary.intervals[intervalIndex].firstFrame = relativeFirstFrame;
                binary.intervals[intervalIndex].numFrames = interval.numFrames;
                binary.intervals[intervalIndex].tagListIndex = interval.tagListIndex;
                binary.intervals[intervalIndex].codeBookIndex = Binary.CodeBookIndex.Invalid;

                intervalIndex++;
            }

            foreach (var tagList in tagLists)
            {
                binary.tagLists[tagListsIndex].tagIndicesIndex = tagIndicesIndex;
                binary.tagLists[tagListsIndex].numIndices = tagList.Length;

                foreach (var tag in tagList)
                {
                    binary.tagIndices[tagIndicesIndex] = tag.tagIndex;

                    tagIndicesIndex++;
                }

                tagListsIndex++;
            }

            foreach (var marker in markers)
            {
                binary.markers[markerIndex].traitIndex = marker.traitIndex;
                binary.markers[markerIndex].frameIndex = marker.frameIndex;
                binary.markers[markerIndex].segmentIndex = marker.segmentIndex;

                markerIndex++;
            }

            int numSegments = segments.NumSegments;

            for (int i = 0; i < numSegments; ++i)
            {
                Assert.IsTrue(binary.segments[i].tagIndex == 0);
                Assert.IsTrue(binary.segments[i].numTags == 0);

                binary.segments[i].tagIndex = segments[i].tagIndex;
                binary.segments[i].numTags = segments[i].numTags;

                Assert.IsTrue(binary.segments[i].intervalIndex == 0);
                Assert.IsTrue(binary.segments[i].numIntervals == 0);

                binary.segments[i].intervalIndex = segments[i].intervalIndex;
                binary.segments[i].numIntervals = segments[i].numIntervals;

                binary.segments[i].markerIndex = segments[i].markerIndex;
                binary.segments[i].numMarkers = segments[i].numMarkers;

                if (binary.segments[i].numMarkers <= 0)
                {
                    binary.segments[i].markerIndex = Binary.MarkerIndex.Invalid;
                }
            }

            Assert.IsTrue(tagIndex == tags.Count);
            Assert.IsTrue(traitIndex == traits.Count);
            Assert.IsTrue(intervalIndex == intervals.Count);
            Assert.IsTrue(markerIndex == markers.Count);
            Assert.IsTrue(writeOffset == memoryRequirements);
        }

        void GenerateMarkers()
        {
            AnimationSampler sampler = null;

            foreach (var segment in segments.ToArray())
            {
                sampler = GetAnimationSampler(segment, sampler);

                var payloadBuilder = new PayloadBuilder(this, sampler);

                payloadBuilder.interval = segment.destination;

                var taggedClip = segment.clip;

                var animationClip = taggedClip.Clip;

                Assert.IsTrue(segment.markerIndex == 0);
                Assert.IsTrue(segment.numMarkers == 0);

                var markers = new List<Marker>();

                foreach (var marker in segment.clip.Markers)
                {
                    int frameIndex =
                        animationClip.TimeInSecondsToIndex(
                            marker.timeInSeconds);

                    if (segment.source.Contains(frameIndex))
                    {
                        int adjustedFrameIndex =
                            segment.Remap(frameIndex) -
                            segment.destination.FirstFrame;

                        sampler.SamplePose(marker.timeInSeconds);

                        payloadBuilder.sampleTimeInSeconds = marker.timeInSeconds;

                        var markerTrait = BuildTrait(marker.payload, payloadBuilder);

                        markers.Add(new Marker
                        {
                            segmentIndex = segment.segmentIndex,
                            frameIndex = adjustedFrameIndex,
                            traitIndex = RegisterTrait(markerTrait)
                        });
                    }
                }

                segment.markerIndex = this.markers.Count;
                segment.numMarkers = markers.Count;

                this.markers.AddRange(markers);
            }

            if (sampler != null)
            {
                sampler.Dispose();
            }
        }

        void GenerateTags()
        {
            AnimationSampler sampler = null;

            foreach (var segment in segments.ToArray())
            {
                sampler = GetAnimationSampler(segment, sampler);

                GenerateTags(segment, sampler);
            }

            for (int i = 0; i < intervals.Count; ++i)
            {
                Assert.IsTrue(intervals[i].index == 0);
                intervals[i].index = i;
            }

            for (int i = 0; i < tags.Count; ++i)
            {
                Assert.IsTrue(tags[i].tagIndex == 0);
                tags[i].tagIndex = i;
            }

            sampler.Dispose();
        }

        void GenerateTags(Segment segment, AnimationSampler sampler)
        {
            var tags = new List<Tag>();

            var payloadBuilder = new PayloadBuilder(this, sampler);

            foreach (var tag in segment.tags)
            {
                var animationClip = segment.clip;

                float startTime = math.max(0.0f, tag.startTime);
                float endTime = math.min(animationClip.DurationInSeconds, tag.EndTime);
                float duration = endTime - startTime;

                if (duration <= 0.0f)
                {
                    UnityEngine.Debug.LogWarning($"One '{tag.Name}' tag is laying entirely outside of '{animationClip.ClipName}' clip range, it will therefore be ignored. (Tag starting at {tag.startTime:0.00} seconds, whose duration is {tag.duration:0.00} seconds).");
                    continue;
                }

                int firstFrame = animationClip.TimeInSecondsToIndex(startTime);
                int numFrames = animationClip.TimeInSecondsToIndex(duration);

                int onePastLastFrame =
                    math.min(firstFrame + numFrames,
                        animationClip.NumFrames);

                var interval = new Interval(firstFrame, onePastLastFrame);

                Interval intersection = interval.Intersection(segment.source);

                int adjustedFirstFrame = segment.Remap(intersection.FirstFrame);
                int adjustedOnePastLastFrame = segment.Remap(intersection.OnePastLastFrame);
                int adjustedNumFrames = adjustedOnePastLastFrame - adjustedFirstFrame;

                payloadBuilder.interval = new Interval(
                    adjustedFirstFrame, adjustedOnePastLastFrame);

                var tagTrait = BuildTrait(tag.payload, payloadBuilder);

                tags.Add(new Tag
                {
                    segment = segment,
                    interval = payloadBuilder.interval,
                    traitIndex = RegisterTrait(tagTrait)
                });
            }

            Coalesce(tags);

            Assert.IsTrue(segment.intervalIndex == 0);
            Assert.IsTrue(segment.numIntervals == 0);

            segment.intervalIndex = intervals.Count;

            var numIntervals =
                GenerateIntervals(tags, segment);

            segment.numIntervals = numIntervals;

            Assert.IsTrue(segment.tagIndex == 0);
            Assert.IsTrue(segment.numTags == 0);

            segment.tagIndex = this.tags.Count;
            segment.numTags = tags.Count;

            this.tags.AddRange(tags);
        }

        struct Boundary
        {
            public Tag tag;
            public int frameIndex;

            public static Boundary Create(Tag tag, int frameIndex)
            {
                return new Boundary
                {
                    tag = tag,
                    frameIndex = frameIndex
                };
            }
        }

        public class TaggedInterval
        {
            public int index;
            public Segment segment;
            public int tagListIndex;
            public Interval interval;

            public int segmentIndex => segment.segmentIndex;

            public int firstFrame => interval.FirstFrame;
            public int numFrames => interval.NumFrames;
            public int onePastLastFrame => firstFrame + numFrames;

            public static TaggedInterval Create(Segment segment, Interval interval, int tagListIndex)
            {
                return new TaggedInterval
                {
                    segment = segment,
                    interval = interval,
                    tagListIndex = tagListIndex
                };
            }
        }

        (Metric, int) GetMetric(TaggedInterval taggedInterval)
        {
            Tag[] tags = tagLists[taggedInterval.tagListIndex];

            foreach (var tag in tags)
            {
                var traitType = traits[tag.traitIndex].type;

                var metric = FindMetricForTraitType(traitType);

                if (metric != null)
                {
                    return (metric, tag.traitIndex);
                }
            }

            return (null, -1);
        }

        int GenerateIntervals(List<Tag> tags, Segment segment)
        {
            int numIntervals = 0;

            Assert.IsTrue(tags.Count > 0);

            var boundaries = new List<Boundary>();

            foreach (var tag in tags)
            {
                boundaries.Add(
                    Boundary.Create(
                        tag, tag.interval.FirstFrame));

                boundaries.Add(
                    Boundary.Create(
                        tag, tag.interval.OnePastLastFrame));
            }

            boundaries = boundaries.OrderBy(x => x.frameIndex).ToList();

            var openList = new List<Tag>();

            openList.Add(boundaries[0].tag);

            for (int i = 1; i < boundaries.Count; ++i)
            {
                var boundary = boundaries[i];

                Assert.IsTrue(boundary.frameIndex >= boundaries[i - 1].frameIndex);

                if (boundary.frameIndex > boundaries[i - 1].frameIndex)
                {
                    var interval =
                        new Interval(boundaries[i - 1].frameIndex,
                            boundary.frameIndex);

                    var tagListIndex =
                        PushTagList(openList.ToArray());

                    intervals.Add(
                        TaggedInterval.Create(
                            segment, interval, tagListIndex));

                    numIntervals++;
                }

                if (openList.FindAll(x => x == boundary.tag).Count > 0)
                {
                    openList.Remove(boundary.tag);
                }
                else
                {
                    openList.Add(boundary.tag);
                }
            }

            Assert.IsTrue(openList.Count == 0);

            return numIntervals;
        }

        int PushTagList(Tag[] tags)
        {
            for (int i = 0; i < tagLists.Count; ++i)
            {
                if (Enumerable.SequenceEqual(tagLists[i], tags))
                {
                    return i;
                }
            }

            tagLists.Add(tags);

            return tagLists.Count - 1;
        }

        static void Coalesce(List<Tag> tags)
        {
            for (int i = 0; i < tags.Count - 1; ++i)
            {
                for (int j = i + 1; j < tags.Count; ++j)
                {
                    if (tags[i].CanCombineWith(tags[j]))
                    {
                        var interval = Interval.Union(
                            tags[i].interval, tags[j].interval);

                        tags[i].interval = interval;

                        i--;
                        tags.RemoveAt(j);
                        break;
                    }
                }
            }
        }

        void VerifyInterval(TaggedInterval taggedInterval)
        {
            foreach (var tag in tagLists[taggedInterval.tagListIndex])
            {
                var intersection =
                    tag.interval.Intersection(
                        taggedInterval.interval);

                Assert.IsTrue(intersection.Equals(taggedInterval.interval));
            }

            foreach (var tag in tagLists[taggedInterval.tagListIndex])
            {
                Assert.IsTrue(tag.segment == taggedInterval.segment);
            }
        }

        List<Tag> tags = new List<Tag>();
        List<Marker> markers = new List<Marker>();
        List<TaggedInterval> intervals = new List<TaggedInterval>();
        List<Tag[]> tagLists = new List<Tag[]>();
    }
}
