using System;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

using UnityEngine;

namespace Unity.Kinematica.Editor
{
    internal struct KMeans : IDisposable
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

            public static Settings Create()
            {
                return new Settings
                {
                    numIterations = 25,
                    numAttempts = 1,
                    seed = 1234
                };
            }

            public static Settings Default => Create();
        }

        //
        // Settings for KMeans clustering
        //

        public Settings settings;

        //
        // dimension of the vectors
        //

        public int d;

        //
        // number of centroids
        //

        public int k;

        //
        // centroids (k * d)
        //

        public NativeArray<float> centroids;

        public KMeans(int d, int k, Settings settings)
        {
            this.d = d;
            this.k = k;

            this.settings = settings;

            centroids = new NativeArray<float>(d * k, Allocator.Persistent);
        }

        public void Dispose()
        {
            centroids.Dispose();
        }

        public void Train(NativeSlice<float> x, int nx, int dsub, Action<ProgressFeedback> callback)
        {
            var distances = new NativeArray<distance>(nx, Allocator.Temp);

            var bestCentroids = new NativeArray<float>(d * k, Allocator.Temp);

            float minimumError = float.MaxValue;

            Debug.Assert(centroids.Length == d * k);

            for (int attempt = 0; attempt < numAttempts; attempt++)
            {
                //
                // initialize centroids with random points from the dataset
                //

                Debug.Assert(centroids.Length == d * k);

                using (var perm = new NativeArray<int>(nx, Allocator.Persistent))
                {
                    RandomPermutation(perm, randomSeed + 1 + attempt * 15486557);

                    for (int i = 0; i < k; ++i)
                    {
                        int index = perm[i % perm.Length];

                        for (int j = 0; j < d; ++j)
                        {
                            centroids[i * d + j] = x[index * d + j];
                        }
                    }
                }

                float error = 0.0f;

                for (int i = 0; i < numIterations; ++i)
                {
                    MeasureCentroids(distances, x, centroids);

                    error = 0.0f;
                    for (int j = 0; j < nx; j++)
                    {
                        error += distances[j].l2;
                    }

                    int nsplit =
                        UpdateCentroids(
                            x, centroids, distances, d, k, nx);

                    float basePercentage = (float)(i + 1) / (float)numIterations;
                    float factor = ImbalanceFactor(nx, k, distances);

                    callback(new ProgressFeedback
                    {
                        percentage = basePercentage / numAttempts,
                        info = $"Iteration {i} Objective={error:0.00} Imbalance={factor:0.00} NumSplits={nsplit}"
                    });
                }

                if (numAttempts > 1)
                {
                    if (error < minimumError)
                    {
                        minimumError = error;

                        centroids.CopyTo(bestCentroids);
                    }
                }
            }

            if (numAttempts > 1)
            {
                bestCentroids.CopyTo(centroids);
            }

            bestCentroids.Dispose();
            distances.Dispose();
        }

        int numIterations => settings.numIterations;

        int numAttempts => settings.numAttempts;

        int randomSeed => settings.seed;

        struct distance
        {
            public int index;
            public float l2;
        }

        unsafe void MeasureCentroids(NativeArray<distance> result, NativeSlice<float> x, NativeArray<float> centroids)
        {
            int nx = result.Length;

            distance* result_ptr = (distance*)NativeArrayUnsafeUtility.GetUnsafePtr(result);

            float* x_ptr = (float*)NativeSliceUnsafeUtility.GetUnsafePtr(x);
            float* y_ptr = (float*)NativeArrayUnsafeUtility.GetUnsafePtr(centroids);

            for (int i = 0; i < nx; i++)
            {
                result_ptr[i].index = -1;
                result_ptr[i].l2 = float.MaxValue;

                int id = i * d;

                for (int j = 0; j < k; j++)
                {
                    var jd = j * d;

                    float disij = 0.0f;
                    for (int k = 0; k < d; ++k)
                    {
                        float d = x_ptr[id + k] - y_ptr[jd + k];
                        disij += d * d;
                    }

                    if (disij < result_ptr[i].l2)
                    {
                        result_ptr[i].index = j;
                        result_ptr[i].l2 = disij;
                    }
                }
            }
        }

        //
        // Update stage for k-means.
        //
        // Compute centroids given assignment of vectors to centroids
        //
        // x         - training vectors, size n * d
        // centroids - centroid vectors, size k * d
        // assign    - nearest centroid for each training vector, size n
        //
        // returns number of split operations to fight empty clusters
        //

        unsafe int UpdateCentroids(NativeSlice<float> x, NativeArray<float> centroids, NativeArray<distance> assign, int d, int k, int n)
        {
            int* hassign = stackalloc int[k];
            for (int i = 0; i < k; ++i)
            {
                hassign[i] = 0;
            }

            Debug.Assert(centroids.Length == d * k);

            for (int i = 0; i < centroids.Length; ++i)
            {
                centroids[i] = 0.0f;
            }

            for (int i = 0; i < n; ++i)
            {
                int ci = assign[i].index;
                Debug.Assert(ci >= 0 && ci < k);
                hassign[ci]++;
                for (int j = 0; j < d; ++j)
                {
                    centroids[ci * d + j] += x[i * d + j];
                }
            }

            for (int ci = 0; ci < k; ci++)
            {
                var ni = hassign[ci];
                if (ni != 0)
                {
                    for (int j = 0; j < d; ++j)
                    {
                        centroids[ci * d + j] /= (float)ni;
                    }
                }
            }

            //
            // Take care of void clusters
            //

            int nsplit = 0;

            var random = new RandomGenerator(1234);

            for (int ci = 0; ci < k; ++ci)
            {
                //
                // need to redefine a centroid
                //

                if (hassign[ci] == 0)
                {
                    int cj = 0;
                    while (true)
                    {
                        //
                        // probability to pick this cluster for split
                        //

                        float p = (hassign[cj] - 1.0f) / (float)(n - k);
                        float r = random.Float();

                        if (r < p)
                        {
                            //
                            // found our cluster to be split
                            //
                            break;
                        }

                        cj = (cj + 1) % k;
                    }

                    for (int j = 0; j < d; ++j)
                    {
                        centroids[ci * d + j] = centroids[cj * d + j];
                    }

                    //
                    // small symmetric perturbation
                    //

                    float eps = 1.0f / 1024.0f;
                    for (int j = 0; j < d; ++j)
                    {
                        if (j % 2 == 0)
                        {
                            centroids[ci * d + j] *= 1 + eps;
                            centroids[cj * d + j] *= 1 - eps;
                        }
                        else
                        {
                            centroids[ci * d + j] *= 1 - eps;
                            centroids[cj * d + j] *= 1 + eps;
                        }
                    }

                    //
                    // assume even split of the cluster
                    //

                    hassign[ci] = hassign[cj] / 2;
                    hassign[cj] -= hassign[ci];

                    nsplit++;
                }
            }

            return nsplit;
        }

        unsafe float ImbalanceFactor(int n, int k, NativeArray<distance> assign)
        {
            int* histogram = stackalloc int[k];
            for (int i = 0; i < k; ++i)
            {
                histogram[i] = 0;
            }

            for (int i = 0; i < n; i++)
            {
                histogram[assign[i].index]++;
            }

            float total = 0.0f;
            float result = 0.0f;

            for (int i = 0; i < k; i++)
            {
                total += histogram[i];
                result += histogram[i] * (float)histogram[i];
            }
            result = result * k / (total * total);

            return result;
        }

        void RandomPermutation(NativeArray<int> perm, int seed)
        {
            int n = perm.Length;

            for (int i = 0; i < n; i++)
            {
                perm[i] = i;
            }

            var random = new RandomGenerator(seed);

            for (int i = 0; i + 1 < n; i++)
            {
                int i2 = i + random.Integer(n - i);
                int t = perm[i];
                perm[i] = perm[i2];
                perm[i2] = t;
            }
        }
    }
}
