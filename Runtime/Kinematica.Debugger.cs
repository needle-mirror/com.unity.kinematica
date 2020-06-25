using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
using UnityEngine.Assertions;

using Unity.Mathematics;
using Unity.SnapshotDebugger;
using Unity.Collections;

using Buffer = Unity.SnapshotDebugger.Buffer;
using System.Linq;

namespace Unity.Kinematica
{
    public partial class Kinematica : SnapshotProvider, FrameDebugProvider<AnimationFrameDebugInfo>, IMotionSynthesizerProvider
    {
        List<AnimationFrameDebugInfo> m_FrameDebugInfos = new List<AnimationFrameDebugInfo>();

        public int GetUniqueIdentifier()
        {
            return gameObject.GetInstanceID();
        }

        public string GetDisplayName()
        {
            return gameObject.name;
        }

        /// <summary>
        /// return the currently active animation frames
        /// </summary>
        /// <returns></returns>
        public List<AnimationFrameDebugInfo> GetFrameDebugInfo()
        {
            if (!synthesizerHolder.IsValid)
            {
                return new List<AnimationFrameDebugInfo>();
            }

            return Synthesizer.Ref.GetFrameDebugInfo();
        }

        /// <summary>
        /// Stores the contents of the Kinematica component in the buffer passed as argument.
        /// </summary>
        /// <param name="buffer">Buffer that the contents of the Kinematica component should be written to.</param>
        public override void WriteToStream(Buffer buffer)
        {
            buffer.Write(transform.position);
            buffer.Write(transform.rotation);

            synthesizerHolder.WriteToStream(buffer);
        }

        /// <summary>
        /// Retrieves the contents of the Kinematica component from the buffer passed as argument.
        /// </summary>
        /// <param name="buffer">Buffer that the contents of the Kinematica component should be read from.</param>
        public override void ReadFromStream(Buffer buffer)
        {
            transform.position = buffer.ReadVector3();
            transform.rotation = buffer.ReadQuaternion();

            synthesizerHolder.ReadFromStream(buffer);
        }

        /// <summary>
        /// Informs the snapshot debugger that Kinematica require serialize/deserialize callback functions called
        /// </summary>
        public override bool RequirePostProcess => true;

        /// <summary>
        /// Post process callback called after all snapshot objects have been serialized, can be use to serialize additional data
        /// </summary>
        public override void OnWritePostProcess(Buffer buffer) => synthesizerHolder.OnWritePostProcess(buffer);

        /// <summary>
        /// Post process callback called after all snapshot objects have been deserialized, can be use to deserialize additional data.
        /// </summary>
        public override void OnReadPostProcess(Buffer buffer) => synthesizerHolder.OnReadPostProcess(buffer);
    }
}
