using Unity.Burst;

namespace Unity.Kinematica
{
    /// <summary>
    /// The condition task executes its child based on
    /// an internal condition that can be controlled externally.
    /// </summary>
    [Data("Condition", "#2A3756"), BurstCompile]
    public struct ConditionTask : Task
    {
        internal Identifier<ConditionTask> self;

        internal MemoryRef<MotionSynthesizer> synthesizer;

        /// <summary>
        /// Denotes the internal state of the condition task.
        /// </summary>
        public BlittableBool value;

        /// <summary>
        /// Execute method for the condition task.
        /// </summary>
        /// <remarks>
        /// The condition task executes its first child if
        /// its internal value is set to true. In this case
        /// the condition task returns the status of the
        /// child task, Failure otherwise.
        /// </remarks>
        /// <returns>Result of the condition task.</returns>
        public Result Execute()
        {
            ref var synthesizer = ref this.synthesizer.Ref;

            ref var memoryChunk = ref synthesizer.memoryChunk.Ref;

            if (value)
            {
                var node = memoryChunk.FirstChild(self);

                if (node.IsValid)
                {
                    return synthesizer.Execute(node);
                }
            }

            return Result.Failure;
        }

        internal ref MotionSynthesizer Ref => ref synthesizer.Ref;

        /// <summary>
        /// Surrogate method for automatic task execution.
        /// </summary>
        /// <param name="self">Task reference that is supposed to be executed.</param>
        /// <returns>Result of the task execution.</returns>
        [BurstCompile]
        public static Result ExecuteSelf(ref TaskRef self)
        {
            return self.Cast<ConditionTask>().Execute();
        }

        internal ConditionTask(ref MotionSynthesizer synthesizer)
        {
            this.synthesizer = synthesizer.self;

            self = Identifier<ConditionTask>.Invalid;

            value = false;
        }

        /// <summary>
        /// Creates a new action task as a child of the contition task.
        /// </summary>
        /// <returns>Reference to the newly created action task.</returns>
        public ref ActionTask Action()
        {
            return ref synthesizer.Ref.Action(self);
        }

        /// <summary>
        /// Creates a new selector task as a child of the contition task.
        /// </summary>
        /// <returns>Reference to the newly created selector task.</returns>
        public ref SelectorTask Selector()
        {
            return ref synthesizer.Ref.Selector(self);
        }

        /// <summary>
        /// Creates a new sequence task as a child of the contition task.
        /// </summary>
        /// <returns>Reference to the newly created sequence task.</returns>
        public ref SequenceTask Sequence()
        {
            return ref synthesizer.Ref.Sequence(self);
        }

        /// <summary>
        /// Creates a new parallel task as a child of the contition task.
        /// </summary>
        /// <returns>Reference to the newly created parallel task.</returns>
        public ref ParallelTask Parallel()
        {
            return ref synthesizer.Ref.Parallel(self);
        }

        internal static ConditionTask Create(ref MotionSynthesizer synthesizer)
        {
            return new ConditionTask(ref synthesizer);
        }

        /// <summary>
        /// Implicit cast operator that allows to convert a condition task into a typed identifier.
        /// </summary>
        public static implicit operator Identifier<ConditionTask>(ConditionTask task)
        {
            return task.self;
        }

        /// <summary>
        /// Implicit cast operator that allows to convert a condition task into an identifier.
        /// </summary>
        public static implicit operator MemoryIdentifier(ConditionTask task)
        {
            return task.self;
        }

        /// <summary>
        /// Implicit cast operator that allows to convert a condition task to a bool.
        /// </summary>
        public static implicit operator bool(ConditionTask task)
        {
            return task.value;
        }
    }
}
