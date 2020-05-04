using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Unity.Kinematica.Editor
{
    internal static class Utility
    {
        internal static IEnumerable<MethodInfo> GetExtensionMethods(Type extendedType)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type type in SnapshotDebugger.ReflectionUtility.GetTypesFromAssembly(assembly))
                {
                    if (type.IsSealed && !type.IsGenericType && !type.IsNested)
                    {
                        foreach (var method in type.GetMethods(BindingFlags.Static |
                            BindingFlags.Public | BindingFlags.NonPublic))
                        {
                            if (method.IsDefined(typeof(ExtensionAttribute), false))
                            {
                                if (method.GetParameters()[0].ParameterType == extendedType)
                                {
                                    yield return method;
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
