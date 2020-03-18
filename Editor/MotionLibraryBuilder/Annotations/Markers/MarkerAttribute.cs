using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using ColorUtility = Unity.SnapshotDebugger.ColorUtility;

namespace Unity.Kinematica.Editor
{
    [AttributeUsage(AttributeTargets.Struct)]
    public class MarkerAttribute : Attribute
    {
        public const string k_UnknownMarkerType = "Unknown Marker Type";
        Color color;
        string displayName;

        public MarkerAttribute(string displayName, string color)
        {
            this.color = ColorUtility.FromHtmlString(color);
            this.displayName = displayName;
        }

        public static bool IsMarkerType(Type type)
        {
            Assert.IsTrue(type != null);
            return HasMarkerAttribute(type) && PayloadUtilities.ImplementsPayloadInterface(type);
        }

        public static bool HasMarkerAttribute(Type type)
        {
            return type.GetCustomAttributes(typeof(MarkerAttribute), true).Length > 0;
        }

        public static Color GetColor(Type type)
        {
            if (type == null)
            {
                return Color.gray;
            }

            MarkerAttribute markerAttribute = type.GetCustomAttributes(typeof(MarkerAttribute), true).FirstOrDefault() as MarkerAttribute;
            return markerAttribute.color;
        }

        static List<Type> k_TypesCache;

        public static IEnumerable<Type> GetMarkerTypes()
        {
            return k_TypesCache ?? (k_TypesCache = AttributeCache<MarkerAttribute>.PopulateTypes());
        }

        public static string GetDescription(Type type)
        {
            if (type == null)
            {
                return k_UnknownMarkerType;
            }

            var attributes = (MarkerAttribute[])
                type.GetCustomAttributes(typeof(MarkerAttribute), false);

            if (attributes.Length == 0)
            {
                return type.Name;
            }

            return attributes[0].displayName;
        }
    }
}
