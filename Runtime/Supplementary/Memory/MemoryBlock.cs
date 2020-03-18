using System;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Assertions;

namespace Unity.Kinematica
{
    internal static class Memory
    {
        public static unsafe bool IsAligned(void* ptr, int alignment)
        {
            long value = (long)ptr;
            return (value & (--alignment)) == 0;
        }

        public static int Align(int value, int alignment)
        {
            Assert.IsTrue(alignment != 0);
            return (value + (alignment - 1)) & ~(alignment - 1);
        }

        public unsafe static void* AlignPtr(void* ptr, int alignment)
        {
            Assert.IsTrue(alignment != 0);
            long value = (long)ptr;
            value = (value + (alignment - 1)) & ~(alignment - 1);
            return (void*)value;
        }

        public unsafe static int AlignmentOffset(void* ptr, int alignment)
        {
            Assert.IsTrue(alignment != 0);
            long value = (long)ptr;
            value = (value + (alignment - 1)) & ~(alignment - 1);
            return (int)(value - (long)ptr);
        }
    }

    internal struct MemoryRequirements
    {
        public MemoryRequirements(int size, int alignment)
        {
            this.size = size;
            this.alignment = alignment;
        }

        public static MemoryRequirements Null
        {
            get
            {
                return new MemoryRequirements(0, 4);
            }
        }

        public static MemoryRequirements Create(int size, int alignment = 4)
        {
            return new MemoryRequirements(size, alignment);
        }

        public static MemoryRequirements Of<T>() where T : struct
        {
            IsUnmanagedAndBlittable<T>();

            return new MemoryRequirements(
                UnsafeUtility.SizeOf<T>(),
                UnsafeUtility.AlignOf<T>());
        }

        [BurstDiscard]
        public static void IsUnmanagedAndBlittable<T>() where T : struct
        {
            if (!UnsafeUtility.IsUnmanaged<T>() || !UnsafeUtility.IsBlittable<T>())
            {
                throw new InvalidOperationException(
                    $"{typeof(T)} used in MemoryRequirements<{typeof(T)}> must be unmanaged and blittable.");
            }
        }

        public static MemoryRequirements operator+(MemoryRequirements lhs, MemoryRequirements rhs)
        {
            Assert.IsTrue(lhs.Alignment != 0);
            Assert.IsTrue(rhs.Alignment != 0);

            int alignment = math.max(rhs.Alignment, lhs.Alignment);
            int size = Memory.Align(lhs.Size, rhs.Alignment) + rhs.Size;

            return new MemoryRequirements(size, alignment);
        }

        public static MemoryRequirements operator*(MemoryRequirements lhs, int count)
        {
            Assert.IsTrue(lhs.Alignment != 0);

            return new MemoryRequirements(
                count == 1 ? lhs.Size : (Memory.Align(
                    lhs.Size, lhs.Alignment) * count), lhs.Alignment);
        }

        public int Size => size;

        public int Alignment => alignment;

        internal int size;
        internal int alignment;
    }

    /// <summary>
    /// A memory reference allows a ref value to be stored in
    /// memory and later to be converted back into its ref value.
    /// </summary>
    public unsafe struct MemoryRef<T> where T : struct
    {
        [NativeDisableUnsafePtrRestriction]
        internal void* ptr;

        /// <summary>
        /// Constructs a new memory reference from a ref value.
        /// </summary>
        /// <param name="target">The ref value that this memory reference should be constructed for.</param>
        public MemoryRef(ref T target)
        {
            ptr = UnsafeUtility.AddressOf(ref target);
        }

        /// <summary>
        /// Construct a new memory reference from a memory header
        /// </summary>
        /// <param name="value">The memory header that this memory reference should be constructed for.</param>
        public static implicit operator MemoryRef<T>(MemoryHeader<T> value)
        {
            return Create(ref value.Ref);
        }

        /// <summary>
        /// Constructs a new memory reference from a ref value.
        /// </summary>
        /// <param name="target">The ref value that this memory reference should be constructed for.</param>
        public static MemoryRef<T> Create(ref T target)
        {
            return new MemoryRef<T>(ref target);
        }

        /// <summary>
        /// Retrieves the original ref value from the memory reference.
        /// </summary>
        public ref T Ref
        {
            get { return ref UnsafeUtilityEx.AsRef<T>((byte*)ptr); }
        }

        /// <summary>
        /// Determines if the given memory reference is valid or not.
        /// </summary>
        /// <returns>True if the memory reference is valid; false otherwise.</returns>
        public bool IsValid
        {
            get { return ptr != null; }
        }

        /// <summary>
        /// Invalid memory reference.
        /// </summary>
        public static MemoryRef<T> Null => new MemoryRef<T>();
    }

    /// <summary>
    /// A memory header maintains a chunk of memory that contains an arbitrary
    /// collection of nested value types. This is a workaround for the inability
    /// to nest NativeArrays.
    /// </summary>
    public unsafe struct MemoryHeader<T> : IDisposable where T : struct
    {
        [NativeDisableUnsafePtrRestriction]
        internal void* ptr;

        internal Allocator allocatorLabel;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal AtomicSafetyHandle safetyHandle;
        internal DisposeSentinel disposeSentinel;
#endif

        /// <summary>
        /// Constructs a new memory reference from a ref value.
        /// </summary>
        /// <param name="target">The ref value that this memory reference should be constructed for.</param>
        public static MemoryRef<T> Create(ref T target)
        {
            return new MemoryRef<T>(ref target);
        }

        /// <summary>
        /// Determines if the given memory header is valid or not.
        /// </summary>
        /// <returns>True if the memory header is valid; false otherwise.</returns>
        public bool IsValid
        {
            get { return ptr != null; }
        }

        /// <summary>
        /// Retrieves the original ref value from the memory header.
        /// </summary>
        public ref T Ref
        {
            get { return ref UnsafeUtilityEx.AsRef<T>((byte*)ptr); }
        }

        /// <summary>
        /// Disposes the underlying allocated memory of the memory header.
        /// </summary>
        public void Dispose()
        {
            if (!UnsafeUtility.IsValidAllocator(allocatorLabel))
            {
                throw new InvalidOperationException("The memory block can not be disposed because it was not allocated with a valid allocator.");
            }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            DisposeSentinel.Dispose(ref safetyHandle, ref disposeSentinel);
#endif

            UnsafeUtility.Free(ptr, allocatorLabel);

            ptr = null;
        }
    }

    internal unsafe struct MemoryBlock
    {
        public static MemoryBlock Create<T>(MemoryRequirements memoryRequirements, Allocator allocator, out MemoryHeader<T> memoryHeader, T value = default) where T : struct
        {
            var memoryBlock = new MemoryBlock(memoryRequirements, allocator);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            DisposeSentinel.Create(out memoryHeader.safetyHandle,
                out memoryHeader.disposeSentinel, 1, allocator);
#endif

            memoryHeader.allocatorLabel = allocator;

            Assert.IsTrue(Memory.IsAligned(memoryBlock.ptr, UnsafeUtility.AlignOf<T>()));

            memoryHeader.ptr = memoryBlock.ptr;

            memoryBlock.Increment(MemoryRequirements.Of<T>());

            memoryHeader.Ref = value;

            return memoryBlock;
        }

        public ref T Create<T>(T value = default) where T : struct
        {
            MemoryRequirements memoryRequirements = MemoryRequirements.Of<T>();

            ref T result = ref UnsafeUtilityEx.AsRef<T>(
                AlignAndIncrement(memoryRequirements));

            result = value;

            return ref result;
        }

        public MemoryRef<T> CreateRef<T>(T value = default) where T : struct
        {
            return MemoryRef<T>.Create(ref Create<T>(value));
        }

        public MemoryArray<T> CreateArray<T>(int numEntries) where T : struct
        {
            MemoryRequirements memoryRequirements = MemoryRequirements.Of<T>() * numEntries;

            return new MemoryArray<T>
            {
                ptr = AlignAndIncrement(memoryRequirements),
                length = numEntries
            };
        }

        public MemoryArray<T> CreateArray<T>(int numEntries, T value) where T : struct
        {
            MemoryArray<T> memoryArray = CreateArray<T>(numEntries);

            for (int i = 0; i < numEntries; ++i)
            {
                memoryArray[i] = value;
            }

            return memoryArray;
        }

        MemoryBlock(MemoryRequirements memoryRequirements, Allocator allocator)
        {
            Allocate(memoryRequirements, allocator, out this);
        }

        static void Allocate(MemoryRequirements memoryRequirements, Allocator allocator, out MemoryBlock result)
        {
            // Native allocation is only valid for Temp, Job and Persistent.
            if (allocator <= Allocator.None)
                throw new ArgumentException(
                    "Allocator must be Temp, TempJob or Persistent", nameof(allocator));

            // Make sure we cannot allocate more than int.MaxValue (2,147,483,647 bytes)
            // because the underlying UnsafeUtility.Malloc is expecting a int.
            // TODO: change UnsafeUtility.Malloc to accept a UIntPtr length instead to match C++ API
            if (memoryRequirements.Size > int.MaxValue)
                throw new ArgumentOutOfRangeException($"Memory requirements cannot exceed {int.MaxValue} bytes");

            result = default;
            result.ptr = UnsafeUtility.Malloc(
                memoryRequirements.Size, memoryRequirements.Alignment, allocator);
            result.memoryRequirements = memoryRequirements;
        }

        void Increment(int size)
        {
            Assert.IsTrue(size <= memoryRequirements.Size);
            ptr = (void*)(((byte*)ptr) + size);
            memoryRequirements.size -= size;
        }

        void Increment(MemoryRequirements memoryRequirements)
        {
            Assert.IsTrue(memoryRequirements.Alignment != 0);
            Assert.IsTrue(Memory.IsAligned(ptr, memoryRequirements.Alignment));
            Increment(memoryRequirements.Size);
        }

        void Align(MemoryRequirements memoryRequirements)
        {
            Align(memoryRequirements.Alignment);
        }

        void Align(int alignment)
        {
            Assert.IsTrue(alignment != 0);
            int increment =
                Memory.AlignmentOffset(ptr, alignment);
            Increment(increment);
        }

        void* AlignAndIncrement(MemoryRequirements memoryRequirements)
        {
            Align(memoryRequirements);
            void* result = ptr;
            Increment(memoryRequirements);
            return result;
        }

        public bool IsComplete => Remaining == 0;

        public int Remaining => memoryRequirements.Size;

        [NativeDisableUnsafePtrRestriction]
        void* ptr;

        MemoryRequirements memoryRequirements;
    }
}
