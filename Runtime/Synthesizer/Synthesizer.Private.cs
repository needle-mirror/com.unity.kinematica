using Unity.Mathematics;
using Unity.Collections;

namespace Unity.Kinematica
{
    public partial struct MotionSynthesizer
    {
        private void Construct(ref MemoryBlock memoryBlock, BlobAssetReference<Binary> binary, AffineTransform worldRootTransform, float blendDuration)
        {
            m_binary = binary;

            // We basically copy statically available data into this instance
            // so that the burst compiler does not complain about accessing static data.
            ConstructDataTypes(ref memoryBlock);
            ConstructTraitTypes(ref memoryBlock);

            poseGenerator.Construct(ref memoryBlock, ref binary.Value, blendDuration);

            trajectory.Construct(ref memoryBlock, ref binary.Value);

            rootTransform = worldRootTransform;
            rootDeltaTransform = AffineTransform.identity;

            lastSamplingTime = TimeIndex.Invalid;

            samplingTime = SamplingTime.Invalid;

            delayedPushTime = TimeIndex.Invalid;

            frameCount = -1;

            lastProcessedFrameCount = -1;
        }

        [ReadOnly]
        private BlobAssetReference<Binary> m_binary;

        /// <summary>
        /// The trajectory model maintains a representation of the simulated
        /// character movement over the global time horizon.
        /// </summary>
        public TrajectoryModel trajectory;

        /// <summary>
        /// Denotes the delta time in seconds during the last update.
        /// </summary>
        public float deltaTime => _deltaTime;

        internal AffineTransform rootTransform;

        internal AffineTransform rootDeltaTransform;

        /// <summary>
        /// Denotes a references to the motion synthesizer itself.
        /// </summary>
        public MemoryRef<MotionSynthesizer> self;

        internal MemoryRef<MemoryChunk> memoryChunk;

#if UNITY_EDITOR
        internal MemoryRef<MemoryChunk> memoryChunkShadow;
#endif
        MemoryArray<DataType> dataTypes;

        MemoryArray<DataField> dataFields;

        MemoryArray<int> sizeOfTable;

        MemoryArray<TraitType> traitTypes;

#if UNITY_EDITOR
        internal BlittableBool immutable;
#endif
        BlittableBool updateInProgress;

        PoseGenerator poseGenerator;

        float _deltaTime;

        SamplingTime samplingTime;

        TimeIndex lastSamplingTime;

        TimeIndex delayedPushTime;

        private int frameCount;

        private int lastProcessedFrameCount;
    }
}
