using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Assertions;
using Unity.SnapshotDebugger;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Kinematica
{
    internal struct TransformBuffer
    {
        public static MemoryHeader<TransformBuffer> Create(int numTransforms, Allocator allocator)
        {
            var memoryRequirements = MemoryRequirements.Of<TransformBuffer>();

            memoryRequirements += GetMemoryRequirements(numTransforms);

            MemoryHeader<TransformBuffer> transformBuffer;

            var memoryBlock = MemoryBlock.Create(
                memoryRequirements, allocator, out transformBuffer);

            transformBuffer.Ref.Construct(
                ref memoryBlock, numTransforms);

            Assert.IsTrue(memoryBlock.IsComplete);

            return transformBuffer;
        }

        internal unsafe void CopyFrom(ref TransformBuffer transformBuffer)
        {
            Assert.IsTrue(transformBuffer.transforms.Length == transforms.Length);

            var numBytes = Length * UnsafeUtility.SizeOf<AffineTransform>();

            UnsafeUtility.MemCpy(transforms.ptr, transformBuffer.transforms.ptr, numBytes);
        }

        internal void Construct(ref MemoryBlock memoryBlock, int numTransforms)
        {
            transforms = memoryBlock.CreateArray(numTransforms, AffineTransform.identity);
        }

        public int Length => transforms.Length;

        public AffineTransform this[int index]
        {
            get { return transforms[index]; }
            set { transforms[index] = value; }
        }

        public void WriteToStream(Buffer buffer)
        {
            transforms.WriteToStream(buffer);
        }

        public void ReadFromStream(Buffer buffer)
        {
            transforms.ReadFromStream(buffer);
        }

        public static MemoryRequirements GetMemoryRequirements(int numTransforms)
        {
            return MemoryRequirements.Of<AffineTransform>() * numTransforms;
        }

        public MemoryArray<AffineTransform> transforms;
    }
}
