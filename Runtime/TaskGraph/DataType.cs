using System;
using System.Reflection;
using System.Collections.Generic;

using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;

using UnityEngine.Assertions;

namespace Unity.Kinematica
{
    /// <summary>
    /// Data types are identified by annotating a user-defined struct
    /// with the data attribute. All data types are collected at startup
    /// in the data type collection.
    /// </summary>
    /// <seealso cref="DataAttribute"/>
    public class DataType
    {
        /// <summary>
        /// Optional flags used to control data type behavior.
        /// </summary>
        public enum Flag
        {
            /// <summary>
            /// Default data type flag.
            /// </summary>
            None,

            /// <summary>
            /// Indicates that the corresponding data type should
            /// show individual array elements as distinct nodes
            /// in the task graph view.
            /// </summary>
            ExpandArray,

            /// <summary>
            /// Indicates that the children of a data type should
            /// be topologically sorted upon execution.
            /// </summary>
            TopologySort
        }

        internal class Field
        {
            public string name;
            public FieldInfo info;
            public Type type;
            public int offset;

            public static Field Create(FieldInfo info, Type type, string name)
            {
                var offset =
                    UnsafeUtility.GetFieldOffset(info);

                return new Field
                {
                    info = info,
                    name = name,
                    type = type,
                    offset = offset
                };
            }
        }

        internal Type type;

        internal Flag flag;

        internal int hashCode;

        internal ExecuteFunction executeFunction;
        internal IntPtr debugDrawMethod;

        internal Field[] inputFields;
        internal Field[] outputFields;
        internal Field[] propertyFields;

        internal int numInputFields => inputFields.Length;
        internal int numOutputFields => outputFields.Length;
        internal int numPropertyFields => propertyFields.Length;

        internal bool executable => executeFunction.IsValid;

        internal static bool IsGenericType(Type type, Type genericType)
        {
            return (type.IsGenericType && type.GetGenericTypeDefinition() == genericType);
        }

        DataType(Type type)
        {
            Assert.IsTrue(HasDataAttribute(type));

            if (!UnsafeUtility.IsUnmanaged(type) || !UnsafeUtility.IsBlittable(type))
            {
                throw new InvalidOperationException(
                    $"Data type {type.FullName} must be unmanaged and blittable.");
            }

            if (ImplementsTaskInterface(type))
            {
                executeFunction = ExecuteFunction.CompileStaticMemberFunction(type);

                Assert.IsTrue(executeFunction.IsValid);
            }
            else
            {
                debugDrawMethod = IntPtr.Zero;
            }

            var fields = type.GetFields(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            var inputFields = new List<Field>();
            var outputFields = new List<Field>();
            var propertyFields = new List<Field>();

            foreach (var field in fields)
            {
                string name = field.Name;

                var inputAttribute = field.GetCustomAttribute<InputAttribute>();

                if (inputAttribute != null)
                {
                    if (!IsGenericType(field.FieldType, typeof(Identifier<>)))
                    {
                        throw new InvalidOperationException(
                            $"Input field {inputAttribute.name} in data type {type.FullName} must be of of type {nameof(MemoryIdentifier)}.");
                    }

                    var inputType = field.FieldType.GetGenericArguments()[0];

                    if (!string.IsNullOrEmpty(inputAttribute.name))
                    {
                        name = inputAttribute.name;
                    }

                    inputFields.Add(Field.Create(field, inputType, name));
                }

                var outputAttribute = field.GetCustomAttribute<OutputAttribute>();

                if (outputAttribute != null)
                {
                    if (!IsGenericType(field.FieldType, typeof(Identifier<>)))
                    {
                        throw new InvalidOperationException(
                            $"Output field {outputAttribute.name} in data type {type.FullName} must be of of type {nameof(MemoryIdentifier)}.");
                    }

                    var outputType = field.FieldType.GetGenericArguments()[0];

                    if (!string.IsNullOrEmpty(outputAttribute.name))
                    {
                        name = outputAttribute.name;
                    }

                    outputFields.Add(Field.Create(field, outputType, name));
                }

                var propertyAttribute = field.GetCustomAttribute<PropertyAttribute>();

                if (propertyAttribute != null)
                {
                    if (!string.IsNullOrEmpty(propertyAttribute.name))
                    {
                        name = propertyAttribute.name;
                    }

                    propertyFields.Add(Field.Create(field, field.FieldType, name));
                }
            }

            if (!executeFunction.IsValid)
            {
                if (inputFields.Count > 0 || outputFields.Count > 0)
                {
                    throw new InvalidOperationException(
                        $"Type {type.FullName} needs to be executable in order to specify input/output fields.");
                }
            }

            hashCode = BurstRuntime.GetHashCode32(type);

            flag = DataAttribute.Flag(type);

            this.type = type;

            this.inputFields = inputFields.ToArray();
            this.outputFields = outputFields.ToArray();
            this.propertyFields = propertyFields.ToArray();
        }

        static bool ImplementsTaskInterface(Type type)
        {
            return typeof(Task).IsAssignableFrom(type);
        }

        static bool HasDataAttribute(Type type)
        {
            return type.GetCustomAttributes(typeof(DataAttribute), true).Length > 0;
        }

        internal static DataType Create(Type type)
        {
            return new DataType(type);
        }

        static DataType[] types;

        internal static DataType GetDataType(Type type)
        {
            foreach (var dataType in types)
            {
                if (dataType.type == type)
                {
                    return dataType;
                }
            }

            return null;
        }

        internal static DataType[] Types
        {
            get
            {
                if (types == null)
                {
                    var dataTypes = new List<DataType>();

                    foreach (var type in GetAllTypes())
                    {
                        if (HasDataAttribute(type))
                        {
                            dataTypes.Add(Create(type));
                        }
                    }

                    types = dataTypes.ToArray();
                }

                return types;
            }
        }

        static IEnumerable<Type> GetAllTypes()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type type in assembly.GetTypes())
                {
                    if (!type.IsAbstract)
                    {
                        yield return type;
                    }
                }
            }
        }
    }
}
