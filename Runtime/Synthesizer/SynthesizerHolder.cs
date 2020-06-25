using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Assertions;
using System.Collections.Generic;

namespace Unity.Kinematica
{
    /// <summary>
    /// This struct hold and setup the synthesizer and its dependencies.
    /// The synthesizer cannot hold directly its own dependencies because they are managed code and synthesizer need to be unmanaged
    /// in order to be used by Burst
    /// </summary>
    /// <seealso cref="MotionSynthesizer"/>
    public partial struct MotionSynthesizerHolder
    {
        /// <summary>
        /// Create uninitialized synthesizer holder, which can still be checked for validity by checking <code>IsValid</code> property
        /// </summary>
        /// <returns></returns>
        public static MotionSynthesizerHolder CreateInvalid()
        {
            return new MotionSynthesizerHolder()
            {
                synthesizer = MemoryHeader<MotionSynthesizer>.CreateInvalid(),
                memoryChunk = MemoryHeader<MemoryChunk>.CreateInvalid(),
#if UNITY_EDITOR
                memoryChunkShadow = MemoryHeader<MemoryChunk>.CreateInvalid()
#endif
            };
        }

        /// <summary>
        /// Create synthesizer holder
        /// </summary>
        /// <param name="transform">Start root transform</param>
        /// <param name="resource">Reference to Kinematica binary</param>
        /// <param name="blendDuration">Blend duration between segments in seconds</param>
        /// <param name="capacity">Initial alllocated memory in bytes for the task graph data</param>
        /// <returns></returns>
        public static MotionSynthesizerHolder Create(Transform transform, BinaryReference resource, float blendDuration, int capacity = 1024)
        {
            return new MotionSynthesizerHolder(
                AffineTransform.Create(transform.position, transform.rotation),
                resource,
                blendDuration,
                capacity);
        }

        /// <summary>
        /// Return true if the synthesizer is initialized and ready to be used, false otherwise
        /// </summary>
        public bool IsValid => synthesizer.IsValid;

        /// <summary>
        /// Allows direct access to the underlying Kinematica runtime asset.
        /// </summary>
        public ref Binary Binary
        {
            get
            {
                Assert.IsTrue(IsValid);
                return ref synthesizer.Ref.Binary;
            }
        }

        /// <summary>
        /// Allows direct access to the motion synthesizer.
        /// </summary>
        /// <remarks>
        /// Most of Kinematica's API methods can be found in the
        /// motion synthesizer. API methods that are specific to the
        /// game object wrapper can be found on the Kinematica
        /// component directly.
        /// </remarks>
        public MemoryRef<MotionSynthesizer> Synthesizer => synthesizer;

        /// <summary>
        /// Dispose the internal buffers
        /// </summary>
        /// <remarks>
        /// This method releases all internally constructed objects and unregisters
        /// the Kinematica component from the snapshot debugger.
        /// </remarks>
        public void Dispose()
        {
            if (IsValid)
            {
                synthesizer.Dispose();

                if (memoryChunk.IsValid)
                {
                    memoryChunk.Ref.Dispose();
                }

                memoryChunk.Dispose();

#if UNITY_EDITOR
                if (memoryChunkShadow.IsValid)
                {
                    memoryChunkShadow.Ref.Dispose();
                }

                memoryChunkShadow.Dispose();
#endif
            }
        }

        /// <summary>
        /// To be called during the regular game object update loop.
        /// </summary>
        public void Update()
        {
            if (IsValid)
            {
                synthesizer.Ref.UpdateFrameCount(Time.frameCount);
            }
        }

        /// <summary>
        /// To be called during the regular OnEarlyUpdate loop.
        /// </summary>
        public void OnEarlyUpdate(bool rewind)
        {
#if UNITY_EDITOR
            if (IsValid)
            {
                synthesizer.Ref.immutable = rewind;
            }
#endif
        }

        MotionSynthesizerHolder(AffineTransform rootTransform, BinaryReference resource, float blendDuration, int capacity)
        {
            MemoryRequirements memoryRequirements = MemoryRequirements.Create(capacity);

            memoryChunk = MemoryChunk.Create(
                memoryRequirements, Allocator.Persistent);

#if UNITY_EDITOR
            memoryChunkShadow = MemoryChunk.Create(
                memoryRequirements, Allocator.Persistent);
#endif
            synthesizer = MotionSynthesizer.Create(
                resource, rootTransform, blendDuration);

            synthesizer.Ref.self = synthesizer;
            synthesizer.Ref.memoryChunk = memoryChunk;
#if UNITY_EDITOR
            synthesizer.Ref.memoryChunkShadow = memoryChunkShadow;
#endif

            var typeIndex =
                synthesizer.Ref.GetDataTypeIndex<RootTask>();

            MemoryIdentifier root = memoryChunk.Ref.Allocate(
                RootTask.Create(ref synthesizer.Ref),
                typeIndex, MemoryIdentifier.Invalid);

            synthesizer.Ref.GetRef<RootTask>(root).Ref.self = root;
        }

        MemoryHeader<MotionSynthesizer> synthesizer;

        MemoryHeader<MemoryChunk> memoryChunk;

#if UNITY_EDITOR
        MemoryHeader<MemoryChunk> memoryChunkShadow;
#endif
    }
}
