using Unity.Burst;

namespace Unity.Kinematica
{
    /// <summary>
    /// The root task is the only mandatory task that exsists
    /// in a task graph and is implicitely created as part of
    /// the task graph execution system. It is responsible for
    /// executing the optional part of the task graph.
    /// </summary>
    [Data("Root", "#2A5637"), BurstCompile]
    public struct RootTask : Task
    {
        internal MemoryIdentifier self;

        internal MemoryRef<MotionSynthesizer> synthesizer;

        /// <summary>
        /// Execute method for the root task.
        /// </summary>
        /// <remarks>
        /// The root task executes its children in order
        /// until a child task returns a success status. The root
        /// task unconditionally returns a success status.
        /// </remarks>
        /// <returns>Result of the root task.</returns>
        public unsafe Result Execute()
        {
            ref var synthesizer = ref this.synthesizer.Ref;

            ref var memoryChunk = ref synthesizer.memoryChunk.Ref;

            var node = memoryChunk.FirstChild(self);

            while (node.IsValid)
            {
                var result = synthesizer.Execute(node);

                if (result == Result.Success)
                {
                    break;
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
            return self.Cast<RootTask>().Execute();
        }

        internal RootTask(ref MotionSynthesizer synthesizer)
        {
            this.synthesizer = synthesizer.self;

            self = MemoryIdentifier.Invalid;
        }

        internal static RootTask Create(ref MotionSynthesizer synthesizer)
        {
            return new RootTask(ref synthesizer);
        }
    }
}
