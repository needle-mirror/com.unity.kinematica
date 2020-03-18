namespace Unity.Kinematica
{
    /// <summary>
    /// Wrapper around a memory identifier that carries the type of the memory identifier.
    /// </summary>
    /// <seealso cref="MemoryIdentifier"/>
    public struct Identifier<T> where T : struct
    {
        MemoryIdentifier identifier;

        /// <summary>
        /// Determines if the given identifier is valid or not.
        /// </summary>
        /// <returns>True if the identifier is valid; false otherwise.</returns>
        public bool IsValid => identifier.IsValid;

        /// <summary>
        /// Creates a new typed identifier from a plain identifier.
        /// </summary>
        /// <param name="identifier">The memory identifier that the typed identifier should refer to.</param>
        public static Identifier<T> Create(MemoryIdentifier identifier)
        {
            return new Identifier<T>
            {
                identifier = identifier
            };
        }

        /// <summary>
        /// Implicit conversion from a typed identifier to a plain memory identifier.
        /// </summary>
        public static implicit operator MemoryIdentifier(Identifier<T> identifier)
        {
            return identifier.identifier;
        }

        /// <summary>
        /// An invalid identifier.
        /// </summary>
        public static Identifier<T> Invalid => Create(MemoryIdentifier.Invalid);
    }
}
