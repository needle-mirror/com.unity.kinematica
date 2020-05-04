using Unity.Burst;

namespace Unity.Kinematica
{
    /// <summary>
    /// The selector task executes all of its children in order
    /// until it encounters a non-failure status.
    /// </summary>
    [Data("Selector", "#2A5637"), BurstCompile]
    public struct SelectorTask : Task
    {
        internal Identifier<SelectorTask> self;

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
        public static Result ExecuteSelf(ref TaskRef self)
        {
            return self.Cast<SelectorTask>().Execute();
        }

        internal SelectorTask(ref MotionSynthesizer synthesizer)
        {
            this.synthesizer = synthesizer.self;

            self = Identifier<SelectorTask>.Invalid;
        }

        /// <summary>
        /// Creates a new condition task as a child of the selector task.
        /// </summary>
        /// <returns>Reference to the newly created condition task.</returns>
        public ref ConditionTask Condition()
        {
            return ref synthesizer.Ref.Condition(self);
        }

        /// <summary>
        /// Creates a new action task as a child of the selector task.
        /// </summary>
        /// <returns>Reference to the newly created action task.</returns>
        public ref ActionTask Action()
        {
            return ref synthesizer.Ref.Action(self);
        }

        /// <summary>
        /// Creates a new selector task as a child of the selector task.
        /// </summary>
        /// <returns>Reference to the newly created selector task.</returns>
        public ref SelectorTask Selector()
        {
            return ref synthesizer.Ref.Selector(self);
        }

        /// <summary>
        /// Creates a new sequence task as a child of the selector task.
        /// </summary>
        /// <param name="loop">If false, once the sequence has finished executing all its children, it will do nothing and just return success. If true, sequence will reexecute all its children tasks indefinitely.</param>
        /// <param name="resetWhenNotExecuted">If true, and if the sequence isn't executed during one task graph pass, next time the sequence will be executed again, it will restart execution from its first child.</param>
        /// <returns>Reference to the newly created sequence task.</returns>
        public ref SequenceTask Sequence(bool loop = false, bool resetWhenNotExecuted = true)
        {
            return ref synthesizer.Ref.Sequence(self, loop, resetWhenNotExecuted);
        }

        /// <summary>
        /// Creates a new parallel task as a child of the selector task.
        /// </summary>
        /// <returns>Reference to the newly created parallel task.</returns>
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
