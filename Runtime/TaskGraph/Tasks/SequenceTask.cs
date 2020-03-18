using Unity.Burst;

namespace Unity.Kinematica
{
    /// <summary>
    /// The sequence task executes all of its children in order
    /// until it encounters a success status.
    /// </summary>
    [Data("Sequence", "#2A5637"), BurstCompile]
    public struct SequenceTask : Task
    {
        internal MemoryIdentifier self;

        internal MemoryRef<MotionSynthesizer> synthesizer;

        int currentIndex;
        int expectedTickFrame;

        /// <summary>
        /// Execute method for the sequence task.
        /// </summary>
        /// <remarks>
        /// The sequence task executes all of its children in order
        /// until it encounters a success status.
        /// </remarks>
        /// <returns>Result success if a child task executes successfully; false otherwise.</returns>
        public unsafe Result Execute()
        {
            ref var synthesizer = ref this.synthesizer.Ref;

            ref var memoryChunk = ref synthesizer.memoryChunk.Ref;

            var header = memoryChunk.GetHeader(self);

            var nextTickFrame = header->GetNextTickFrame();

            if (expectedTickFrame != nextTickFrame)
            {
                currentIndex = 0;
            }

            expectedTickFrame = nextTickFrame + 1;

            if (currentIndex == memoryChunk.NumChildren(self))
            {
                return Result.Success;
            }

            var node = memoryChunk.Child(self, currentIndex);

            if (node.IsValid)
            {
                var result = synthesizer.Execute(node);

                if (result == Result.Success)
                {
                    currentIndex++;
                }

                return result;
            }

            return Result.Failure;
        }

        /// <summary>
        /// Surrogate method for automatic task execution.
        /// </summary>
        /// <param name="self">Task reference that is supposed to be executed.</param>
        /// <returns>Result of the task execution.</returns>
        [BurstCompile]
        public static Result ExecuteSelf(ref TaskRef self)
        {
            return self.Cast<SequenceTask>().Execute();
        }

        internal SequenceTask(ref MotionSynthesizer synthesizer)
        {
            this.synthesizer = synthesizer.self;

            self = MemoryIdentifier.Invalid;

            currentIndex = 0;
            expectedTickFrame = -1;
        }

        /// <summary>
        /// Creates a new condition task as a child of the sequence task.
        /// </summary>
        /// <returns>Reference to the newly created condition task.</returns>
        public ref ConditionTask Condition()
        {
            return ref synthesizer.Ref.Condition(self);
        }

        /// <summary>
        /// Creates a new action task as a child of the sequence task.
        /// </summary>
        /// <returns>Reference to the newly created action task.</returns>
        public ref ActionTask Action()
        {
            return ref synthesizer.Ref.Action(self);
        }

        /// <summary>
        /// Creates a new selector task as a child of the sequence task.
        /// </summary>
        /// <returns>Reference to the newly created selector task.</returns>
        public ref SelectorTask Selector()
        {
            return ref synthesizer.Ref.Selector(self);
        }

        /// <summary>
        /// Creates a new sequence task as a child of the sequence task.
        /// </summary>
        /// <returns>Reference to the newly created sequence task.</returns>
        public ref SequenceTask Sequence()
        {
            return ref synthesizer.Ref.Sequence(self);
        }

        /// <summary>
        /// Creates a new parallel task as a child of the sequence task.
        /// </summary>
        /// <returns>Reference to the newly created parallel task.</returns>
        public ref ParallelTask Parallel()
        {
            return ref synthesizer.Ref.Parallel(self);
        }

        internal static SequenceTask Create(ref MotionSynthesizer synthesizer)
        {
            return new SequenceTask(ref synthesizer);
        }

        /// <summary>
        /// Implicit cast operator that allows to convert a selector task into an identifier.
        /// </summary>
        public static implicit operator MemoryIdentifier(SequenceTask task)
        {
            return task.self;
        }
    }
}
