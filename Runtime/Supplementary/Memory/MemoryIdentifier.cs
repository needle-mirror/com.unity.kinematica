using System;
using Unity.Collections;
using UnityEngine.Assertions;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Kinematica
{
    /// <summary>
    /// Memory identifiers are used to uniquely identify data types and tasks
    /// that are part of Kinematica's task graph.
    /// </summary>
    /// <seealso cref="MotionSynthesizer.Allocate"/>
    public struct MemoryIdentifier
    {
        /// <summary>
        /// Denotes the handle used to identify a data type element.
        /// </summary>
        public short index;

        /// <summary>
        /// Determines if the given memory identifier is valid or not.
        /// </summary>
        /// <returns>True if the memory identifier is valid; false otherwise.</returns>
        public bool IsValid => index != Invalid;

        /// <summary>
        /// Determines whether two memory identifiers are equal.
        /// </summary>
        /// <param name="identifier">The memory identifier to compare against the current memory identifier.</param>
        /// <returns>True if the specified memory identifier is equal to the current memory identifier; otherwise, false.</returns>
        public bool Equals(MemoryIdentifier identifier)
        {
            return index == identifier.index;
        }

        /// <summary>
        /// Implicit conversion from a memory identifier to a short.
        /// </summary>
        public static implicit operator short(MemoryIdentifier identifier)
        {
            return identifier.index;
        }

        /// <summary>
        /// Implicit conversion from a short to a memory identifier.
        /// </summary>
        public static implicit operator MemoryIdentifier(short index)
        {
            return Create(index);
        }

        internal static MemoryIdentifier Create(short index)
        {
            return new MemoryIdentifier
            {
                index = index
            };
        }

        /// <summary>
        /// Invalid memory identifier.
        /// </summary>
        public static MemoryIdentifier Invalid => - 1;
    }
}
