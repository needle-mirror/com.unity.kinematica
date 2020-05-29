namespace Unity.Kinematica
{
    internal struct MemoryArrayIdentifier
    {
        public MemoryIdentifier identifier;
        public short index;

        public bool IsValid => identifier.IsValid;

        public bool Equals(MemoryArrayIdentifier other)
        {
            return identifier == other.identifier && index == other.index;
        }

        public static MemoryArrayIdentifier Create(short identifier, short version, short index = 0)
        {
            return new MemoryArrayIdentifier
            {
                identifier = MemoryIdentifier.Create(identifier, version),
                index = index
            };
        }

        public static MemoryArrayIdentifier Invalid => Create(-1, 0);
    }
}
