using Unity.Burst;

namespace Unity.Kinematica
{
    /// <summary>
    /// The parallel task executes all of its children in order
    /// until it encounters a non-success status.
    /// </summary>
    [Data("Parallel", "#2A5637"), BurstCompile]
    public struct ParallelTask : Task, GenericTask<ParallelTask>
    {
        public Identifier<ParallelTask> self { get; set; }

        internal MemoryRef<MotionSynthesizer> synthesizer;

        /// <summary>
        /// Execute method for the parallel task.
        /// </summary>
        /// <remarks>
        /// The parallel task executes all of its children in order
        /// until it encounters a non-success status.
        /// </remarks>
        /// <returns>Result of the child task that didn't execute successfully; success if the parallel task has no children.</returns>
        public unsafe Result Execute()
        {
            ref var synthesizer = ref this.synthesizer.Ref;

            ref var memoryChunk = ref synthesizer.memoryChunk.Ref;

            var node = memoryChunk.FirstChild(self);

            while (node.IsValid)
            {
                var result = synthesizer.Execute(node);

                if (result != Result.Success)
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
            return self.Cast<ParallelTask>().Execute();
        }

        internal ParallelTask(ref MotionSynthesizer synthesizer)
        {
            this.synthesizer = synthesizer.self;

            self = Identifier<ParallelTask>.Invalid;
        }

        internal static ParallelTask Create(ref MotionSynthesizer synthesizer)
        {
            return new ParallelTask(ref synthesizer);
        }

        /// <summary>
        /// Implicit cast operator that allows to convert a parallel task into a typed identifier.
        /// </summary>
        public static implicit operator Identifier<ParallelTask>(ParallelTask task)
        {
            return task.self;
        }
    }
}
