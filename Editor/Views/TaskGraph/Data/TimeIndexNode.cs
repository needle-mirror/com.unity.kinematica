namespace Unity.Kinematica.Editor
{
    [GraphNode(typeof(TimeIndex))]
    internal class TimeIndexNode : TimeNode
    {
        public override SamplingTime GetSamplingTime()
        {
            ref var memoryChunk = ref owner.memoryChunk.Ref;

            return SamplingTime.Create(
                memoryChunk.GetRef<TimeIndex>(identifier).Ref);
        }

        //public unsafe override void SetSamplingTime(ref MotionSynthesizer synthesizer, SamplingTime samplingTime)
        //{
        //    ref var memoryChunk = ref synthesizer.memoryChunkShadow.Ref;

        //    memoryChunk.GetRef<TimeIndex>(identifier).Ref = samplingTime.timeIndex;

        //    memoryChunk.GetHeader(identifier)->SetDirty();
        //}
    }
}
