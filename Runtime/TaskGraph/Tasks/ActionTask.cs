using Unity.Burst;

namespace Unity.Kinematica
{
    /// <summary>
    /// An action task acts as a grouping mechanism for tasks.
    /// </summary>
    /// <remarks>
    /// Tasks that are created inside of an action task will be
    /// automatically arranged in a directed acyclic graph and
    /// execute in topological order.
    /// </remarks>
    /// <seealso cref="Task"/>
    [Data("Action", "#372A56", DataType.Flag.TopologySort), BurstCompile]
    public struct ActionTask : Task, GenericTask<ActionTask>
    {
        /// <summary>
        /// Identifier that represents the instance of this action task.
        /// </summary>
        /// <seealso cref="Identifier"/>
        public Identifier<ActionTask> self { get; set; }

        internal MemoryRef<MotionSynthesizer> synthesizer;

        /// <summary>
        /// Execute method for the action task.
        /// </summary>
        /// <remarks>
        /// Action tasks execute its children in topological order
        /// until a child task returns a failure status. The action
        /// task returns a success status if all children return
        /// a success status.
        /// </remarks>
        /// <returns>Failure if one of the child tasks fails; success if the action task has no children or all tasks have succeeded.</returns>
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
            return self.Cast<ActionTask>().Execute();
        }

        internal ActionTask(ref MotionSynthesizer synthesizer)
        {
            this.synthesizer = synthesizer.self;

            self = Identifier<ActionTask>.Invalid;
        }

        internal static ActionTask Create(ref MotionSynthesizer synthesizer)
        {
            return new ActionTask(ref synthesizer);
        }

        /// <summary>
        /// Implicit cast operator that allows to convert an action task reference into an typed identifier.
        /// </summary>
        public static implicit operator Identifier<ActionTask>(ActionTask task)
        {
            return task.self;
        }
    }
}
