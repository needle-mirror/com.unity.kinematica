using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;

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

        // Unity AnimationClip can have inaccurate length when the clip is pretty long (noticeable for clip > 5 mins)
        internal static float ComputeAccurateClipDuration(AnimationClip clip)
        {
            var bindings = AnimationUtility.GetCurveBindings(clip);

            int maxNumKeys = 0;
            foreach (EditorCurveBinding binding in bindings)
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                int numKeys = curve.keys.Length;
                maxNumKeys = Mathf.Max(numKeys, maxNumKeys);
            }

            int numFrames = maxNumKeys - 1;
            if (numFrames <= 0)
            {
                return clip.length;
            }

            return numFrames / clip.frameRate;
        }
    }
}
