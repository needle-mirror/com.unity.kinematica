using Unity.SnapshotDebugger;

using Buffer = Unity.SnapshotDebugger.Buffer;

namespace Unity.Kinematica
{
    public partial struct MotionSynthesizer
    {
        internal void WriteToStream(Buffer buffer)
        {
            buffer.Write(rootTransform);
            buffer.Write(rootDeltaTransform);
            buffer.Write(samplingTime);
            buffer.Write(lastSamplingTime);
            buffer.Write(delayedPushTime);

            poseGenerator.WriteToStream(buffer);
            trajectory.WriteToStream(buffer);

            memoryChunk.Ref.WriteToStream(buffer);
        }

        internal void ReadFromStream(Buffer buffer)
        {
            rootTransform = buffer.ReadAffineTransform();
            rootDeltaTransform = buffer.ReadAffineTransform();
            samplingTime = buffer.ReadSamplingTime();
            lastSamplingTime = buffer.ReadTimeIndex();
            delayedPushTime = buffer.ReadTimeIndex();

            poseGenerator.ReadFromStream(buffer);
            trajectory.ReadFromStream(buffer);

            memoryChunk.Ref.ReadFromStream(buffer);
        }

        internal Buffer OnPostProcess()
        {
            var buffer = Buffer.Create();

#if UNITY_EDITOR
            memoryChunkShadow.Ref.WriteToStream(buffer);
#endif
            return buffer;
        }

        internal void OnPostProcess(Buffer buffer)
        {
#if UNITY_EDITOR
            memoryChunkShadow.Ref.ReadFromStream(buffer);
#endif
        }
    }
}
