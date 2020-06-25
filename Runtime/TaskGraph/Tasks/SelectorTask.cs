using Unity.Burst;

namespace Unity.Kinematica
{
    /// <summary>
    /// The selector task executes all of its children in order
    /// until it encounters a non-failure status.
    /// </summary>
    [Data("Selector", "#2A5637"), BurstCompile]
    public struct SelectorTask : Task, GenericTask<SelectorTask>
    {
        public Identifier<SelectorTask> self { get; set; }

        internal MemoryRef<MotionSynthesizer> synthesizer;

        /// <summary>
        /// Execute method for the selector task.
        /// </summary>
        /// <remarks>
        /// The selector task executes all of its children in order
        /// until it encounters a non-failure status.
        /// </remarks>
        /// <returns>Result of the child task that didn't execute with a failure status; success if the selector task has no children.</returns>
        public unsafe Result Execute()
        {
            ref var synthesizer = ref this.synthesizer.Ref;

            ref var memoryChunk = ref synthesizer.memoryChunk.Ref;

            var node = memoryChunk.FirstChild(self);

            while (node.IsValid)
            {
                var result = synthesizer.Execute(node);

                if (result != Result.Failure)
                {
                    return result;
                }

                node = memoryChunk.NextSibling(node);
            }

            return Result.Success;
        }

        /// <summary>
        /// Surrogate method for automatic task execution.
        /// </summary>
        /// <param name="self">Task reference that is supposed to be executed.</param>
        /// <returns>Result of the task execution.</returns>
        [BurstCompile]
        public static Result ExecuteSelf(ref TaskPointer self)
        {
            return self.Cast<SelectorTask>().Execute();
        }

        internal SelectorTask(ref MotionSynthesizer synthesizer)
        {
            this.synthesizer = synthesizer.self;

            self = Identifier<SelectorTask>.Invalid;
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

        internal static SelectorTask Create(ref MotionSynthesizer synthesizer)
        {
            return new SelectorTask(ref synthesizer);
        }

        /// <summary>
        /// Implicit cast operator that allows to convert a selector task into a typed identifier.
        /// </summary>
        public static implicit operator Identifier<SelectorTask>(SelectorTask task)
        {
            return task.self;
        }

        /// <summary>
        /// Implicit cast operator that allows to convert a selector task into an identifier.
        /// </summary>
        public static implicit operator MemoryIdentifier(SelectorTask task)
        {
            return task.self;
        }
    }
}
