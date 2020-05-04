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
        public static Result ExecuteSelf(ref TaskRef self)
        {
            return self.Cast<SequenceTask>().Execute();
        }

        internal SequenceTask(ref MotionSynthesizer synthesizer, bool loop, bool resetWhenNotExecuted)
        {
            this.synthesizer = synthesizer.self;

            self = MemoryIdentifier.Invalid;

            this.loop = loop;
            this.resetWhenNotExecuted = resetWhenNotExecuted;

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
        /// <param name="loop">If false, once the sequence has finished executing all its children, it will do nothing and just return success. If true, sequence will reexecute all its children tasks indefinitely.</param>
        /// <param name="resetWhenNotExecuted">If true, and if the sequence isn't executed during one task graph pass, next time the sequence will be executed again, it will restart execution from its first child.</param>
        /// <returns>Reference to the newly created sequence task.</returns>
        public ref SequenceTask Sequence(bool loop = false, bool resetWhenNotExecuted = true)
        {
            return ref synthesizer.Ref.Sequence(self, loop, resetWhenNotExecuted);
        }

        /// <summary>
        /// Creates a new parallel task as a child of the sequence task.
        /// </summary>
        /// <returns>Reference to the newly created parallel task.</returns>
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
