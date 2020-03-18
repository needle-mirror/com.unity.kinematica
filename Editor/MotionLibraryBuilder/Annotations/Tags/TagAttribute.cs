using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using ColorUtility = Unity.SnapshotDebugger.ColorUtility;

namespace Unity.Kinematica.Editor
{
    [AttributeUsage(AttributeTargets.Struct)]
    public class TagAttribute : Attribute
    {
        public const string k_UnknownTagType = "Unknown Tag Type";
        Color color;
        string displayName;

        public TagAttribute(string displayName, string color)
        {
            this.color = ColorUtility.FromHtmlString(color);
            this.displayName = displayName;
        }

        public static bool IsTagType(Type type)
        {
            Assert.IsTrue(type != null);

            return HasTagAttribute(type) && PayloadUtilities.ImplementsPayloadInterface(type);
        }

        public static bool HasTagAttribute(Type type)
        {
            return type.GetCustomAttributes(typeof(TagAttribute), true).Length > 0;
        }

        public static Color GetColor(Type type)
        {
            if (type == null)
            {
                return Color.gray;
            }

            TagAttribute tagAttribute =
                type.GetCustomAttributes(typeof(TagAttribute),
                    true).FirstOrDefault() as TagAttribute;

            return tagAttribute.color;
        }

        static List<Type> k_TypesCache;
        static List<Type> k_VisibleTypesCache;

        public static IEnumerable<Type> GetTypes()
        {
            return k_TypesCache ?? (k_TypesCache = AttributeCache<TagAttribute>.PopulateTypes());
        }

        public static IEnumerable<Type> GetVisibleTypesInInspector()
        {
            if (k_VisibleTypesCache == null)
            {
                k_VisibleTypesCache = new List<Type>();

                foreach (Type type in GetTypes())
                {
                    if (type.GetCustomAttributes(typeof(HideInInspector), true).Length == 0)
                    {
                        k_VisibleTypesCache.Add(type);
                    }
                }
            }

            return k_VisibleTypesCache;
        }

        public static Type TypeFromName(string name)
        {
            foreach (Type type in GetTypes())
            {
                if (GetDescription(type) == name)
                {
                    return type;
                }
            }

            return null;
        }

        static Dictionary<string, Type> k_PayloadArgTypeToTagType;

        public static Type FindTypeByPayloadArgumentType(Type payloadArgType)
        {
            if (k_PayloadArgTypeToTagType == null)
            {
                k_PayloadArgTypeToTagType = new Dictionary<string, Type>();
                foreach (Type type in GetTypes())
                {
                    Type argType = PayloadUtilities.GenericArgumentTypeFromTagInterface(type);
                    if (k_PayloadArgTypeToTagType.ContainsKey(argType.Name))
                    {
                        // log error that two tag types have the same payload argument
                        continue;
                    }


                    k_PayloadArgTypeToTagType.Add(argType.Name, type);
                }
            }

            Type tagType = null;
            k_PayloadArgTypeToTagType.TryGetValue(payloadArgType.FullName, out tagType);
            return tagType;
        }

        public static string GetDescription(Type type)
        {
            if (type == null)
            {
                return k_UnknownTagType;
            }

            var attributes = (TagAttribute[])
                type.GetCustomAttributes(typeof(TagAttribute), false);

            if (attributes.Length == 0)
            {
                return type.Name;
            }

            return attributes[0].displayName;
        }

        static List<string> m_Descriptions;
        public static List<string> GetAllDescriptions()
        {
            if (m_Descriptions == null)
            {
                m_Descriptions = new List<string>();
                foreach (var type in GetVisibleTypesInInspector())
                {
                    m_Descriptions.Add(GetDescription(type));
                }
            }
            return m_Descriptions;
        }
    }
}
