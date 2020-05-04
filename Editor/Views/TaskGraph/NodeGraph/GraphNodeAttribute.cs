using System;
using System.Collections.Generic;

namespace Unity.Kinematica.Editor
{
    public class GraphNodeAttribute : Attribute
    {
        Type type;

        public GraphNodeAttribute(Type type)
        {
            this.type = type;
        }

        public static Type Type(Type type)
        {
            var attribute = GetAttribute(type);

            if (attribute == null)
            {
                return null;
            }

            return attribute.type;
        }

        static GraphNodeAttribute GetAttribute(Type type)
        {
            var attributes =
                type.GetCustomAttributes(
                    typeof(GraphNodeAttribute), false);

            if (attributes.Length == 0)
            {
                return null;
            }

            return attributes[0] as GraphNodeAttribute;
        }

        static Dictionary<Type, Type> types;

        public static Type GetType(Type sourceType)
        {
            if (types == null)
            {
                types = new Dictionary<Type, Type>();

                foreach (var nodeType in GetAllTypes())
                {
                    var taskType = Type(nodeType);

                    if (taskType != null)
                    {
                        if (!nodeType.IsSubclassOf(typeof(GraphNode)))
                        {
                            throw new InvalidOperationException(
                                $"Type {nodeType.FullName} needs to inherit from {typeof(GraphNode).FullName}.");
                        }

                        types[taskType] = nodeType;
                    }
                }
            }

            Type targetType;

            if (types.TryGetValue(sourceType, out targetType))
            {
                return targetType;
            }

            return null;
        }

        static IEnumerable<Type> GetAllTypes()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type type in SnapshotDebugger.ReflectionUtility.GetTypesFromAssembly(assembly))
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
