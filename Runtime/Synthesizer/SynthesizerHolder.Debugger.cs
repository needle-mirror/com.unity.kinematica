using System.Collections.Generic;
using Unity.SnapshotDebugger;

namespace Unity.Kinematica
{
    /// <summary>
    /// This struct hold and setup the synthesizer and its dependencies.
    /// </summary>
    /// <seealso cref="MotionSynthesizer"/>
    public partial struct MotionSynthesizerHolder
    {
        /// <summary>
        /// Forward the SnapshotDebugger WriteToStream call to the synthesizer
        /// </summary>
        /// <param name="buffer"></param>
        public void WriteToStream(Unity.SnapshotDebugger.Buffer buffer)
        {
            if (synthesizer.IsValid)
            {
                synthesizer.Ref.WriteToStream(buffer);
            }
        }

        /// <summary>
        /// Forward the SnapshotDebugger ReadFromStream call to the synthesizer
        /// </summary>
        /// <param name="buffer"></param>
        public void ReadFromStream(Unity.SnapshotDebugger.Buffer buffer)
        {
            if (synthesizer.IsValid)
            {
                synthesizer.Ref.ReadFromStream(buffer);
            }
        }

        /// <summary>
        /// Perform the SnapshotDebugger OnPostProcess() method on the synthesizer with the provided buffer.
        /// </summary>
        /// <param name="buffer"></param>
        public void OnWritePostProcess(Unity.SnapshotDebugger.Buffer buffer)
        {
#if UNITY_EDITOR
            if (synthesizer.IsValid)
            {
                synthesizer.Ref.memoryChunkShadow.Ref.WriteToStream(buffer);
            }
#endif
        }

        /// <summary>
        /// Perform the SnapshotDebugger OnPostProcess(Buffer buffer) method on the synthesizer.
        /// </summary>
        public void OnReadPostProcess(Unity.SnapshotDebugger.Buffer buffer)
        {
#if UNITY_EDITOR
            if (synthesizer.IsValid)
            {
                synthesizer.Ref.memoryChunkShadow.Ref.ReadFromStream(buffer);
            }
#endif
        }
    }
}
