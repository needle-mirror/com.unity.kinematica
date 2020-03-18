using UnityEditor;
using UnityEngine;

namespace Unity.Kinematica.Editor
{
    internal class SerializableGuidUtility
    {
        public static SerializableGuid GetSerializableGuidFromAssetPath(string assetPath)
        {
            string assetGuidStr = AssetDatabase.AssetPathToGUID(assetPath);
            SerializableGuid assetGuid = new SerializableGuid();
            assetGuid.SetGuidStr(assetGuidStr);

            return assetGuid;
        }

        public static SerializableGuid GetSerializableGuidFromAsset(Object asset)
        {
            string assetPath = AssetDatabase.GetAssetPath(asset);
            return GetSerializableGuidFromAssetPath(assetPath);
        }
    }
}
