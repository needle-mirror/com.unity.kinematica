using System;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine.Assertions;

namespace Unity.Kinematica
{
    //
    // Motion synthesizer binary resource
    //

    public partial struct Binary
    {
        /// <summary>
        /// A trajectory fragment contains information necessary to perform
        /// similarity calculations between trajectories.
        /// </summary>
        /// <remarks>
        /// A trajectory is simply a sequence for root joint transforms
        /// forming a continuous path. Trajectories can either be expressed
        /// in world space according to the original animation data or relative
        /// to a sampling time.
        /// <para>
        /// A trajectory fragment can be constructed from any animation pose
        /// contained in the motion library. It requires a metric in order
        /// to extract the relevant information. The similarity
        /// calculation uses a weighted sum of features that includes
        /// root velocity, forward direction and optionally root displacements.
        /// </para>
        /// <para>
        /// The main purpose of this class is to:
        /// * Create trajectory fragments from poses in the motion library.
        /// * Perform similarity calculation between trajectory fragments.
        /// </para>
        /// </remarks>
        public struct TrajectoryFragment : IDisposable
        {
            /// <summary>
            /// Denotes the metric index that this trajectory fragment belongs to.
            /// </summary>
            public MetricIndex metricIndex;

            /// <summary>
            /// Denotes the sampling time that this trajectory fragment was generated from.
            /// </summary>
            public SamplingTime samplingTime;

            /// <summary>
            /// Denotes the feature array of this trajectory fragment.
            /// </summary>
            public NativeArray<float3> array;

            internal TrajectoryFragment(ref Binary binary, MetricIndex metricIndex)
            {
                samplingTime = SamplingTime.Invalid;

                this.metricIndex = metricIndex;

                var numFeatures = GetNumFeatures(ref binary, metricIndex);

                array = new NativeArray<float3>(numFeatures, Allocator.Temp);
            }

            internal static int GetNumFeatures(ref Binary binary, MetricIndex metricIndex)
            {
                var numFeatures = 0;

                ref var metric = ref binary.GetMetric(metricIndex);

                if (metric.trajectoryDisplacements)
                {
                    numFeatures += metric.numTrajectorySamples;
                }

                numFeatures += GetNumQuantizedFeatures(ref binary, metricIndex);
                numFeatures += GetNumNormalizedFeatures(ref binary, metricIndex);

                return numFeatures;
            }

            internal static int GetNumQuantizedFeatures(ref Binary binary, MetricIndex metricIndex)
            {
                return binary.GetMetric(metricIndex).numTrajectorySamples + 1;
            }

            internal static int GetNumNormalizedFeatures(ref Binary binary, MetricIndex metricIndex)
            {
                return GetNumQuantizedFeatures(ref binary, metricIndex);
            }

            /// <summary>
            /// Determines whether or not this trajectory fragment is valid.
            /// </summary>
            public bool IsValid => array.IsCreated;

            /// <summary>
            /// Disposes the underlying feature array.
            /// </summary>
            public void Dispose()
            {
                array.Dispose();
            }

            /// <summary>
            /// Represents an invalid trajectory fragment.
            /// </summary>
            public static TrajectoryFragment Invalid
            {
                get => new TrajectoryFragment();
            }

            /// <summary>
            /// Determines whether a two trajectory fragments are equal.
            /// </summary>
            /// <param name="instance">The trajectory fragment to compare against the current trajectory fragment.</param>
            /// <param name="eps">Tolerance value to indicate by how much the result can diverge before being considered unequal.</param>
            /// <returns>True if the trajectory fragment passed as argument is equal to the current trajectory fragment; otherwise, false.</returns>
            public bool Equals(TrajectoryFragment instance, float eps)
            {
                if (length != instance.length)
                {
                    return false;
                }

                for (int i = 0; i < length; ++i)
                {
                    if (!Missing.equalEps(this[i], instance[i], eps))
                    {
                        return false;
                    }
                }

                return true;
            }

            /// <summary>
            /// Denotes the size of the feature array.
            /// </summary>
            public int length => array.Length;

            /// <summary>
            /// Gives access to the individual pose fragment features.
            /// </summary>
            public float3 this[int index] => array[index];

            internal static TrajectoryFragment Create(ref Binary binary, MetricIndex metricIndex)
            {
                return new TrajectoryFragment(ref binary, metricIndex);
            }

            internal struct Factory
            {
                public TrajectoryFragment instance;

                public Factory(ref Binary binary, MetricIndex metricIndex)
                {
                    instance = TrajectoryFragment.Create(ref binary, metricIndex);
                }

                public static Factory Create(ref Binary binary, MetricIndex metricIndex)
                {
                    return new Factory(ref binary, metricIndex);
                }

                public static TrajectoryFragment Create(ref Binary binary, MetricIndex metricIndex, SamplingTime samplingTime)
                {
                    var factory = Create(ref binary, metricIndex);

                    factory.GenerateTrajectoryFeatures(ref binary, samplingTime);

                    factory.instance.samplingTime = samplingTime;

                    return factory.instance;
                }

                public static TrajectoryFragment Create(ref Binary binary, MetricIndex metricIndex, MemoryArray<AffineTransform> trajectory)
                {
                    var factory = Create(ref binary, metricIndex);

                    factory.GenerateTrajectoryFeatures(ref binary, trajectory);

                    factory.instance.samplingTime = SamplingTime.Invalid;

                    return factory.instance;
                }

                void GenerateTrajectoryFeatures(ref Binary binary, SamplingTime samplingTime)
                {
                    var timeHorizon = binary.TimeHorizon;

                    ref var metric = ref binary.GetMetric(instance.metricIndex);

                    var numTrajectorySamples = metric.numTrajectorySamples;

                    Assert.IsTrue(numTrajectorySamples > 0);

                    var startTimeInSeconds =
                        math.min(timeHorizon, timeHorizon *
                            metric.trajectorySampleRange);

                    var advanceInSeconds =
                        (timeHorizon - startTimeInSeconds) /
                        numTrajectorySamples;

                    //
                    // Sample velocities and forward directions
                    //

                    float deltaTime = math.rcp(binary.SampleRate);

                    var trajectoryTimeSpan = deltaTime * 2.0f;

                    var relativeTime = startTimeInSeconds;

                    var stride = numTrajectorySamples + 1;

                    for (int i = 0; i <= numTrajectorySamples; ++i)
                    {
                        var rootVelocity =
                            binary.GetTrajectoryVelocity(
                                samplingTime, relativeTime, trajectoryTimeSpan);

                        var rootForward =
                            binary.GetTrajectoryForward(
                                samplingTime, relativeTime, trajectoryTimeSpan);

                        WriteFloat3(rootVelocity, stride * 0 + i);
                        WriteFloat3(rootForward, stride * 1 + i);

                        relativeTime =
                            math.min(timeHorizon,
                                relativeTime + advanceInSeconds);
                    }

                    //
                    // Sample root displacement
                    //

                    if (metric.trajectoryDisplacements)
                    {
                        relativeTime = startTimeInSeconds;

                        AffineTransform referenceTransform =
                            binary.GetTrajectoryTransform(samplingTime);

                        AffineTransform previousRootTransform =
                            referenceTransform.inverseTimes(
                                binary.GetTrajectoryTransform(
                                    binary.Advance(samplingTime, relativeTime)));

                        for (int i = 0; i < numTrajectorySamples; ++i)
                        {
                            relativeTime =
                                math.min(timeHorizon,
                                    relativeTime + advanceInSeconds);

                            AffineTransform rootTransform =
                                referenceTransform.inverseTimes(
                                    binary.GetTrajectoryTransform(
                                        binary.Advance(samplingTime, relativeTime)));

                            WriteFloat3(rootTransform.t - previousRootTransform.t, stride * 2 + i);

                            previousRootTransform = rootTransform;
                        }
                    }
                }

                void GenerateTrajectoryFeatures(ref Binary binary, MemoryArray<AffineTransform> trajectory)
                {
                    var timeHorizon = binary.TimeHorizon;

                    var sampleRate = binary.SampleRate;

                    int trajectoryLength = trajectory.Length;

                    int halfTrajectoryLength = trajectoryLength / 2;

                    AffineTransform GetRootTransform(float sampleTimeInSeconds)
                    {
                        var fraction = sampleTimeInSeconds / timeHorizon;

                        var truncated =
                            math.floor(
                                fraction * halfTrajectoryLength);

                        int sampleKeyFrame =
                            halfTrajectoryLength +
                            Missing.truncToInt(truncated);

                        var numFramesMinusOne = trajectoryLength - 1;

                        sampleKeyFrame = math.clamp(sampleKeyFrame, 0, numFramesMinusOne);

                        if (sampleKeyFrame >= numFramesMinusOne)
                        {
                            return trajectory[numFramesMinusOne];
                        }

                        float fractionalKeyFrame =
                            halfTrajectoryLength +
                            fraction * halfTrajectoryLength;
                        float theta = math.saturate(fractionalKeyFrame - sampleKeyFrame);

                        if (theta <= Missing.epsilon)
                        {
                            return trajectory[sampleKeyFrame];
                        }

                        AffineTransform t0 = trajectory[sampleKeyFrame + 0];
                        AffineTransform t1 = trajectory[sampleKeyFrame + 1];

                        return Missing.lerp(t0, t1, theta);
                    }

                    float3 GetRootVelocity(float sampleTimeInSeconds)
                    {
                        var deltaTime = math.rcp(sampleRate);

                        var futureSampleTimeInSeconds =
                            math.min(sampleTimeInSeconds + deltaTime, timeHorizon);

                        sampleTimeInSeconds =
                            math.max(futureSampleTimeInSeconds - deltaTime, -timeHorizon);

                        var t1 = GetRootTransform(futureSampleTimeInSeconds).t;
                        var t0 = GetRootTransform(sampleTimeInSeconds).t;

                        return (t1 - t0) / deltaTime;
                    }

                    float3 GetRootForward(float timeInSeconds)
                    {
                        return Missing.zaxis(GetRootTransform(timeInSeconds).q);
                    }

                    //
                    // Sample velocities and forward directions
                    //

                    ref var metric = ref binary.GetMetric(instance.metricIndex);

                    var numTrajectorySamples = metric.numTrajectorySamples;

                    Assert.IsTrue(numTrajectorySamples > 0);

                    var startTimeInSeconds =
                        math.min(timeHorizon, timeHorizon *
                            metric.trajectorySampleRange);

                    var advanceInSeconds =
                        (timeHorizon - startTimeInSeconds) /
                        numTrajectorySamples;

                    var relativeTime = startTimeInSeconds;

                    var stride = numTrajectorySamples + 1;

                    for (int i = 0; i <= numTrajectorySamples; ++i)
                    {
                        var rootVelocity = GetRootVelocity(relativeTime);
                        var rootForward = GetRootForward(relativeTime);

                        WriteFloat3(rootVelocity, stride * 0 + i);
                        WriteFloat3(rootForward, stride * 1 + i);

                        relativeTime =
                            math.min(timeHorizon,
                                relativeTime + advanceInSeconds);
                    }

                    //
                    // Sample root displacement
                    //

                    if (metric.trajectoryDisplacements)
                    {
                        relativeTime = startTimeInSeconds;

                        var previousRootTransform = GetRootTransform(relativeTime);

                        for (int i = 0; i < numTrajectorySamples; ++i)
                        {
                            relativeTime =
                                math.min(timeHorizon,
                                    relativeTime + advanceInSeconds);

                            var rootTransform = GetRootTransform(relativeTime);

                            var displacement = rootTransform.t - previousRootTransform.t;

                            WriteFloat3(displacement, stride * 2 + i);

                            previousRootTransform = rootTransform;
                        }
                    }
                }

                void WriteFloat3(float3 value, int index)
                {
                    instance.array[index] = value;
                }
            }

            internal static TrajectoryFragment Create(ref Binary binary, MetricIndex metricIndex, SamplingTime samplingTime)
            {
                return Factory.Create(ref binary, metricIndex, samplingTime);
            }

            internal static TrajectoryFragment Create(ref Binary binary, MetricIndex metricIndex, MemoryArray<AffineTransform> trajectory)
            {
                return Factory.Create(ref binary, metricIndex, trajectory);
            }
        }

        /// <summary>
        /// Reconstructs a trajectory fragment for a given sampling time in the runtime asset.
        /// </summary>
        /// <remarks>
        /// The runtime asset building process generates trajectory fragments for any segment
        /// for which a metric has been defined. These trajectory fragments are stored inside
        /// the runtime asset in a compressed format.
        /// <para>
        /// Trajectory fragments can either be constructed from scratch or can be reconstructed
        /// from the information contained in the runtime asset. Reconstruction is faster
        /// than creating a trajectory fragment but is subject to loss of precision.
        /// </para>
        /// </remarks>
        /// <param name="samplingTime">The sampling time from which the trajectory fragment should be reconstructed.</param>
        /// <returns>The resulting trajectory fragment.</returns>
        public TrajectoryFragment ReconstructTrajectoryFragment(SamplingTime samplingTime)
        {
            int numCodeBooks = this.numCodeBooks;

            for (int i = 0; i < numCodeBooks; ++i)
            {
                ref var codeBook = ref GetCodeBook(i);

                int numIntervals = codeBook.intervals.Length;

                int fragmentIndex = 0;

                for (int j = 0; j < numIntervals; ++j)
                {
                    var intervalIndex = codeBook.intervals[j];

                    ref var interval = ref GetInterval(intervalIndex);

                    if (interval.Contains(samplingTime.timeIndex))
                    {
                        var relativeIndex =
                            samplingTime.frameIndex - interval.firstFrame;

                        var fragment =
                            TrajectoryFragment.Create(
                                ref this, codeBook.metricIndex);

                        fragment.samplingTime = samplingTime;

                        var index = fragmentIndex + relativeIndex;

                        Assert.IsTrue(index < codeBook.numFragments);

                        fragment.metricIndex = codeBook.metricIndex;

                        codeBook.trajectories.DecodeFeatures(fragment.array, index);

                        codeBook.trajectories.InverseNormalize(fragment.array);

                        return fragment;
                    }

                    fragmentIndex += interval.numFrames;
                }
            }

            return TrajectoryFragment.Invalid;
        }

        /// <summary>
        /// Creates a trajectory fragment from a sampling time.
        /// </summary>
        /// <param name="metricIndex">The metric defining the trajectory fragment layout.</param>
        /// <param name="samplingTime">The sampling time that the trajectory fragment should be created for.</param>
        /// <returns>The resulting trajectory fragment.</returns>
        public TrajectoryFragment CreateTrajectoryFragment(MetricIndex metricIndex, SamplingTime samplingTime)
        {
            return TrajectoryFragment.Create(ref this, metricIndex, samplingTime);
        }

        /// <summary>
        /// Creates a trajectory fragment from sequence of root transforms.
        /// </summary>
        /// <remarks>
        /// The root transforms passed as argument are expected to be relative to the world origin,
        /// i.e. in character space.
        /// </remarks>
        /// <param name="metricIndex">The metric defining the trajectory fragment layout.</param>
        /// <param name="trajectory">Sequence of root transforms in character space.</param>
        /// <returns>The resulting trajectory fragment.</returns>
        public TrajectoryFragment CreateTrajectoryFragment(MetricIndex metricIndex, MemoryArray<AffineTransform> trajectory)
        {
            return TrajectoryFragment.Create(ref this, metricIndex, trajectory);
        }
    }
}
