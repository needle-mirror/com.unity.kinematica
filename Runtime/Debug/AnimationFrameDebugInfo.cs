using System;
using Unity.SnapshotDebugger;

namespace Unity.Kinematica
{
    internal struct AnimationFrameDebugInfo : IFrameDebugInfo
    {
        public int     sequenceIdentifier;
        public string  animName;
        public float   animFrame;
        public float   animTime;
        public float   weight;
    }
}
