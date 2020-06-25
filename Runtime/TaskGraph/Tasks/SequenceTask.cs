using Unity.Burst;

namespace Unity.Kinematica
{
    /// <summary>
    /// The sequence task executes all of its children in order
    /// until it encounters a success status.
    /// </summary>
    [Data("Sequence", "#2A5637"), BurstCompile]
    public struct SequenceTask : Task, GenericTask<SequenceTask>
    {
        public Identifier<SequenceTask> self { get; set; }

        internal MemoryRef<MotionSynthesizer> synthesizer;

        /// <summary>
        /// If false, once the sequence has finished executing all its children, it will do nothing and just return success. If true, sequence will reexecute all its children tasks indefinitely.
        /// </summary>
        BlittableBool loop;

        /// <summary>
        /// If true, and if the sequence isn't executed during one task graph pass, next time the sequence will be executed again, it will restart execution from its first child.
        /// </summary>
        BlittableBool resetWhenNotExecuted;

        int currentIndex;
        int expectedTickFrame;

        /// <summary>
        /// Execute method for the sequence task.
        /// </summary>
        /// <remarks>
        /// The sequence task executes all of its children in order, one per frame.
        /// If one child task execution fails, it will be re-executed again next frame, and so on until it succeeds.
        ///
        /// </remarks>
        /// <returns>Result success if a child task executes successfully or if all children have already been successfuly executed (the sequence is then considered to be finished); false otherwise.</returns>
        public unsafe Result Execute()
        {
            ref var synthesizer = ref this.synthesizer.Ref;

            ref var memoryChunk = ref synthesizer.memoryChunk.Ref;

            var header = memoryChunk.GetHeader(self);

            var nextTickFrame = header->GetNextTickFrame();

            if (resetWhenNotExecuted && expectedTickFrame != nextTickFrame)
            {
                currentIndex = 0;
            }

            expectedTickFrame = nextTickFrame + 1;

            if (currentIndex == memoryChunk.NumChildren(self))
            {
                if (loop)
                {
                    currentIndex = 0;
                }
                else
                {
                    return Result.Success;
                }
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
        public static Result ExecuteSelf(ref TaskPointer self)
        {
            return self.Cast<SequenceTask>().Execute();
        }

        internal SequenceTask(ref MotionSynthesizer synthesizer, bool loop, bool resetWhenNotExecuted)
        {
            this.synthesizer = synthesizer.self;

            self = Identifier<SequenceTask>.Invalid;

            this.loop = loop;
            this.resetWhenNotExecuted = resetWhenNotExecuted;

            currentIndex = 0;
            expectedTickFrame = -1;
        }

        [System.Obsolete("Please use similar function from TaskReference.")]
        public ref ConditionTask Condition()
        {
            return ref synthesizer.Ref.Condition(self);
        }

        [System.Obsolete("Please use similar function from TaskReference.")]
        public ref ActionTask Action()
        {
            return ref synthesizer.Ref.Action(self);
        }

        [System.Obsolete("Please use similar function from TaskReference.")]
        public ref SelectorTask Selector()
        {
            return ref synthesizer.Ref.Selector(self);
        }

        [System.Obsolete("Please use similar function from TaskReference.")]
        public ref SequenceTask Sequence(bool loop = false, bool resetWhenNotExecuted = true)
        {
            return ref synthesizer.Ref.Sequence(self, loop, resetWhenNotExecuted);
        }

        [System.Obsolete("Please use similar function from TaskReference.")]
        public ref ParallelTask Parallel()
        {
            return ref synthesizer.Ref.Parallel(self);
        }

        internal static SequenceTask Create(ref MotionSynthesizer synthesizer, bool loop, bool resetWhenNotExecuted)
        {
            return new SequenceTask(ref synthesizer, loop, resetWhenNotExecuted);
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
