using System;

using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;

using Unity.Mathematics;
using Unity.SnapshotDebugger;
using Unity.Collections;

namespace Unity.Kinematica
{
    /// <summary>
    /// This component is a wrapper around the motion synthesizer.
    /// </summary>
    /// <remarks>
    /// The motion synthesizer represents the actual core implementation
    /// of Kinematica which can be used in a pure DOTS environment directly.
    /// It provides a raw transform buffer which represents the current
    /// character pose and does not provide any infrastructure to feed
    /// the current pose to the character.
    /// <para>
    /// The Kinematica component is a wrapper around the motion synthesizer
    /// that can be used in scenarios where Kinematica is to be used in
    /// conjunction with stock Unity Game Objects.
    /// </para>
    /// <para>
    /// It establishes a Playable graph that forwards the character pose
    /// to the Animator component. It also provides automatic snapshots
    /// and rewind functionality, i.e. no additional user code is required
    /// to support snapshot debugging of the Kinematica component.
    /// </para>
    /// </remarks>
    /// <seealso cref="MotionSynthesizer"/>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    [AddComponentMenu("Kinematica/Kinematica")]
    public partial class Kinematica : SnapshotProvider//, FrameDebugProvider<AnimationFrameDebugInfo>
    {
        /// <summary>
        /// Allows access to the underlying Kinemtica runtime asset.
        /// </summary>
        public BinaryReference resource;

        /// <summary>
        /// Denotes the default blend duration for the motion synthesizer.
        /// </summary>
        [Tooltip("Blending between poses will last for this duration in seconds.")]
        [Range(0.0f, 1.0f)]
        public float blendDuration = 0.25f;

        internal bool IsInitialized
        {
            get; private set;
        }

        [Tooltip("If true, Kinematica will apply root motion to move character in the world. Set this boolean to false in order to process root motion inside your script.")]
        public bool applyRootMotion = true;

        /// <summary>
        /// Denotes the delta time in seconds to be used during this frame.
        /// </summary>
        /// <remarks>
        /// The delta time in seconds mirrors Time.deltaTime during play mode
        /// unless the snapshot debugger rewinds to a recorded snapshot.
        /// The current frame delta time is recorded as part of a snapshot
        /// to guarantee the exact same evaluation result when in case the
        /// snapshot debugger rewinds to a previous snapshot frame.
        /// </remarks>
        [SerializeField]
        [HideInInspector]
        protected float _deltaTime;

        MemoryHeader<MotionSynthesizer> synthesizer;

        MemoryHeader<MemoryChunk> memoryChunk;

#if UNITY_EDITOR
        MemoryHeader<MemoryChunk> memoryChunkShadow;
#endif

        PlayableGraph playableGraph;

        Job job;

        /// <summary>
        /// Allows direct access to the underlying Kinematica runtime asset.
        /// </summary>
        public ref Binary Binary
        {
            get { return ref synthesizer.Ref.Binary; }
        }

        /// <summary>
        /// Allows direct access to the motion synthesizer.
        /// </summary>
        /// <remarks>
        /// Most of Kinematica's API methods can be found in the
        /// motion synthesizer. API methods that are specific to the
        /// game object wrapper can be found on the Kinematica
        /// component directly.
        /// </remarks>
        public MemoryRef<MotionSynthesizer> Synthesizer
        {
            get
            {
                if (synthesizer.IsValid)
                {
                    return synthesizer;
                }

                var memoryRequirements =
                    MemoryRequirements.Create(1024);

                memoryChunk = MemoryChunk.Create(
                    memoryRequirements, Allocator.Persistent);

#if UNITY_EDITOR
                memoryChunkShadow = MemoryChunk.Create(
                    memoryRequirements, Allocator.Persistent);
#endif
                var rootTransform =
                    new AffineTransform(
                        transform.localPosition, transform.localRotation);

                synthesizer = MotionSynthesizer.Create(
                    resource, rootTransform, blendDuration);

                synthesizer.Ref.self = synthesizer;
                synthesizer.Ref.memoryChunk = memoryChunk;
#if UNITY_EDITOR
                synthesizer.Ref.memoryChunkShadow = memoryChunkShadow;
#endif

                var typeIndex =
                    synthesizer.Ref.GetDataTypeIndex<RootTask>();

                var root = memoryChunk.Ref.Allocate(
                    RootTask.Create(ref synthesizer.Ref),
                    typeIndex, MemoryIdentifier.Invalid);

                synthesizer.Ref.GetRef<RootTask>(root).Ref.self = root;

                return synthesizer;
            }
        }

        /// <summary>
        /// Override for OnEnable().
        /// </summary>
        /// <remarks>
        /// The Playable graph that forwards the current character pose to the
        /// Animator component gets constructed during the execution of this method.
        /// <para>
        /// This method also registers the Kinematica component with the snapshot debugger.
        /// </para>
        /// </remarks>
        public override void OnEnable()
        {
            base.OnEnable();

//#if UNITY_EDITOR
//            Debugger.frameDebugger.AddFrameDebugProvider<AnimationDebugRecord>(this);
//#endif
            OnEarlyUpdate(false);

            try
            {
                if (!CreatePlayableGraph())
                {
                    throw new Exception("Couldn't create playable graph");
                }

                IsInitialized = true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Cannot play Kinematica asset on target {gameObject.name} : {e.Message}");
                gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Override for OnDisable()
        /// </summary>
        /// <remarks>
        /// This method releases all internally constructed objects and unregisters
        /// the Kinematica component from the snapshot debugger.
        /// </remarks>
        public override void OnDisable()
        {
            if (!IsInitialized)
            {
                return;
            }

            base.OnDisable();

//#if UNITY_EDITOR
//            Debugger.frameDebugger.RemoveFrameDebugProvider(this);
//#endif
            synthesizer.Dispose();

            if (memoryChunk.IsValid)
            {
                memoryChunk.Ref.Dispose();
            }

            memoryChunk.Dispose();

#if UNITY_EDITOR
            if (memoryChunkShadow.IsValid)
            {
                memoryChunkShadow.Ref.Dispose();
            }

            memoryChunkShadow.Dispose();
#endif
            job.Dispose();

            if (playableGraph.IsValid())
            {
                playableGraph.Destroy();
            }
        }

        /// <summary>
        /// This callback will be automatically invoked
        /// during UnityEngine.PlayerLoop.EarlyUpdate().
        /// </summary>
        public virtual void EarlyUpdate()
        {
        }

        /// <summary>
        /// Override for OnEarlyUpdate() which will be invoked
        /// as part of the snapshot debugger infrastructure during
        /// the execution of UnityEngine.PlayerLoop.EarlyUpdate.
        /// </summary>
        /// <param name="rewind">True when the snapshot debugger rewinds to a previously recorded snapshot, false otherwise.</param>
        public override void OnEarlyUpdate(bool rewind)
        {
            _deltaTime = Debugger.instance.deltaTime;

            if (!rewind)
            {
                EarlyUpdate();
            }

#if UNITY_EDITOR
            if (synthesizer.IsValid)
            {
                synthesizer.Ref.immutable = rewind;
            }
#endif
        }

        /// <summary>
        /// Called during the regular game object update loop.
        /// </summary>
        public void Update()
        {
            if (synthesizer.IsValid)
            {
                synthesizer.Ref.UpdateFrameCount(Time.frameCount);
            }
        }

        /// <summary>
        /// Handler method which gets invoked during the animator update.
        /// </summary>
        /// <remarks>
        /// The motion synthesizer maintains the full world space transform
        /// of the character at all times. This method simply forwards this
        /// transform to the game object's transform.
        /// </remarks>
        public virtual void OnAnimatorMove()
        {
            if (applyRootMotion && synthesizer.IsValid)
            {
                transform.position = synthesizer.Ref.WorldRootTransform.t;
                transform.rotation = synthesizer.Ref.WorldRootTransform.q;
            }
        }

        bool CreatePlayableGraph()
        {
            var animator = GetComponent<Animator>();
            if (animator.avatar == null)
            {
                animator.avatar = AvatarBuilder.BuildGenericAvatar(animator.gameObject, transform.name);
                animator.avatar.name = "Avatar";
            }

            job = new Job();
            if (!job.Setup(animator,
                GetComponentsInChildren<Transform>(), ref Synthesizer.Ref))
            {
                return false;
            }

            playableGraph =
                PlayableGraph.Create(
                    $"Kinematica_{animator.transform.name}");

            var output = AnimationPlayableOutput.Create(playableGraph, "ouput", animator);

            var playable = AnimationScriptPlayable.Create(playableGraph, job);

            output.SetSourcePlayable(playable);

            playableGraph.Play();

            return true;
        }
    }
}
