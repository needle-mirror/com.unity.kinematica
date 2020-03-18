using System;
using UnityEngine;
using ColorUtility = Unity.SnapshotDebugger.ColorUtility;

namespace Unity.Kinematica
{
    /// <summary>
    /// Attribute used to annotate data types that are
    /// used as elements in the task graph.
    /// </summary>
    /// <remarks>
    /// <example>
    /// <code>
    /// [Data("CurrentPose", "#2A3756"), BurstCompile]
    /// public struct CurrentPoseTask : Task
    /// {
    ///     ...
    /// }
    /// </code>
    /// </example>
    /// </remarks>
    /// <seealso cref="DataType"/>
    [AttributeUsage(AttributeTargets.Struct)]
    public class DataAttribute : Attribute
    {
        Color color;
        string displayName;
        DataType.Flag flag;

        /// <summary>
        /// Construct a new data attribute with the name and flags passed as argument.
        /// </summary>
        /// <param name="displayName">Display name to be used for the data type.</param>
        /// <param name="flag">Flags that control the behavior of this data type.</param>
        public DataAttribute(string displayName, DataType.Flag flag = DataType.Flag.None)
        {
            this.displayName = displayName;
            this.flag = flag;
        }

        /// <summary>
        /// Constructs a new data attribute with the name and flags passed as argument.
        /// </summary>
        /// <param name="displayName">Display name to be used for the data type.</param>
        /// <param name="color">Color to be used for display purposes of this data type.</param>
        /// <param name="flag">Flags that control the behavior of this data type.</param>
        public DataAttribute(string displayName, string color, DataType.Flag flag = DataType.Flag.None)
        {
            this.color = ColorUtility.FromHtmlString(color);
            this.displayName = displayName;
            this.flag = flag;
        }

        internal static DataType.Flag Flag(Type type)
        {
            var attribute = GetAttribute(type);

            if (attribute == null)
            {
                return DataType.Flag.None;
            }

            return attribute.flag;
        }

        internal static string GetDescription(Type type)
        {
            var attribute = GetAttribute(type);

            if (attribute == null)
            {
                return type.Name;
            }

            return attribute.displayName;
        }

        internal static Color GetColor(Type type)
        {
            var attribute = GetAttribute(type);

            if (attribute == null)
            {
                return Color.gray;
            }

            return attribute.color;
        }

        static DataAttribute GetAttribute(Type type)
        {
            var attributes =
                type.GetCustomAttributes(
                    typeof(DataAttribute), false);

            if (attributes.Length == 0)
            {
                return null;
            }

            return attributes[0] as DataAttribute;
        }
    }
}
