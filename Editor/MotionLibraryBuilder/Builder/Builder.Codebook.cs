using System;
using System.Collections.Generic;

using UnityEditor;

using Unity.Mathematics;
using Unity.Collections;

using UnityEngine.Assertions;

using PoseFragment = Unity.Kinematica.Binary.PoseFragment;
using TrajectoryFragment = Unity.Kinematica.Binary.TrajectoryFragment;
using MetricIndex = Unity.Kinematica.Binary.MetricIndex;

namespace Unity.Kinematica.Editor
{
    internal partial class Builder
    {
        public interface Fragment : IDisposable
        {
            NativeArray<float3> Features();

            float3 Feature(int index);
        }

        struct PoseFragmentWrapper : Fragment
        {
            public NativeArray<float3> Features() => fragment.array;

            public float3 Feature(int index) => fragment[index];

            public void Dispose()
            {
                fragment.Dispose();
            }

            public static Fragment Create(ref Binary binary, MetricIndex metricIndex, SamplingTime samplingTime)
            {
                var fragment = PoseFragment.Create(ref binary, metricIndex, samplingTime);

                return new PoseFragmentWrapper
                {
                    fragment = fragment
                };
            }

            PoseFragment fragment;
        }

        struct TrajectoryFragmentWrapper : Fragment
        {
            public NativeArray<float3> Features() => fragment.array;

            public float3 Feature(int index) => fragment[index];

            public void Dispose()
            {
                fragment.Dispose();
            }

            public static Fragment Create(ref Binary binary, MetricIndex metricIndex, SamplingTime samplingTime)
            {
                var fragment = TrajectoryFragment.Create(ref binary, metricIndex, samplingTime);

                return new TrajectoryFragmentWrapper
                {
                    fragment = fragment
                };
            }

            TrajectoryFragment fragment;
        }

        public interface FragmentFactory
        {
            int GetNumFeatures(ref Binary binary, MetricIndex metricIndex);

            int GetNumQuantizedFeatures(ref Binary binary, MetricIndex metricIndex);

            int GetNumNormalizedFeatures(ref Binary binary, MetricIndex metricIndex);

            Fragment Create(ref Binary binary, MetricIndex metricIndex, SamplingTime samplingTime);
        }

        struct PoseFragmentFactory : FragmentFactory
        {
            public int GetNumFeatures(ref Binary binary, MetricIndex metricIndex)
            {
                return PoseFragment.GetNumFeatures(ref binary, metricIndex);
            }

            public int GetNumQuantizedFeatures(ref Binary binary, MetricIndex metricIndex)
            {
                return 0;
            }

            public int GetNumNormalizedFeatures(ref Binary binary, MetricIndex metricIndex)
            {
                return 0;
            }

            public Fragment Create(ref Binary binary, MetricIndex metricIndex, SamplingTime samplingTime)
            {
                return PoseFragmentWrapper.Create(ref binary, metricIndex, samplingTime);
            }

            public static FragmentFactory Create()
            {
                return new PoseFragmentFactory();
            }
        }

        struct TrajectoryFragmentFactory : FragmentFactory
        {
            public int GetNumFeatures(ref Binary binary, MetricIndex metricIndex)
            {
                return TrajectoryFragment.GetNumFeatures(ref binary, metricIndex);
            }

            public int GetNumQuantizedFeatures(ref Binary binary, MetricIndex metricIndex)
            {
                return TrajectoryFragment.GetNumQuantizedFeatures(ref binary, metricIndex);
            }

            public int GetNumNormalizedFeatures(ref Binary binary, MetricIndex metricIndex)
            {
                return TrajectoryFragment.GetNumNormalizedFeatures(ref binary, metricIndex);
            }

            public Fragment Create(ref Binary binary, MetricIndex metricIndex, SamplingTime samplingTime)
            {
                return TrajectoryFragmentWrapper.Create(ref binary, metricIndex, samplingTime);
            }

            public static FragmentFactory Create()
            {
                return new TrajectoryFragmentFactory();
            }
        }

        class CodeBook
        {
            public int index;

            public Metric metric;

            public int traitIndex;

            public List<TaggedInterval> intervals = new List<TaggedInterval>();

            public class FragmentEncoder : IDisposable
            {
                public NativeArray<byte> codes;

                public NativeArray<byte> quantizedValues;

                public NativeArray<float3> codeWords;

                public NativeArray<BoundingBox> boundingBoxes;

                public NativeArray<Quantizer> quantizers;

                public int numFragments;

                public struct Settings
                {
                    public int metricIndex;
                    public int numAttempts;
                    public int numIterations;
                    public int minimumNumberSamples;
                    public int maximumNumberSamples;

                    public static Settings Default => new Settings();
                }

                public static FragmentEncoder Create(ref Binary binary, Settings settings, TaggedInterval[] intervals, FragmentFactory factory)
                {
                    return new FragmentEncoder(ref binary, settings, intervals, factory);
                }

                public void Dispose()
                {
                    codes.Dispose();

                    quantizedValues.Dispose();

                    codeWords.Dispose();

                    boundingBoxes.Dispose();

                    quantizers.Dispose();
                }

                FragmentEncoder(ref Binary binary, Settings settings, TaggedInterval[] intervals, FragmentFactory factory)
                {
                    //
                    // Prologue
                    //

                    int metricIndex = settings.metricIndex;

                    var numFeatures = factory.GetNumFeatures(ref binary, metricIndex);

                    var numQuantizedFeatures = factory.GetNumQuantizedFeatures(ref binary, metricIndex);

                    var numNormalizedFeatures = factory.GetNumNormalizedFeatures(ref binary, metricIndex);

                    var numTransformedFeatures = numFeatures - numQuantizedFeatures - numNormalizedFeatures;

                    numFragments = 0;

                    foreach (var interval in intervals)
                    {
                        numFragments += interval.numFrames;
                    }

                    //
                    // Generate fragments
                    //

                    var fragments = new Fragment[numFragments];

                    int writeIndex = 0;

                    foreach (var interval in intervals)
                    {
                        int segmentIndex = interval.segmentIndex;

                        Assert.IsTrue(
                            interval.firstFrame >=
                            interval.segment.destination.FirstFrame);

                        Assert.IsTrue(
                            interval.onePastLastFrame <=
                            interval.segment.destination.OnePastLastFrame);

                        int relativeFirstFrame =
                            interval.firstFrame -
                            interval.segment.destination.FirstFrame;

                        int numFrames = interval.numFrames;

                        for (int i = 0; i < numFrames; ++i)
                        {
                            int frameIndex = relativeFirstFrame + i;

                            var timeIndex =
                                TimeIndex.Create(
                                    segmentIndex, frameIndex);

                            var samplingTime =
                                SamplingTime.Create(timeIndex);

                            fragments[writeIndex++] =
                                factory.Create(ref binary, metricIndex, samplingTime);
                        }
                    }

                    Assert.IsTrue(writeIndex == numFragments);

                    //
                    // Generate feature quantizers
                    //

                    quantizers =
                        new NativeArray<Quantizer>(
                            numQuantizedFeatures, Allocator.Temp);

                    for (int i = 0; i < numQuantizedFeatures; ++i)
                    {
                        float minimum = float.MaxValue;
                        float maximum = float.MinValue;

                        for (int j = 0; j < numFragments; ++j)
                        {
                            var length =
                                math.length(
                                    fragments[j].Feature(i));

                            minimum = math.min(minimum, length);
                            maximum = math.max(maximum, length);
                        }

                        float range = maximum - minimum;

                        quantizers[i] = Quantizer.Create(minimum, range);
                    }

                    //
                    // Quantize magnitudes and normalize fragments
                    //

                    int numQuantizedValues = numFragments * numQuantizedFeatures;

                    quantizedValues =
                        new NativeArray<byte>(
                            numQuantizedValues, Allocator.Temp);

                    writeIndex = 0;

                    for (int i = 0; i < numFragments; ++i)
                    {
                        var features = fragments[i].Features();

                        Assert.IsTrue(features.Length == numFeatures);

                        for (int j = 0; j < numQuantizedFeatures; ++j)
                        {
                            var featureValue = features[j];

                            var length = math.length(featureValue);

                            var code = quantizers[j].Encode(length);

                            quantizedValues[writeIndex++] = code;

                            var defaultDirection = Missing.zero;

                            if (length < 0.02f)
                            {
                                features[j] = defaultDirection;
                            }
                            else
                            {
                                features[j] =
                                    math.normalizesafe(
                                        featureValue, defaultDirection);
                            }
                        }
                    }

                    //
                    // Generate bounding boxes for feature normalization
                    //

                    boundingBoxes =
                        new NativeArray<BoundingBox>(
                            numTransformedFeatures, Allocator.Temp);

                    var transformedIndex = numFeatures - numTransformedFeatures;

                    for (int i = 0; i < numTransformedFeatures; ++i)
                    {
                        var pointCloud = new float3[numFragments];

                        for (int j = 0; j < numFragments; ++j)
                        {
                            pointCloud[j] =
                                fragments[j].Feature(
                                    transformedIndex + i);
                        }

                        var boundingBox =
                            BoundingBox.Create(pointCloud);

                        boundingBoxes[i] = boundingBox;
                    }

                    //
                    // Normalize fragments
                    //

                    for (int i = 0; i < numFragments; ++i)
                    {
                        var features = fragments[i].Features();

                        Assert.IsTrue(features.Length == numFeatures);

                        for (int j = 0; j < numTransformedFeatures; ++j)
                        {
                            features[transformedIndex + j] =
                                boundingBoxes[j].normalize(
                                    features[transformedIndex + j]);
                        }
                    }

                    //
                    // Arrange fragment features in a single array for PQ encoding
                    //

                    var fragmentFeatures = new NativeSlice<float3>[numFragments];

                    for (int i = 0; i < numFragments; ++i)
                    {
                        fragmentFeatures[i] = fragments[i].Features();
                    }

                    //
                    // Product Quantization
                    //

                    int numCodes = numFragments * numFeatures;

                    int numCodeWords = numFeatures * 256;

                    codes = new NativeArray<byte>(numCodes, Allocator.Persistent);

                    codeWords = new NativeArray<float3>(numCodeWords, Allocator.Persistent);

                    var pqs = ProductQuantizer.Settings.Default;

                    pqs.numAttempts = settings.numAttempts;
                    pqs.numIterations = settings.numIterations;
                    pqs.minimumNumberSamples = settings.minimumNumberSamples;
                    pqs.maximumNumberSamples = settings.maximumNumberSamples;

                    var pq =
                        new ProductQuantizer(
                            numFeatures * 3, numFeatures, pqs);

                    pq.Train(fragmentFeatures, feedback =>
                    {
                        EditorUtility.DisplayProgressBar("Motion Synthesizer Asset",
                            feedback.info, feedback.percentage);
                    });

                    pq.ComputeCodes(fragmentFeatures, codes, feedback =>
                    {
                        EditorUtility.DisplayProgressBar("Motion Synthesizer Asset",
                            feedback.info, feedback.percentage);
                    });

                    Assert.IsTrue(pq.centroids.Length == numCodeWords * 3);

                    for (int i = 0; i < numCodeWords; ++i)
                    {
                        float x = pq.centroids[i * 3 + 0];
                        float y = pq.centroids[i * 3 + 1];
                        float z = pq.centroids[i * 3 + 2];

                        var centroid = new float3(x, y, z);

                        codeWords[i] = centroid;
                    }

                    pq.Dispose();

                    //
                    // Epilogue
                    //

                    for (int i = 0; i < numFragments; ++i)
                    {
                        fragments[i].Dispose();
                    }
                }
            }

            public static CodeBook Create(Metric metric, int traitIndex)
            {
                return new CodeBook
                {
                    metric = metric,
                    traitIndex = traitIndex
                };
            }

            public FragmentEncoder Generate(ref Binary binary, FragmentFactory factory)
            {
                var settings = FragmentEncoder.Settings.Default;

                settings.metricIndex = metric.index;
                settings.numAttempts = metric.numAttempts;
                settings.numIterations = metric.numIterations;
                settings.minimumNumberSamples = metric.minimumNumberSamples;
                settings.maximumNumberSamples = metric.maximumNumberSamples;

                return FragmentEncoder.Create(ref binary, settings, intervals.ToArray(), factory);
            }
        }

        List<CodeBook> codeBooks = new List<CodeBook>();

        public void BuildFragments()
        {
            GenerateCodeBooks();

            ref Binary binary = ref Binary;

            allocator.Allocate(codeBooks.Count, ref binary.codeBooks);

            int codeBookIndex = 0;

            foreach (var codeBook in codeBooks)
            {
                var metricIndex = codeBook.metric.index;
                var traitIndex = codeBook.traitIndex;

                ref var metric = ref binary.GetMetric(metricIndex);

                ref var destination = ref binary.codeBooks[codeBookIndex];

                destination.metricIndex = metricIndex;
                destination.traitIndex = traitIndex;

                var numIntervals = codeBook.intervals.Count;

                allocator.Allocate(numIntervals, ref destination.intervals);

                int intervalIndex = 0;

                foreach (var interval in codeBook.intervals)
                {
                    destination.intervals[intervalIndex] = interval.index;

                    intervalIndex++;
                }

                Assert.IsTrue(intervalIndex == codeBook.intervals.Count);

                {
                    var factory = codeBook.Generate(ref binary, PoseFragmentFactory.Create());

                    int numFragments = factory.numFragments;

                    Assert.IsTrue(destination.numFragments == 0);

                    destination.numFragments = numFragments;

                    var numFeatures = PoseFragment.GetNumFeatures(ref binary, metricIndex);

                    var numQuantizedFeatures = PoseFragment.GetNumQuantizedFeatures(ref binary, metricIndex);

                    var numNormalizedFeatures = PoseFragment.GetNumNormalizedFeatures(ref binary, metricIndex);

                    var numTransformedFeatures = numFeatures - numQuantizedFeatures - numNormalizedFeatures;

                    destination.poses.numFragments = numFragments;
                    destination.poses.numFeatures = (short)numFeatures;
                    destination.poses.numFeaturesQuantized = (short)numQuantizedFeatures;
                    destination.poses.numFeaturesNormalized = (short)numNormalizedFeatures;
                    destination.poses.numFeaturesTransformed = (short)numTransformedFeatures;

                    var numCodes = factory.codes.Length + factory.quantizedValues.Length;

                    allocator.Allocate(numCodes, ref destination.poses.codes);

                    int writeIndex = 0;
                    int readIndexQuantized = 0;
                    int readIndex = 0;

                    for (int i = 0; i < numFragments; ++i)
                    {
                        for (int j = 0; j < numQuantizedFeatures; ++j)
                        {
                            var quantizedValue = factory.quantizedValues[readIndexQuantized++];

                            destination.poses.codes[writeIndex++] = quantizedValue;
                        }

                        for (int j = 0; j < numFeatures; ++j)
                        {
                            var code = factory.codes[readIndex++];

                            destination.poses.codes[writeIndex++] = code;
                        }
                    }

                    Assert.IsTrue(readIndexQuantized == factory.quantizedValues.Length);
                    Assert.IsTrue(readIndex == factory.codes.Length);
                    Assert.IsTrue(writeIndex == numCodes);

                    var numCodeWords = factory.codeWords.Length;

                    allocator.Allocate(numCodeWords, ref destination.poses.centroids);

                    for (int i = 0; i < numCodeWords; ++i)
                    {
                        destination.poses.centroids[i] = factory.codeWords[i];
                    }

                    allocator.Allocate(numTransformedFeatures, ref destination.poses.boundingBoxes);

                    Assert.IsTrue(numTransformedFeatures == factory.boundingBoxes.Length);

                    for (int i = 0; i < numTransformedFeatures; ++i)
                    {
                        destination.poses.boundingBoxes[i].transform = factory.boundingBoxes[i].transform;
                        destination.poses.boundingBoxes[i].extent = factory.boundingBoxes[i].extent;
                        destination.poses.boundingBoxes[i].inverseDiagonal = factory.boundingBoxes[i].inverseDiagonal;
                    }

                    allocator.Allocate(numQuantizedFeatures, ref destination.poses.quantizers);

                    Assert.IsTrue(numQuantizedFeatures == factory.quantizers.Length);

                    for (int i = 0; i < numQuantizedFeatures; ++i)
                    {
                        destination.poses.quantizers[i].minimum = factory.quantizers[i].minimum;
                        destination.poses.quantizers[i].range = factory.quantizers[i].range;
                    }

                    factory.Dispose();
                }

                {
                    var factory = codeBook.Generate(ref binary, TrajectoryFragmentFactory.Create());

                    int numFragments = factory.numFragments;

                    Assert.IsTrue(destination.numFragments == numFragments);

                    var numFeatures = TrajectoryFragment.GetNumFeatures(ref binary, metricIndex);

                    var numQuantizedFeatures = TrajectoryFragment.GetNumQuantizedFeatures(ref binary, metricIndex);

                    var numNormalizedFeatures = TrajectoryFragment.GetNumNormalizedFeatures(ref binary, metricIndex);

                    var numTransformedFeatures = numFeatures - numQuantizedFeatures - numNormalizedFeatures;

                    destination.trajectories.numFragments = numFragments;
                    destination.trajectories.numFeatures = (short)numFeatures;
                    destination.trajectories.numFeaturesQuantized = (short)numQuantizedFeatures;
                    destination.trajectories.numFeaturesNormalized = (short)numNormalizedFeatures;
                    destination.trajectories.numFeaturesTransformed = (short)numTransformedFeatures;

                    var numCodes = factory.codes.Length + factory.quantizedValues.Length;

                    allocator.Allocate(numCodes, ref destination.trajectories.codes);

                    int writeIndex = 0;
                    int readIndexQuantized = 0;
                    int readIndex = 0;

                    for (int i = 0; i < numFragments; ++i)
                    {
                        for (int j = 0; j < numQuantizedFeatures; ++j)
                        {
                            var quantizedValue = factory.quantizedValues[readIndexQuantized++];

                            destination.trajectories.codes[writeIndex++] = quantizedValue;
                        }

                        for (int j = 0; j < numFeatures; ++j)
                        {
                            var code = factory.codes[readIndex++];

                            destination.trajectories.codes[writeIndex++] = code;
                        }
                    }

                    Assert.IsTrue(readIndexQuantized == factory.quantizedValues.Length);
                    Assert.IsTrue(readIndex == factory.codes.Length);
                    Assert.IsTrue(writeIndex == numCodes);

                    var numCodeWords = factory.codeWords.Length;

                    allocator.Allocate(numCodeWords, ref destination.trajectories.centroids);

                    for (int i = 0; i < numCodeWords; ++i)
                    {
                        destination.trajectories.centroids[i] = factory.codeWords[i];
                    }

                    allocator.Allocate(numTransformedFeatures, ref destination.trajectories.boundingBoxes);

                    Assert.IsTrue(numTransformedFeatures == factory.boundingBoxes.Length);

                    for (int i = 0; i < numTransformedFeatures; ++i)
                    {
                        destination.trajectories.boundingBoxes[i].transform = factory.boundingBoxes[i].transform;
                        destination.trajectories.boundingBoxes[i].extent = factory.boundingBoxes[i].extent;
                        destination.trajectories.boundingBoxes[i].inverseDiagonal = factory.boundingBoxes[i].inverseDiagonal;
                    }

                    allocator.Allocate(numQuantizedFeatures, ref destination.trajectories.quantizers);

                    Assert.IsTrue(numQuantizedFeatures == factory.quantizers.Length);

                    for (int i = 0; i < numQuantizedFeatures; ++i)
                    {
                        destination.trajectories.quantizers[i].minimum = factory.quantizers[i].minimum;
                        destination.trajectories.quantizers[i].range = factory.quantizers[i].range;
                    }

                    factory.Dispose();
                }

                codeBookIndex++;
            }

            Assert.IsTrue(codeBookIndex == codeBooks.Count);
        }

        void GenerateCodeBooks()
        {
            int intervalIndex = 0;

            ref Binary binary = ref Binary;

            foreach (var taggedInterval in intervals)
            {
                (var metric, var traitIndex) = GetMetric(taggedInterval);

                Assert.IsTrue(
                    binary.intervals[intervalIndex].codeBookIndex ==
                    Binary.CodeBookIndex.Invalid);

                if (metric != null)
                {
                    var codeBook = GetOrCreateCodeBook(metric, traitIndex);

                    codeBook.intervals.Add(taggedInterval);

                    binary.intervals[intervalIndex].codeBookIndex = codeBook.index;
                }

                intervalIndex++;
            }

            Assert.IsTrue(intervalIndex == intervals.Count);
        }

        CodeBook GetOrCreateCodeBook(Metric metric, int traitIndex)
        {
            foreach (var codeBook in codeBooks)
            {
                if (codeBook.metric == metric)
                {
                    if (codeBook.traitIndex == traitIndex)
                    {
                        return codeBook;
                    }
                }
            }

            var result = CodeBook.Create(metric, traitIndex);

            result.index = codeBooks.Count;

            codeBooks.Add(result);

            return result;
        }
    }
}
