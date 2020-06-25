using UnityEngine.Assertions;

namespace Unity.Kinematica.Editor
{
    [GraphNode(typeof(SamplingTime))]
    internal class SamplingTimeNode : TimeNode
    {
        public override SamplingTime GetSamplingTime()
        {
            ref var memoryChunk = ref owner.memoryChunk.Ref;

            return memoryChunk.GetRef<SamplingTime>(identifier).Ref;
        }

        //public unsafe override void SetSamplingTime(ref MotionSynthesizer synthesizer, SamplingTime samplingTime)
        //{
        //    ref var memoryChunk = ref synthesizer.memoryChunkShadow.Ref;

        //    memoryChunk.GetRef<SamplingTime>(identifier).Ref = samplingTime;

        //    memoryChunk.GetHeader(identifier)->SetDirty();
        //}
    }
}
