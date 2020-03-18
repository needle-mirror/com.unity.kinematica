using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;

namespace Unity.Kinematica.Editor
{
    class MetricsTrack : Track
    {
        Asset m_TargetAsset;

        Asset TargetAsset
        {
            get { return m_TargetAsset;}
            set
            {
                if (m_TargetAsset != value)
                {
                    if (m_TargetAsset != null)
                    {
                        m_TargetAsset.MetricsModified -= ReloadElements;
                    }

                    m_TargetAsset = value;
                    if (m_TargetAsset != null)
                    {
                        m_TargetAsset.MetricsModified += ReloadElements;
                    }
                }
            }
        }

        public MetricsTrack(Timeline owner) : base(owner)
        {
            AddToClassList("metricsTrack");
        }

        class MetricRange : IInterval
        {
            public string name;
            public float intervalStart { get; set; }
            public float intervalEnd { get; set; }
            public int priority;
        }

        struct Segment
        {
            public float startTime;
            public float endTime;

            public bool DoesCoverTime(float time)
            {
                return time >= startTime && time <= endTime;
            }

            public bool DoesCoverInterval(IInterval interval)
            {
                return DoesCoverTime(interval.intervalStart) && DoesCoverTime(interval.intervalEnd);
            }
        }

        public override void ReloadElements()
        {
            Profiler.BeginSample("MetricsTrack.ReloadElements");
            Clear();

            TargetAsset = m_Owner.TargetAsset;
            if (TargetAsset == null)
            {
                Profiler.EndSample();
                return;
            }

            TaggedAnimationClip clip = m_Owner.TaggedClip;
            if (clip == null)
            {
                Profiler.EndSample();
                return;
            }

            IntervalTree<MetricRange> intervalTree = BuildIntervalTree(clip);
            List<MetricRange> results = new List<MetricRange>();
            intervalTree.IntersectsWithRange(0f, clip.DurationInSeconds, results);
            ProcessIntervalTreeResults(results);

            List<Segment> segments = ComputeSegments(clip);

            int numBoundaryFrames = Missing.truncToInt(TargetAsset.TimeHorizon * TargetAsset.SampleRate);
            float boundaryDuration = numBoundaryFrames / TargetAsset.SampleRate;


            foreach (var metricRange in results)
            {
                Segment segment = segments[0];
                foreach (Segment s in segments)
                {
                    if (s.DoesCoverInterval(metricRange))
                    {
                        segment = s;
                    }
                }

                int segmentStartIndex = clip.ClipFramesToAssetFrames(TargetAsset, clip.ClampedTimeInSecondsToIndex(segment.startTime));
                if (!DoesClipContainsValidSegment(TargetAsset, clip.PreBoundaryClip))
                {
                    segment.startTime += boundaryDuration;
                    segmentStartIndex += numBoundaryFrames;
                }

                int segmentEndIndex = clip.ClipFramesToAssetFrames(TargetAsset, clip.ClampedTimeInSecondsToIndex(segment.endTime));
                if (!DoesClipContainsValidSegment(TargetAsset, clip.PostBoundaryClip))
                {
                    segment.endTime -= boundaryDuration;
                    segmentEndIndex -= numBoundaryFrames;
                }

                bool emptyInterval = segmentStartIndex >= segmentEndIndex;

                var metricElement = new MetricElement(
                    metricRange.name,
                    Mathf.Clamp(metricRange.intervalStart, 0.0f, clip.DurationInSeconds),
                    Mathf.Clamp(metricRange.intervalEnd, 0.0f, clip.DurationInSeconds),
                    segment.startTime,
                    segment.endTime,
                    emptyInterval,
                    m_Owner);

                Add(metricElement);
            }

            ResizeContents();

            Profiler.EndSample();
        }

        /// <summary>
        /// Takes a result list from an IntervalTree and merges adjacent intervals with the same name and adjust for priority levels
        /// </summary>
        /// <param name="source"></param>
        void ProcessIntervalTreeResults(List<MetricRange> source)
        {
            /*
             * A second level of processing is done to merge intervals that share the same name and return intervals with higher priority
             */
            source.Sort((left, right) => Comparer<int>.Default.Compare(left.priority, right.priority));
            // start from 2nd to last result hiding and splitting lower priority results and proceeding backwards in the list
            for (int i = source.Count - 2; i >= 0; --i)
            {
                MetricRange higherPriority = source[i];

                for (int j = i + 1; j < source.Count;)
                {
                    MetricRange lowerPriority = source[j];
                    if (lowerPriority.intervalStart >= higherPriority.intervalStart &&
                        lowerPriority.intervalEnd <= higherPriority.intervalEnd)
                    {
                        source.RemoveAt(j);
                        continue;
                    }

                    bool insertNew = false;

                    if (lowerPriority.intervalStart <= higherPriority.intervalStart)
                    {
                        if (lowerPriority.intervalEnd <= higherPriority.intervalStart)
                        {
                            ++j;
                            continue;
                        }

                        // merge with higher priority
                        if (lowerPriority.name.Equals(higherPriority.name))
                        {
                            higherPriority = new MetricRange
                            {
                                intervalStart = lowerPriority.intervalStart,
                                intervalEnd = Mathf.Max(lowerPriority.intervalEnd, higherPriority.intervalEnd),
                                priority = higherPriority.priority,
                                name = lowerPriority.name
                            };

                            source[i] = higherPriority;
                            source.RemoveAt(j);
                            continue;
                        }

                        if (lowerPriority.intervalEnd > higherPriority.intervalEnd)
                        {
                            // we need to split the lowerPriority interval
                            insertNew = true;
                        }

                        // shrink
                        source[j] = new MetricRange
                        { intervalStart = lowerPriority.intervalStart, intervalEnd = higherPriority.intervalStart, priority = lowerPriority.priority, name = lowerPriority.name };
                    }

                    if (lowerPriority.intervalEnd >= higherPriority.intervalEnd)
                    {
                        if (lowerPriority.intervalStart < higherPriority.intervalEnd)
                        {
                            // merge with higher priority
                            if (lowerPriority.name.Equals(higherPriority.name))
                            {
                                higherPriority = new MetricRange
                                {
                                    intervalStart = higherPriority.intervalStart,
                                    intervalEnd = lowerPriority.intervalEnd,
                                    priority = higherPriority.priority,
                                    name = lowerPriority.name
                                };

                                source[i] = higherPriority;
                                source.RemoveAt(j);
                                continue;
                            }

                            var newResult = new MetricRange
                            {
                                intervalStart = higherPriority.intervalEnd, intervalEnd = lowerPriority.intervalEnd, priority = lowerPriority.priority, name = lowerPriority.name
                            };

                            // if this lower priority interval surrounded the higher priority one we will need to add a second lower priority interval, otherwise we can just replace the original
                            if (insertNew)
                            {
                                source.Insert(j + 1, newResult);
                            }
                            else
                            {
                                source[j] = newResult;
                            }
                        }
                    }

                    ++j;
                }
            }
        }

        IntervalTree<MetricRange> BuildIntervalTree(TaggedAnimationClip clip)
        {
            List<TagAnnotation> tags = clip.Tags;
            Profiler.BeginSample("MetricsTrack.BuildIntervalTree");
            IntervalTree<MetricRange> intervalTree = new IntervalTree<MetricRange>();

            for (var i = 0; i < tags.Count; i++)
            {
                var tag = tags[i];
                int metricIndex = clip.GetMetricIndexCoveringTimeOnTag(tag.Name);

                if (metricIndex < 0 || metricIndex >= TargetAsset.Metrics.Count)
                {
                    continue;
                }

                string metricName = TargetAsset.GetMetric(metricIndex).name;
                intervalTree.Add(new MetricRange { intervalStart = tag.startTime, intervalEnd = tag.EndTime, name = metricName, priority = i });
            }

            Profiler.EndSample();
            return intervalTree;
        }

        public override void ResizeContents()
        {
            foreach (MetricElement me in Children().OfType<MetricElement>())
            {
                me.Resize(m_Owner.WidthMultiplier);
            }
        }

        bool DoesClipContainsValidSegment(Asset asset, AnimationClip animClip)
        {
            if (animClip == null)
            {
                return false;
            }

            foreach (TaggedAnimationClip clip in asset.AnimationLibrary)
            {
                if (clip.AnimationClip == animClip)
                {
                    foreach (TagAnnotation tag in clip.Tags)
                    {
                        if (clip.GetMetricIndexCoveringTimeOnTag(tag.Name) >= 0)
                        {
                            return true;
                        }
                    }

                    return false;
                }
            }

            return false;
        }

        List<Segment>   ComputeSegments(TaggedAnimationClip clip)
        {
            List<Segment> segments = new List<Segment>();

            foreach (TagAnnotation tag in clip.Tags)
            {
                segments.Add(new Segment()
                {
                    startTime = Mathf.Max(0.0f, tag.startTime),
                    endTime = Mathf.Min(clip.DurationInSeconds, tag.EndTime)
                });
            }

            segments.Sort((x, y) => x.startTime.CompareTo(y.startTime));

            // merge overlapping segments
            for (int i = 0; i < segments.Count - 1;)
            {
                if (segments[i + 1].startTime > segments[i].endTime)
                {
                    ++i;
                    continue;
                }

                segments[i] = new Segment()
                {
                    startTime = segments[i].startTime,
                    endTime = Mathf.Max(segments[i].endTime, segments[i + 1].endTime)
                };
                segments.RemoveAt(i + 1);
            }

            return segments;
        }
    }
}
