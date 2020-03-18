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

namespace Unity.Kinematica
{
    public partial class Kinematica : SnapshotProvider
    {
        internal int GetUniqueIdentifier()
        {
            return gameObject.GetInstanceID();
        }

        internal string GetDisplayName()
        {
            return gameObject.name;
        }

        //public List<AnimationFrameDebugInfo> GetFrameDebugInfo()
        //{
        //    List<AnimationFrameDebugInfo> snapshots = new List<AnimationFrameDebugInfo>();

        //    //for(int i = 0; i < Synthesizer.Ref.GetNumActiveAtoms(); ++i)
        //    //{
        //    //    AtomDebugInfo atomDebugInfo = Synthesizer.Ref.GetAtomDebugInfo(i);

        //    //    snapshots.Add(new AnimationFrameDebugInfo()
        //    //    {
        //    //        sequenceIdentifier = atomDebugInfo.identifier,
        //    //        animName = atomDebugInfo.animName,
        //    //        animFrame = atomDebugInfo.animFrame,
        //    //        animTime = atomDebugInfo.animTime,
        //    //        weight = atomDebugInfo.weight
        //    //    });
        //    //}

        //    //Assert.IsTrue(false);

        //    return snapshots;
        //}

        /// <summary>
        /// Stores the contents of the Kinematica component in the buffer passed as argument.
        /// </summary>
        /// <param name="buffer">Buffer that the contents of the Kinematica component should be written to.</param>
        public override void WriteToStream(Buffer buffer)
        {
            buffer.Write(transform.position);
            buffer.Write(transform.rotation);

            if (synthesizer.IsValid)
            {
                synthesizer.Ref.WriteToStream(buffer);
            }
        }

        /// <summary>
        /// Retrieves the contents of the Kinematica component from the buffer passed as argument.
        /// </summary>
        /// <param name="buffer">Buffer that the contents of the Kinematica component should be read from.</param>
        public override void ReadFromStream(Buffer buffer)
        {
            transform.position = buffer.ReadVector3();
            transform.rotation = buffer.ReadQuaternion();

            if (synthesizer.IsValid)
            {
                synthesizer.Ref.ReadFromStream(buffer);
            }
        }

        /// <summary>
        /// Override of the OnPostProcess() method which gets invoked during snapshot debugging.
        /// </summary>
        public override Buffer OnPostProcess()
        {
            if (synthesizer.IsValid)
            {
                return synthesizer.Ref.OnPostProcess();
            }

            return null;
        }

        /// <summary>
        /// Override of the OnPostProcess() method which gets invoked during snapshot debugging.
        /// </summary>
        public override void OnPostProcess(Buffer buffer)
        {
            if (synthesizer.IsValid)
            {
                synthesizer.Ref.OnPostProcess(buffer);
            }
        }
    }
}
