using System;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Kinematica
{
    /// <summary>
    /// Result of a task execution.
    /// </summary>
    /// <seealso cref="Task"/>
    public enum Result
    {
        /// <summary>
        /// Denotes a successfull task execution.
        /// </summary>
        Success,

        /// <summary>
        /// Denotes a failed task execution.
        /// </summary>
        Failure,

        /// <summary>
        /// Denotes the fact that a task execution hasn't determined its final status yet.
        /// </summary>
        Running
    }

    /// <summary>
    /// Tasks represent executable code that runs as part of the
    /// motion synthesizer job and can control the pose selection process.
    /// </summary>
    /// <remarks>
    /// Tasks are arranged in a directed acyclic graph and automatically
    /// executed in topological order. Each task has access to the motion
    /// synthesizer and therefore has access to the runtime asset as well
    /// the runtime behavior of the synthesizer.
    /// <para>
    /// Tasks can have input/output references to data which allows
    /// for arbitrary complex structures to execute as part of the
    /// task framework.
    /// </para>
    /// </remarks>
    public interface Task
    {
        /// <summary>
        /// Execute method for this task.
        /// </summary>
        /// <remarks>
        /// The execute method will be called from the motion synthesizer job.
        /// </remarks>
        /// <returns>Result of the task execution.</returns>
        Result Execute();
    }

    /// <summary>
    /// Generic reference to a task.
    /// </summary>
    public unsafe struct TaskRef
    {
        internal static TaskRef CreateTaskRef<TaskType>(ref TaskType task) where TaskType : struct, Task
        {
            return new TaskRef()
            {
                ptr = UnsafeUtility.AddressOf<TaskType>(ref task)
            };
        }

        internal static TaskRef CreateFromPayload(byte* payload)
        {
            return new TaskRef()
            {
                ptr = (void*)payload
            };
        }

        /// <summary>
        /// Cast operation from a generic reference to a typed task reference.
        /// </summary>
        /// <returns>Typed reference to the task.</returns>
        public ref TaskType Cast<TaskType>() where TaskType : struct, Task
        {
            return ref UnsafeUtilityEx.AsRef<TaskType>(ptr);
        }

        [NativeDisableUnsafePtrRestriction]
        internal void* ptr;
    }

    internal unsafe struct ExecuteFunction
    {
        internal delegate Result ExecuteDelegate(ref TaskRef taskRef);
        internal FunctionPointer<ExecuteDelegate>  functionPointer;

        public static ExecuteFunction CompileStaticMemberFunction(Type taskType)
        {
            FunctionPointer<ExecuteDelegate> functionPointer = FunctionPointerUtility.CompileStaticMemberFunction<ExecuteDelegate>(taskType, "ExecuteSelf");
            return new ExecuteFunction()
            {
                functionPointer = functionPointer
            };
        }

        public bool IsValid
        {
            get { return FunctionPointerUtility.IsFunctionPointerValid(ref functionPointer); }
        }

        public Result Invoke(byte* taskPayload)
        {
            TaskRef taskRef = TaskRef.CreateFromPayload(taskPayload);
            return functionPointer.Invoke(ref taskRef);
        }
    }
}
