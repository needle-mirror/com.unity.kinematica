using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;

namespace Unity.Kinematica.Editor
{
    internal struct ProgressFeedback
    {
        public float percentage;
        public string info;
    }

    internal class ProductQuantizer : IDisposable
    {
        public struct Settings
        {
            //
            // Number of clustering attempts
            //

            public int numAttempts;

            //
            // Number of clustering iterations
            //

            public int numIterations;

            //
            // seed for the random number generator
            //

            public int seed;

            //
            // minimum and maximum samples per centroid
            //

            public int minimumNumberSamples;
            public int maximumNumberSamples;

            public static Settings Create()
            {
                return new Settings
                {
                    numIterations = 25,
                    minimumNumberSamples = 32,
                    maximumNumberSamples = 256,
                    numAttempts = 1,
                    seed = 1234
                };
            }

            public static Settings Default => Create();
        }

        //
        // Size of the input vectors
        //

        int d;

        //
        // Number of sub-quantizers
        //

        int M;

        //
        // Number of bits per quantization index
        //

        int numBits;

        //
        // dimensionality of each sub-vector
        //

        int dsub;

        //
        // byte per indexed vector
        //

        int codeSize;

        //
        // number of centroids for each sub-quantizer
        //

        int ksub;

        //
        // Settings used during clustering
        //

        Settings settings;

        //
        // Centroid table, size M * ksub * dsub
        //

        public NativeArray<float> centroids;

        //
        // Returns the centroids associated with sub-vector m
        //

        public NativeSlice<float> GetCentroids(int m)
        {
            return new NativeSlice<float>(
                centroids, m * ksub * dsub, ksub * dsub);
        }

        //
        // d - dimensionality of the input vectors
        // M - number of sub-quantizers
        //

        public ProductQuantizer(int d, int M, Settings settings)
        {
            this.d = d;
            this.M = M;

            numBits = 8;

            Debug.Assert(d % M == 0);

            dsub = d / M;

            int numBytesPerIndex = (numBits + 7) / 8;
            codeSize = numBytesPerIndex * M;

            ksub = 1 << numBits;

            centroids =
                new NativeArray<float>(
                    d * ksub, Allocator.Persistent);

            this.settings = settings;
        }

        public void Dispose()
        {
            centroids.Dispose();
        }

        //
        // Train the product quantizer on a set of points.
        //

        public void Train(NativeSlice<float3>[] samples, Action<ProgressFeedback> callback)
        {
            //
            // TODO: Variable bitrate encoding.
            // TODO: Use Jobs to train slices in parallel,
            //       requires alternative random generator.
            //

            var numInputSamples = samples.Length;

            var numTrainingSamples = math.clamp(
                numInputSamples, settings.minimumNumberSamples * ksub,
                settings.maximumNumberSamples * ksub);

            var permutation = new NativeArray<int>(numTrainingSamples, Allocator.Temp);

            Assert.IsTrue(permutation.Length == numTrainingSamples);

            var random = new RandomGenerator(settings.seed);

            if ((numTrainingSamples < numInputSamples) || (numTrainingSamples > numInputSamples))
            {
                for (int i = 0; i < numTrainingSamples; i++)
                {
                    permutation[i] = random.Integer(numInputSamples);
                }
            }
            else
            {
                for (int i = 0; i < numTrainingSamples; i++)
                {
                    permutation[i] = i;
                }
            }

            for (int i = 0; i + 1 < numTrainingSamples; i++)
            {
                int i2 = i + random.Integer(numTrainingSamples - i);
                int t = permutation[i];

                permutation[i] = permutation[i2];
                permutation[i2] = t;
            }

            var slice = new NativeArray<float>(numTrainingSamples * dsub, Allocator.Temp);

            //
            // Loop over features (M)
            //

            for (int m = 0; m < M; m++)
            {
                //
                // Prepare feature slice for all samples (n)
                //

                int writeOffset = 0;

                unsafe
                {
                    for (int i = 0; i < numTrainingSamples; i++)
                    {
                        int sampleIndex = permutation[i];

                        Assert.IsTrue(samples[sampleIndex].Length == M);

                        float* x = (float*)NativeSliceUnsafeUtility.GetUnsafePtr(samples[sampleIndex]);

                        int readOffset = m * dsub;

                        for (int j = 0; j < dsub; ++j)
                        {
                            slice[writeOffset++] = x[readOffset++];
                        }
                    }
                }

                Assert.IsTrue(writeOffset == slice.Length);

                var kms = KMeans.Settings.Default;

                kms.numIterations = settings.numIterations;
                kms.numAttempts = settings.numAttempts;
                kms.seed = settings.seed;

                var kmeans = new KMeans(dsub, ksub, kms);

                Assert.IsTrue(numTrainingSamples >= settings.minimumNumberSamples * ksub);
                Assert.IsTrue(numTrainingSamples <= settings.maximumNumberSamples * ksub);

                kmeans.Train(slice, numTrainingSamples, 3, feedback =>
                {
                    float slicePercentage = 1.0f / M;
                    float currentPercentage = feedback.percentage * slicePercentage;
                    float completedPercentage = m * slicePercentage;

                    callback(new ProgressFeedback
                    {
                        percentage = currentPercentage + completedPercentage,
                        info = $"Training {m}/{M} " + feedback.info
                    });
                });

                int numFloats = ksub * dsub;
                int index = m * numFloats;

                Assert.IsTrue(kmeans.centroids.Length == numFloats);

                for (int i = 0; i < numFloats; ++i)
                {
                    centroids[index + i] = kmeans.centroids[i];
                }

                kmeans.Dispose();
            }

            permutation.Dispose();
            slice.Dispose();
        }

        //
        // Quantize single or multiple vectors with the product quantizer
        //

        unsafe void ComputeCode(float* x_ptr, byte* code_ptr)
        {
            float* distances = stackalloc float[ksub];

            float* y_ptr = (float*)NativeArrayUnsafeUtility.GetUnsafePtr(centroids);

            for (int m = 0; m < M; m++)
            {
                int index = -1;
                float minimumDistance = float.MaxValue;

                int mdsub = m * dsub;
                int mksubdsub = m * ksub * dsub;

                for (int i = 0; i < ksub; ++i)
                {
                    int idsub = i * dsub;

                    float l2square = 0.0f;
                    for (int j = 0; j < dsub; j++)
                    {
                        float d = x_ptr[mdsub + j] - y_ptr[mksubdsub + idsub + j];
                        l2square += d * d;
                    }

                    distances[i] = l2square;
                }

                //
                // Find best centroid
                //

                for (int i = 0; i < ksub; i++)
                {
                    float distance = distances[i];
                    if (distance < minimumDistance)
                    {
                        minimumDistance = distance;
                        index = i;
                    }
                }

                code_ptr[m] = (byte)index;
            }
        }

        public unsafe void ComputeCodes(NativeSlice<float3>[] samples, NativeSlice<byte> codes, Action<ProgressFeedback> callback)
        {
            byte* code_ptr = (byte*)NativeSliceUnsafeUtility.GetUnsafePtr(codes);

            int numSamples = samples.Length;

            for (int i = 0; i < numSamples; i++)
            {
                float* ptr = (float*)NativeSliceUnsafeUtility.GetUnsafePtr(samples[i]);

                ComputeCode(ptr, code_ptr + i * codeSize);

                callback(new ProgressFeedback
                {
                    percentage = (float)i / (float)numSamples,
                    info = "Encoding fragments"
                });
            }
        }
    }
}
