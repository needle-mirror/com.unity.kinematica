using System;

namespace Unity.Kinematica
{
    /// <summary>
    /// Attribute used to annotate task output fields.
    /// </summary>
    /// <remarks>
    /// Tasks can optionally have output properties that are
    /// written to during task execution. The output attribute
    /// is used for such properties.
    /// <example>
    /// <code>
    /// public struct CurrentPoseTask : Task
    /// {
    ///     [Output("Time Index")]
    ///     Identifier<SamplingTime> samplingTime;
    /// }
    /// </code>
    /// </example>
    /// </remarks>
    /// <seealso cref="Task"/>
    [AttributeUsage(AttributeTargets.Field)]
    public class OutputAttribute : Attribute
    {
        internal string name;

        /// <summary>
        /// Constructs an output attribute with the name passed as argument.
        /// </summary>
        /// <remarks>
        /// By default the property type name will be used for display purposes.
        /// Alternatively, an override name can be passed to the output attribute constructor.
        /// </remarks>
        /// <param name="name">Name that is to be used for the property.</param>
        public OutputAttribute(string name = null)
        {
            this.name = name;
        }
    }
}
