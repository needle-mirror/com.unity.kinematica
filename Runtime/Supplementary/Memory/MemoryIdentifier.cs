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
        /// It's a combination of index of the memory block in memory chunk and a unique version
        /// to differentiate blocks with same index because of re-allocation
        /// </summary>
        public int uniqueIdentifier;

        /// <summary>
        /// Index of the corresponding memory block in the memory chunk.
        /// Negative value means the identifier is invalid
        /// </summary>
        public short index => (short)(uniqueIdentifier & 0xFFFF);

        /// <summary>
        /// When memory chunk re-allocates a block with same index, it will increment the version
        /// to ensure previously deleted block and re-allocated block don't share the same identifier
        /// </summary>
        public short version => (short)(uniqueIdentifier >> 16);

        /// <summary>
        /// Determines if the given memory identifier is valid or not.
        /// </summary>
        /// <returns>True if the memory identifier is valid; false otherwise.</returns>
        public bool IsValid => index >= 0;

        /// <summary>
        /// Determines whether two memory identifiers are equal.
        /// </summary>
        /// <param name="identifier">The memory identifier to compare against the current memory identifier.</param>
        /// <returns>True if the specified memory identifier is equal to the current memory identifier; otherwise, false.</returns>
        public bool Equals(MemoryIdentifier identifier)
        {
            return uniqueIdentifier == identifier.uniqueIdentifier;
        }

        public static bool operator==(MemoryIdentifier id1, MemoryIdentifier id2)
        {
            return id1.uniqueIdentifier == id2.uniqueIdentifier;
        }

        public static bool operator!=(MemoryIdentifier id1, MemoryIdentifier id2)
        {
            return id1.uniqueIdentifier != id2.uniqueIdentifier;
        }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            return Equals((MemoryIdentifier)obj);
        }

        public override int GetHashCode()
        {
            return uniqueIdentifier;
        }

        internal static MemoryIdentifier Create(short index, short version)
        {
            return new MemoryIdentifier
            {
                uniqueIdentifier = (version << 16) + index
            };
        }

        /// <summary>
        /// Invalid memory identifier.
        /// </summary>
        public static MemoryIdentifier Invalid => Create(-1, 0);
    }
}
