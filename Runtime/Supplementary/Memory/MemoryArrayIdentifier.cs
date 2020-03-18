using System;
using Unity.Collections;
using UnityEngine.Assertions;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Kinematica
{
    internal struct MemoryArrayIdentifier
    {
        public MemoryIdentifier identifier;
        public short index;

        public bool IsValid => identifier.IsValid;

        public bool Equals(MemoryArrayIdentifier other)
        {
            return identifier.Equals(other.identifier) && index == other.index;
        }

        public static implicit operator MemoryArrayIdentifier(short index)
        {
            return Create(index);
        }

        public static MemoryArrayIdentifier Create(short identifier, short index = 0)
        {
            return new MemoryArrayIdentifier
            {
                identifier = identifier,
                index = index
            };
        }

        public static MemoryArrayIdentifier Invalid => - 1;
    }
}
