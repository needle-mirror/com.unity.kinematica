using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

using UnityEngine.Assertions;

using BoundingBox = Unity.Kinematica.Binary.CodeBook.Encoding.BoundingBox;
using Quantizer = Unity.Kinematica.Binary.CodeBook.Encoding.Quantizer;

namespace Unity.Kinematica
{
    /// <summary>
    /// The reduce task accepts a list of animation pose sequences
    /// and a reference animation pose as its inputs. It outputs the
    /// animation pose from the input pose sequence that is most
    /// similar to the reference animation pose. A trajectory can
    /// optionally be supplied as an additional constraint.
    /// </summary>
    [Data("Reduce", "#2A3756"), BurstCompile]
    public struct ReduceTask : Task
    {
        internal MemoryRef<MotionSynthesizer> synthesizer;

        [Input("Trajectory")]
        internal Identifier<Trajectory> trajectory;

        [Input("Sequences")]
        internal Identifier<PoseSequence> sequences;

        [Input("Sampling Time")]
        internal Identifier<SamplingTime> samplingTime;

        [Output("Closest Match")]
        internal Identifier<TimeIndex> closestMatch;

        /// <summary>
        /// Denotes a value between 0 and 1 that controls
        /// the relative weight between poses and trajectories.
        /// </summary>
        [Property]
        public float responsiveness;

        [Property]
        internal float threshold;

        /// <summary>
        /// Execute method for the reduce task.
        /// </summary>
        /// <remarks>
        /// The reduce task accepts a list of animation pose sequences
        /// and a reference animation pose as its inputs. It outputs the
        /// animation pose from the input pose sequence that is most
        /// similar to the reference animation pose. A trajectory can
        /// optionally be supplied as an additional constraint.
        /// </remarks>
        /// <returns>Returns true if the output time index is valid; false otherwise.</returns>
        public Result Execute()
        {
            if (samplingTime.IsValid)
            {
                if (trajectory.IsValid)
                {
                    return SelectMatchingPoseAndTrajectory();
                }
                else
                {
                    return SelectMatchingPose();
                }
            }
            else
            {
                return SelectFirstPose();
            }
        }

        Result SelectFirstPose()
        {
            ref var synthesizer = ref this.synthesizer.Ref;

            ref Binary binary = ref synthesizer.Binary;

            var sequences =
                synthesizer.GetArray<PoseSequence>(
                    this.sequences);

            Assert.IsTrue(sequences.IsValid);

            var samplingTime =
                synthesizer.GetRef<TimeIndex>(
                    closestMatch);

            Assert.IsTrue(samplingTime.IsValid);

            if (sequences.length > 0)
            {
                ref var interval = ref binary.GetInterval(
                    sequences[0].intervalIndex);

                samplingTime.Ref =
                    interval.GetTimeIndex(
                        sequences[0].firstFrame);

                return Result.Success;
            }

            return Result.Failure;
        }

        internal struct Query
        {
            NativeArray<float3> poseFeatures;
            NativeArray<float3> trajectoryFeatures;

            NativeArray<float> poseDistances;
            NativeArray<float> trajectoryDistances;

            public struct Header
            {
                public int numFeatures;
                public int numFeaturesFlattened;

                public int featureIndex;
                public int distanceIndex;

                public static Header Create(int numFeatures, int numFeaturesFlattened, int featureIndex, int distanceIndex)
                {
                    return new Header
                    {
                        numFeatures = numFeatures,
                        numFeaturesFlattened = numFeaturesFlattened,

                        featureIndex = featureIndex,
                        distanceIndex = distanceIndex
                    };
                }
            };

            NativeArray<Header> poseHeaders;
            NativeArray<Header> trajectoryHeaders;

            Query(ref Binary binary)
            {
                int numCodeBooks = binary.numCodeBooks;

                poseHeaders = new NativeArray<Header>(numCodeBooks, Allocator.Temp);
                trajectoryHeaders = new NativeArray<Header>(numCodeBooks, Allocator.Temp);

                int poseFeatureIndex = 0;
                int poseDistanceIndex = 0;
                int trajectoryFeatureIndex = 0;
                int trajectoryDistanceIndex = 0;

                for (int i = 0; i < numCodeBooks; ++i)
                {
                    ref var codeBook = ref binary.GetCodeBook(i);

                    int numPoseFeatures =
                        codeBook.poses.numFeatures;

                    int numPoseFeaturesFlattened =
                        codeBook.poses.numFeaturesFlattened;

                    int numTrajectoryFeatures =
                        codeBook.trajectories.numFeatures;

                    int numTrajectoryFeaturesFlattened =
                        codeBook.trajectories.numFeaturesFlattened;

                    poseHeaders[i] =
                        Header.Create(numPoseFeatures, numPoseFeaturesFlattened,
                            poseFeatureIndex, poseDistanceIndex);

                    trajectoryHeaders[i] =
                        Header.Create(numTrajectoryFeatures, numTrajectoryFeaturesFlattened,
                            trajectoryFeatureIndex, trajectoryDistanceIndex);

                    poseFeatureIndex += numPoseFeatures;
                    poseDistanceIndex += numPoseFeaturesFlattened;

                    trajectoryFeatureIndex += numTrajectoryFeatures;
                    trajectoryDistanceIndex += numTrajectoryFeaturesFlattened;
                }

                poseFeatures = new NativeArray<float3>(poseFeatureIndex, Allocator.Temp);
                trajectoryFeatures = new NativeArray<float3>(trajectoryFeatureIndex, Allocator.Temp);

                poseDistances = new NativeArray<float>(poseDistanceIndex * 256, Allocator.Temp);
                trajectoryDistances = new NativeArray<float>(trajectoryDistanceIndex * 256, Allocator.Temp);
            }

            unsafe bool FastDecodePose(ref Binary binary, int codeBookIndex, TimeIndex timeIndex)
            {
                Assert.IsTrue(codeBookIndex < binary.numCodeBooks);

                ref var codeBook =
                    ref binary.GetCodeBook(codeBookIndex);

                int numPoseFeatures =
                    codeBook.poses.numFeatures;

                int numPoseFeaturesFlattened =
                    codeBook.poses.numFeaturesFlattened;

                var poseHeader = poseHeaders[codeBookIndex];

                Assert.IsTrue(numPoseFeatures == poseHeader.numFeatures);
                Assert.IsTrue(numPoseFeaturesFlattened == poseHeader.numFeaturesFlattened);

                int numIntervals = codeBook.intervals.Length;

                int fragmentIndex = 0;

                for (int i = 0; i < numIntervals; ++i)
                {
                    var intervalIndex = codeBook.intervals[i];

                    ref var interval = ref binary.GetInterval(intervalIndex);

                    if (interval.Contains(timeIndex))
                    {
                        var relativeIndex =
                            timeIndex.frameIndex - interval.firstFrame;

                        var index = fragmentIndex + relativeIndex;

                        var poseDestination = new NativeSlice<float3>(
                            poseFeatures, poseHeader.featureIndex, numPoseFeatures);

                        codeBook.poses.DecodeFeatures(poseDestination, index);

                        return true;
                    }

                    fragmentIndex += interval.numFrames;
                }

                return false;
            }

            public unsafe void GeneratePoseDistanceTable(ref Binary binary, int codeBookIndex, float weightFactor = 1.0f)
            {
                ref var codeBook =
                    ref binary.GetCodeBook(codeBookIndex);

                var numFeaturesQuantized =
                    codeBook.poses.numFeaturesQuantized;

                var numFeatures =
                    codeBook.poses.numFeatures;

                int numFeaturesFlattened =
                    codeBook.poses.numFeaturesFlattened;

                var header = poseHeaders[codeBookIndex];

                Assert.IsTrue(numFeatures == header.numFeatures);
                Assert.IsTrue(numFeaturesFlattened == header.numFeaturesFlattened);

                var centroids = (float3*)codeBook.poses.centroids.GetUnsafePtr();
                var boundingBoxes = (BoundingBox*)codeBook.poses.boundingBoxes.GetUnsafePtr();
                var quantizers = (Quantizer*)codeBook.poses.quantizers.GetUnsafePtr();

                var features = (float3*)NativeArrayUnsafeUtility.GetUnsafePtr(poseFeatures);
                var distances = (float*)NativeArrayUnsafeUtility.GetUnsafePtr(poseDistances);

                features += header.featureIndex;
                distances += header.distanceIndex;

                weightFactor *= math.rcp(numFeaturesFlattened);

                var writeIndex = 0;

                for (int i = 0; i < numFeaturesQuantized; ++i)
                {
                    var featureValue = features[i];

                    var magnitude = math.length(featureValue);

                    Assert.IsTrue(magnitude >= 0.0f && magnitude <= 1.0f);

                    for (int j = 0; j <= 255; ++j)
                    {
                        var reference = j / 255.0f;

                        var difference =
                            math.abs(magnitude - reference);

                        Assert.IsTrue(difference >= 0.0f && difference <= 1.0f);

                        distances[writeIndex++] = difference * 2.0f * weightFactor;
                    }
                }

                for (int i = 0; i < numFeaturesQuantized; ++i)
                {
                    var featureValue = features[i];

                    var normalizedFeatureValue =
                        math.normalizesafe(features[i], Missing.zero);

                    for (int j = 0; j <= 255; ++j)
                    {
                        var centroid = centroids[(i << 8) + j];

                        var difference =
                            -math.dot(centroid,
                                normalizedFeatureValue) * 0.5f + 0.5f;

                        distances[writeIndex++] = difference * weightFactor;
                    }
                }

                var numFeaturesNormalized =
                    codeBook.poses.numFeaturesNormalized;

                for (int i = 0; i < numFeaturesNormalized; ++i)
                {
                    var index = codeBook.poses.normalizedIndex + i;

                    var featureValue = features[index];

                    for (int j = 0; j <= 255; ++j)
                    {
                        var centroid = centroids[(index << 8) + j];

                        var difference =
                            -math.dot(centroid,
                                featureValue) * 0.5f + 0.5f;

                        distances[writeIndex++] = difference * weightFactor;
                    }
                }

                var numFeaturesTransformed =
                    codeBook.poses.numFeaturesTransformed;

                for (int i = 0; i < numFeaturesTransformed; ++i)
                {
                    var index = codeBook.poses.transformedIndex + i;

                    var featureValue = features[index];

                    for (int j = 0; j <= 255; ++j)
                    {
                        var centroid = centroids[(index << 8) + j];

                        var difference = centroid - featureValue;

                        var normalizedFeatureCost =
                            math.length(difference) * boundingBoxes[i].inverseDiagonal;

                        distances[writeIndex++] = normalizedFeatureCost * weightFactor;
                    }
                }

                Assert.IsTrue(writeIndex == numFeaturesFlattened * 256);
            }

            public unsafe void DecodePose(ref Binary binary, int codeBookIndex, TimeIndex timeIndex)
            {
                Assert.IsTrue(timeIndex.IsValid);

                if (!FastDecodePose(ref binary, codeBookIndex, timeIndex))
                {
                    int numCodeBooks = binary.numCodeBooks;

                    Assert.IsTrue(codeBookIndex < numCodeBooks);

                    ref var codeBook =
                        ref binary.GetCodeBook(codeBookIndex);

                    var metricIndex = codeBook.metricIndex;

                    var samplingTime = SamplingTime.Create(timeIndex);

                    var poseFragment =
                        binary.CreatePoseFragment(
                            metricIndex, samplingTime);

                    int numPoseFeatures = poseFragment.array.Length;

                    Assert.IsTrue(numPoseFeatures == codeBook.poses.numFeatures);

                    var poseHeader = poseHeaders[codeBookIndex];

                    var poseDestination = new NativeSlice<float3>(
                        poseFeatures, poseHeader.featureIndex,
                        numPoseFeatures);

                    codeBook.poses.Normalize(
                        poseDestination, poseFragment.array);

                    poseFragment.Dispose();
                }
            }

            public unsafe void GenerateTrajectoryDistanceTable(ref Binary binary, int codeBookIndex, float weightFactor = 1.0f)
            {
                ref var codeBook =
                    ref binary.GetCodeBook(codeBookIndex);

                var numFeaturesQuantized =
                    codeBook.trajectories.numFeaturesQuantized;

                var numFeatures =
                    codeBook.trajectories.numFeatures;

                int numFeaturesFlattened =
                    codeBook.trajectories.numFeaturesFlattened;

                var header = trajectoryHeaders[codeBookIndex];

                Assert.IsTrue(numFeatures == header.numFeatures);
                Assert.IsTrue(numFeaturesFlattened == header.numFeaturesFlattened);

                var centroids = (float3*)codeBook.trajectories.centroids.GetUnsafePtr();
                var boundingBoxes = (BoundingBox*)codeBook.trajectories.boundingBoxes.GetUnsafePtr();
                var quantizers = (Quantizer*)codeBook.trajectories.quantizers.GetUnsafePtr();

                var features = (float3*)NativeArrayUnsafeUtility.GetUnsafePtr(trajectoryFeatures);
                var distances = (float*)NativeArrayUnsafeUtility.GetUnsafePtr(trajectoryDistances);

                features += header.featureIndex;
                distances += header.distanceIndex;

                weightFactor *= math.rcp(numFeaturesFlattened);

                var writeIndex = 0;

                for (int i = 0; i < numFeaturesQuantized; ++i)
                {
                    var featureValue = features[i];

                    var magnitude = math.length(featureValue);

                    magnitude = math.clamp(magnitude, 0.0f, 1.0f);

                    for (int j = 0; j <= 255; ++j)
                    {
                        var reference = j / 255.0f;

                        var difference =
                            math.abs(magnitude - reference);

                        Assert.IsTrue(difference >= 0.0f && difference <= 1.0f);

                        distances[writeIndex++] = difference * 2.0f * weightFactor;
                    }
                }

                for (int i = 0; i < numFeaturesQuantized; ++i)
                {
                    var featureValue = features[i];

                    var normalizedFeatureValue =
                        math.normalizesafe(features[i], Missing.zero);

                    for (int j = 0; j <= 255; ++j)
                    {
                        var centroid = centroids[(i << 8) + j];

                        var difference =
                            -math.dot(centroid,
                                normalizedFeatureValue) * 0.5f + 0.5f;

                        distances[writeIndex++] = difference * weightFactor;
                    }
                }

                var numFeaturesNormalized =
                    codeBook.trajectories.numFeaturesNormalized;

                for (int i = 0; i < numFeaturesNormalized; ++i)
                {
                    var index = codeBook.trajectories.normalizedIndex + i;

                    var featureValue = features[index];

                    for (int j = 0; j <= 255; ++j)
                    {
                        var centroid = centroids[(index << 8) + j];

                        var difference =
                            -math.dot(centroid,
                                featureValue) * 0.5f + 0.5f;

                        distances[writeIndex++] = difference * weightFactor;
                    }
                }

                var numFeaturesTransformed =
                    codeBook.trajectories.numFeaturesTransformed;

                for (int i = 0; i < numFeaturesTransformed; ++i)
                {
                    var index = codeBook.trajectories.transformedIndex + i;

                    var featureValue = features[index];

                    for (int j = 0; j <= 255; ++j)
                    {
                        var centroid = centroids[(index << 8) + j];

                        var difference = centroid - featureValue;

                        var normalizedFeatureCost =
                            math.length(difference) * boundingBoxes[i].inverseDiagonal;

                        distances[writeIndex++] = normalizedFeatureCost * weightFactor;
                    }
                }

                Assert.IsTrue(writeIndex == numFeaturesFlattened * 256);
            }

            public void DecodeTrajectory(ref Binary binary, int codeBookIndex, MemoryArray<AffineTransform> trajectory)
            {
                int numCodeBooks = binary.numCodeBooks;

                Assert.IsTrue(codeBookIndex < numCodeBooks);

                ref var codeBook =
                    ref binary.GetCodeBook(codeBookIndex);

                var metricIndex = codeBook.metricIndex;

                var trajectoryFragment =
                    binary.CreateTrajectoryFragment(
                        metricIndex, trajectory);

                var header = trajectoryHeaders[codeBookIndex];

                Assert.IsTrue(trajectoryFragment.array.Length == header.numFeatures);

                var trajectoryDestination =
                    new NativeSlice<float3>(trajectoryFeatures,
                        header.featureIndex, header.numFeatures);

                codeBook.trajectories.Normalize(
                    trajectoryDestination, trajectoryFragment.array);

                trajectoryFragment.Dispose();
            }

            public static Query Create(ref Binary binary)
            {
                return new Query(ref binary);
            }

            public NativeSlice<float3> GetPoseSlice(int index)
            {
                Assert.IsTrue(index < poseHeaders.Length);

                var featureIndex = poseHeaders[index].featureIndex;
                var numFeatures = poseHeaders[index].numFeatures;

                return new NativeSlice<float3>(
                    poseFeatures, featureIndex, numFeatures);
            }

            public NativeSlice<float3> GetTrajectorySlice(int index)
            {
                Assert.IsTrue(index < trajectoryHeaders.Length);

                var featureIndex = trajectoryHeaders[index].featureIndex;
                var numFeatures = trajectoryHeaders[index].numFeatures;

                return new NativeSlice<float3>(
                    trajectoryFeatures, featureIndex, numFeatures);
            }

            public unsafe float* GetPoseDistances(int index)
            {
                Assert.IsTrue(index < poseHeaders.Length);

                var featureIndex = poseHeaders[index].featureIndex << 8;

                return (float*)poseDistances.GetUnsafePtr() + featureIndex;
            }

            public NativeSlice<float> GetPoseDistanceSlice(int index)
            {
                Assert.IsTrue(index < poseHeaders.Length);

                var featureIndex = poseHeaders[index].featureIndex;
                var numFeatures = poseHeaders[index].numFeatures;

                return new NativeSlice<float>(
                    poseDistances, featureIndex << 8, numFeatures << 8);
            }

            public unsafe float* GetTrajectoryDistances(int index)
            {
                Assert.IsTrue(index < trajectoryHeaders.Length);

                var featureIndex = trajectoryHeaders[index].featureIndex << 8;

                return (float*)trajectoryDistances.GetUnsafePtr() + featureIndex;
            }

            public NativeSlice<float> GetTrajectoryDistanceSlice(int index)
            {
                Assert.IsTrue(index < trajectoryHeaders.Length);

                var featureIndex = trajectoryHeaders[index].featureIndex;
                var numFeatures = trajectoryHeaders[index].numFeatures;

                return new NativeSlice<float>(
                    trajectoryDistances, featureIndex << 8, numFeatures << 8);
            }

            public void Dispose()
            {
                poseFeatures.Dispose();
                trajectoryFeatures.Dispose();
                poseHeaders.Dispose();
                trajectoryHeaders.Dispose();
            }
        }

        bool RefersToCodeBook(ref Binary binary, MemoryArray<PoseSequence> sequences, int codeBookIndex)
        {
            var numSequences = sequences.length;

            for (int i = 0; i < numSequences; ++i)
            {
                var intervalIndex = sequences[i].intervalIndex;

                ref var interval = ref binary.GetInterval(intervalIndex);

                if (interval.codeBookIndex == codeBookIndex)
                {
                    return true;
                }
            }

            return false;
        }

        unsafe Result SelectMatchingPose()
        {
            ref var synthesizer = ref this.synthesizer.Ref;

            ref Binary binary = ref synthesizer.Binary;

            var intervals = binary.GetIntervals();

            var samplingTime =
                synthesizer.GetRef<SamplingTime>(
                    this.samplingTime);

            var sequences =
                synthesizer.GetArray<PoseSequence>(
                    this.sequences);

            AdjustSequenceBoundaries(sequences);

            var query = Query.Create(ref binary);

            int numCodeBooks = binary.numCodeBooks;

            for (int i = 0; i < numCodeBooks; ++i)
            {
                if (RefersToCodeBook(ref binary, sequences, i))
                {
                    query.DecodePose(
                        ref binary, i, samplingTime.Ref.timeIndex);

                    query.GeneratePoseDistanceTable(ref binary, i);
                }
            }

            float minimumDeviation = float.MaxValue;

            if (threshold > 0.0f)
            {
                minimumDeviation = threshold;
            }

            ref TimeIndex closestMatch =
                ref synthesizer.GetRef<TimeIndex>(
                    this.closestMatch).Ref;

            closestMatch = TimeIndex.Invalid;

            var numSequences = sequences.length;

            for (int i = 0; i < numSequences; ++i)
            {
                var intervalIndex = sequences[i].intervalIndex;

                ref var interval = ref binary.GetInterval(intervalIndex);

                Assert.IsTrue(interval.Contains(sequences[i].firstFrame));

                var codeBookIndex = interval.codeBookIndex;
                if (!codeBookIndex.IsValid)
                {
                    throw new NoMetricException(this.synthesizer, interval.segmentIndex);
                }

                var segmentIndex = (short)interval.segmentIndex;

                ref var codeBook = ref binary.GetCodeBook(codeBookIndex);

                var intervalFragmentIndex =
                    codeBook.GetFragmentIndex(
                        intervals, intervalIndex);

                Assert.IsTrue(intervalFragmentIndex >= 0);

                var fragmentIndex =
                    intervalFragmentIndex + sequences[i].firstFrame;

                Assert.IsTrue(fragmentIndex < codeBook.numFragments);

                var numFrames = sequences[i].numFrames;

                Assert.IsTrue(numFrames <= intervals[intervalIndex].numFrames);

                Assert.IsTrue((fragmentIndex + numFrames) <= codeBook.numFragments);

                float* poseDistances =
                    query.GetPoseDistances(codeBookIndex);

                short frameIndex = (short)(sequences[i].firstFrame);

                int numPoseFeatures = codeBook.poses.numFeaturesFlattened;

                byte* poseCodes =
                    (byte*)codeBook.poses.codes.GetUnsafePtr() +
                    numPoseFeatures * fragmentIndex;

                for (int j = 0; j < numFrames; ++j)
                {
                    float candidateDeviation = 0.0f;

                    float* ptr = poseDistances;

                    for (int k = 0; k < numPoseFeatures; ++k)
                    {
                        candidateDeviation += ptr[*poseCodes++];

                        ptr += 256;
                    }

                    if (candidateDeviation < minimumDeviation)
                    {
                        minimumDeviation = candidateDeviation;

                        closestMatch.segmentIndex = segmentIndex;
                        closestMatch.frameIndex = frameIndex;
                    }

                    fragmentIndex++;
                    frameIndex++;
                }
            }

            query.Dispose();

            if (closestMatch.IsValid)
            {
                return Result.Success;
            }

            return Result.Failure;
        }

        void AdjustSequenceBoundaries(MemoryArray<PoseSequence> sequences)
        {
            ref var synthesizer = ref this.synthesizer.Ref;

            ref Binary binary = ref synthesizer.Binary;

            var numSequences = sequences.length;

            var timeHorizon = binary.TimeHorizon;

            var sampleRate = binary.SampleRate;

            var numBoundaryFrames = Missing.truncToInt(timeHorizon * sampleRate);

            for (int i = 0; i < numSequences; ++i)
            {
                var sequence = sequences[i];

                var intervalIndex = sequence.intervalIndex;

                ref var interval = ref binary.GetInterval(intervalIndex);

                Assert.IsTrue(interval.Contains(sequence.firstFrame));

                ref var segment = ref binary.GetSegment(interval.segmentIndex);

                var onePastLastFrame = sequence.onePastLastFrame;

                var boundaryFrameLeft = numBoundaryFrames;
                if (segment.previousSegment.IsValid)
                {
                    boundaryFrameLeft -= binary.GetSegment(segment.previousSegment).destination.numFrames;
                }

                var boundaryFrameRight = segment.destination.numFrames - numBoundaryFrames;
                if (segment.nextSegment.IsValid)
                {
                    boundaryFrameRight += binary.GetSegment(segment.nextSegment).destination.numFrames;
                }

                sequence.firstFrame = math.max(boundaryFrameLeft, sequence.firstFrame);

                onePastLastFrame = math.min(boundaryFrameRight, onePastLastFrame);

                sequence.numFrames = onePastLastFrame - sequence.firstFrame;

                if (sequence.numFrames <= 0)
                {
                    bool intervalValid = false;

                    ref Binary.TagList tagList = ref binary.GetTagList(interval.tagListIndex);
                    for (int j = 0; j < tagList.numIndices; ++j)
                    {
                        ref Binary.Tag tag = ref binary.GetTag(tagList.tagIndicesIndex + j);
                        int tagFirstFrame = math.max(boundaryFrameLeft, tag.FirstFrame);
                        int tagOnePastLastFrame = math.max(boundaryFrameRight, tag.OnePastLastFrame);

                        if (tagFirstFrame < tagOnePastLastFrame)
                        {
                            // intersection is too short, but one of the tags it contains is long enough, which is enough to make the intersection valid
                            intervalValid = true;
                            break;
                        }
                    }

                    if (!intervalValid)
                    {
                        throw new SegmentTooShortException(this.synthesizer, interval.segmentIndex);
                    }

                    continue;
                }

                sequences[i] = sequence;
            }
        }

        unsafe Result SelectMatchingPoseAndTrajectory()
        {
            ref var synthesizer = ref this.synthesizer.Ref;

            ref Binary binary = ref synthesizer.Binary;

            var intervals = binary.GetIntervals();

            var samplingTime =
                synthesizer.GetRef<SamplingTime>(
                    this.samplingTime);

            var sequences =
                synthesizer.GetArray<PoseSequence>(
                    this.sequences);

            AdjustSequenceBoundaries(sequences);

            var trajectory =
                synthesizer.GetArray<AffineTransform>(
                    this.trajectory);

            var query = Query.Create(ref binary);

            int numCodeBooks = binary.numCodeBooks;

            var poseWeight = 1.0f - responsiveness;
            var trajectoryWeight = responsiveness;

            for (int i = 0; i < numCodeBooks; ++i)
            {
                if (RefersToCodeBook(ref binary, sequences, i))
                {
                    query.DecodePose(
                        ref binary, i, samplingTime.Ref.timeIndex);

                    query.GeneratePoseDistanceTable(
                        ref binary, i, poseWeight);

                    query.DecodeTrajectory(
                        ref binary, i, trajectory);

                    query.GenerateTrajectoryDistanceTable(
                        ref binary, i, trajectoryWeight);
                }
            }

            float minimumDeviation = float.MaxValue;

            if (threshold > 0.0f)
            {
                minimumDeviation = threshold;
            }

            ref TimeIndex closestMatch =
                ref synthesizer.GetRef<TimeIndex>(
                    this.closestMatch).Ref;

            closestMatch = TimeIndex.Invalid;

            var numSequences = sequences.length;

            for (int i = 0; i < numSequences; ++i)
            {
                var intervalIndex = sequences[i].intervalIndex;

                ref var interval = ref binary.GetInterval(intervalIndex);

                Assert.IsTrue(interval.Contains(sequences[i].firstFrame));

                var codeBookIndex = interval.codeBookIndex;
                if (codeBookIndex.value < 0)
                {
                    throw new NoMetricException(this.synthesizer, interval.segmentIndex);
                }

                var segmentIndex = (short)interval.segmentIndex;

                ref var codeBook = ref binary.GetCodeBook(codeBookIndex);

                var intervalFragmentIndex =
                    codeBook.GetFragmentIndex(
                        intervals, intervalIndex);

                Assert.IsTrue(intervalFragmentIndex >= 0);

                var fragmentIndex =
                    intervalFragmentIndex + sequences[i].firstFrame;

                Assert.IsTrue(fragmentIndex < codeBook.numFragments);

                var numFrames = sequences[i].numFrames;

                Assert.IsTrue(numFrames <= intervals[intervalIndex].numFrames);

                Assert.IsTrue((fragmentIndex + numFrames) <= codeBook.numFragments);

                float* poseDistances =
                    query.GetPoseDistances(codeBookIndex);

                float* trajectoryDistances =
                    query.GetTrajectoryDistances(codeBookIndex);

                short frameIndex = (short)(sequences[i].firstFrame);

                int numPoseFeatures = codeBook.poses.numFeaturesFlattened;
                int numTrajectoryFeatures = codeBook.trajectories.numFeaturesFlattened;

                byte* poseCodes =
                    (byte*)codeBook.poses.codes.GetUnsafePtr() +
                    numPoseFeatures * fragmentIndex;

                byte* trajectoryCodes =
                    (byte*)codeBook.trajectories.codes.GetUnsafePtr() +
                    numTrajectoryFeatures * fragmentIndex;

                float* ptr;

                for (int j = 0; j < numFrames; ++j)
                {
                    float candidateDeviation = 0.0f;

                    ptr = poseDistances;

                    for (int k = 0; k < numPoseFeatures; ++k)
                    {
                        candidateDeviation += ptr[*poseCodes++];

                        ptr += 256;
                    }

                    ptr = trajectoryDistances;

                    for (int k = 0; k < numTrajectoryFeatures; ++k)
                    {
                        candidateDeviation += ptr[*trajectoryCodes++];

                        ptr += 256;
                    }

                    if (candidateDeviation < minimumDeviation)
                    {
                        minimumDeviation = candidateDeviation;

                        closestMatch.segmentIndex = segmentIndex;
                        closestMatch.frameIndex = frameIndex;
                    }

                    fragmentIndex++;
                    frameIndex++;
                }
            }

            query.Dispose();

            if (closestMatch.IsValid)
            {
                return Result.Success;
            }

            return Result.Failure;
        }

        /// <summary>
        /// Surrogate method for automatic task execution.
        /// </summary>
        /// <param name="self">Task reference that is supposed to be executed.</param>
        /// <returns>Result of the task execution.</returns>
        [BurstCompile]
        public static Result ExecuteSelf(ref TaskRef self)
        {
            return self.Cast<ReduceTask>().Execute();
        }

        internal static ReduceTask Create(ref MotionSynthesizer synthesizer, Identifier<PoseSequence> sequences, Identifier<SamplingTime> samplingTime, Identifier<Trajectory> trajectory, Identifier<TimeIndex> closestMatch)
        {
            MemoryArray<PoseSequence> sequencesArray = synthesizer.GetArray<PoseSequence>(sequences);
            if (!sequencesArray.IsValid)
            {
                throw new Exception("Error in ReduceTask : invalid sequences identifier");
            }

            if (sequencesArray.Length == 0)
            {
                throw new Exception("Error in ReduceTask : no sequence matching the query were found in the binary");
            }

            if (samplingTime.IsValid)
            {
                for (int i = 0; i < sequencesArray.Length; ++i)
                {
                    ref Binary.Interval interval = ref synthesizer.Binary.GetInterval(sequencesArray[i].intervalIndex);
                    if (!interval.codeBookIndex.IsValid)
                    {
                        throw new NoMetricException(new MemoryRef<MotionSynthesizer>(ref synthesizer), interval.segmentIndex);
                    }
                }
            }

            return new ReduceTask
            {
                synthesizer = synthesizer.self,
                sequences = sequences,
                samplingTime = samplingTime,
                trajectory = trajectory,
                closestMatch = closestMatch,
                responsiveness = 0.6f,
                threshold = 0.0f
            };
        }

        class NoMetricException : Exception
        {
            public NoMetricException(MemoryRef<MotionSynthesizer> synthesizer, int segmentIndex)
            {
                m_Synthesizer = synthesizer;
                m_SegmentIndex = segmentIndex;
            }

            public override string Message
            {
                get
                {
                    ref Binary binary = ref m_Synthesizer.Ref.Binary;

                    ref var segment = ref binary.GetSegment(m_SegmentIndex);
                    string clipName = binary.GetString(segment.nameIndex);

                    return $"Segment from clip {clipName} at frames [{segment.source.FirstFrame},{segment.source.OnePastLastFrame - 1}] has no metric assigned, it cannot be processed by Reduce task";
                }
            }

            MemoryRef<MotionSynthesizer> m_Synthesizer;
            int m_SegmentIndex;
        }

        class SegmentTooShortException : Exception
        {
            public SegmentTooShortException(MemoryRef<MotionSynthesizer> synthesizer, int segmentIndex)
            {
                m_Synthesizer = synthesizer;
                m_SegmentIndex = segmentIndex;
            }

            public override string Message
            {
                get
                {
                    ref Binary binary = ref m_Synthesizer.Ref.Binary;

                    ref var segment = ref binary.GetSegment(m_SegmentIndex);
                    string clipName = binary.GetString(segment.nameIndex);

                    return $"Segment from clip {clipName} at frames [{segment.source.FirstFrame},{segment.source.OnePastLastFrame - 1}] is too short";
                }
            }

            MemoryRef<MotionSynthesizer> m_Synthesizer;
            int m_SegmentIndex;
        }
    }
}
