using System.IO;

using UnityEngine.Assertions;

using Unity.Collections.LowLevel.Unsafe;
using Unity.SnapshotDebugger;

namespace Unity.Kinematica
{
    /// <summary>
    /// A memory array is very similar in nature to a NativeArray.
    /// It stores a reference to an array of data elements and allows
    /// for retrieval of inidividual elements. This is a workaround
    /// for the inability to nest NativeArrays.
    /// </summary>
    public unsafe struct MemoryArray<T> where T : struct
    {
        [NativeDisableUnsafePtrRestriction]
        internal void* ptr;
        internal int length;

        public MemoryArray(T[] array)
        {
            if (array == null || array.Length == 0)
            {
                ptr = null;
                length = 0;
                return;
            }

            ptr = UnsafeUtility.AddressOf<T>(ref array[0]);
            length = array.Length;
        }

        /// <summary>
        /// Determines the amount of elements stored in the memory array.
        /// </summary>
        public int Length => length;

        /// <summary>
        /// Allows to retrieve an individual array element.
        /// The element is returned a ref value.
        /// </summary>
        public ref T this[int index]
        {
            get
            {
                return ref UnsafeUtilityEx.ArrayElementAsRef<T>(ptr, index);
            }
        }

        /// <summary>
        /// Determines if the given memory array is valid or not.
        /// </summary>
        /// <returns>True if the memory array is valid; false otherwise.</returns>
        public bool IsValid
        {
            get { return ptr != null; }
        }

        /// <summary>
        /// Invalid memory array.
        /// </summary>
        public static MemoryArray<T> Null => new MemoryArray<T>();

        internal void WriteToStream(BinaryWriter writer)
        {
            var numBytes = Length * UnsafeUtility.SizeOf<T>();
            var buffer = new byte[numBytes];
            fixed(byte* dst = &buffer[0])
            {
                UnsafeUtility.MemCpy(
                    dst, ptr, buffer.Length);
            }
            writer.Write(buffer);
        }

        internal void ReadFromStream(BinaryReader reader)
        {
            var numBytes = Length * UnsafeUtility.SizeOf<T>();
            var buffer = reader.ReadBytes(numBytes);
            fixed(byte* src = &buffer[0])
            {
                UnsafeUtility.MemCpy(
                    ptr, src, buffer.Length);
            }
        }

        /// <summary>
        /// Reads a memory array from the buffer passed as argument.
        /// </summary>
        /// <param name="buffer">The buffer that the memory array should be read from.</param>
        public void ReadFromStream(Buffer buffer)
        {
            var numBytes = Length * UnsafeUtility.SizeOf<T>();
            var byteArray = buffer.ReadBytes(numBytes);
            fixed(byte* src = &byteArray[0])
            {
                UnsafeUtility.MemCpy(
                    ptr, src, byteArray.Length);
            }
        }

        /// <summary>
        /// Stores a memory array in the buffer passed as argument.
        /// </summary>
        /// <param name="buffer">The buffer that the memory array should be written to.</param>
        public void WriteToStream(Buffer buffer)
        {
            var numBytes = Length * UnsafeUtility.SizeOf<T>();
            var byteArray = new byte[numBytes];
            fixed(byte* dst = &byteArray[0])
            {
                UnsafeUtility.MemCpy(
                    dst, ptr, byteArray.Length);
            }
            buffer.Write(byteArray);
        }

        /// <summary>
        /// Copies the memory array to the target memory array passed as argument.
        /// </summary>
        /// <param name="target">The target memory array that this memory array should be copied to.</param>
        public void CopyTo(ref MemoryArray<T> target)
        {
            Assert.IsTrue(target.Length == length);

            UnsafeUtility.MemCpy(
                target.ptr, ptr, Length * UnsafeUtility.SizeOf<T>());
        }
    }
}
