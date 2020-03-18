using System;

namespace Unity.Kinematica.Editor
{
    internal struct MarkerInfo
    {
        public int frameIndex;
        public Type type;
    }

    internal interface ITag
    {
        Interval GetInterval(MarkerInfo[] markers, Interval tagInterval);
    }
}
