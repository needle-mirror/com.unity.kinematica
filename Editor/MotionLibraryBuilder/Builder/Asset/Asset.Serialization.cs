using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace Unity.Kinematica.Editor
{
    [Serializable]
    internal partial class Asset : ScriptableObject, ISerializationCallbackReceiver
    {
        #region deprecatedV0

        [Obsolete("animationLibrary is now stored in AssetData. Use AnimationLibrary property instead", false)]
        [SerializeField]
        List<TaggedAnimationClip> animationLibrary = new List<TaggedAnimationClip>();

        [Obsolete("destinationAvatar is now stored in AssetData. Use DestinationAvatar property instead", false)]
        [FormerlySerializedAs("targetAvatar")]
        [SerializeField]
        Avatar destinationAvatar;

        [Obsolete("metrics is now stored in AssetData. Use Metrics property instead", false)]
        [FormerlySerializedAs("tagSettings")]
        internal List<Metric> metrics = new List<Metric>();

        [Obsolete("timeHorizon is now stored in AssetData. Use TimeHorizon property instead", false)]
        [Range(0.0f, 5.0f)]
        public float timeHorizon = 1.0f;

        [Obsolete("sampleRate is now stored in AssetData. Use SampleRate property instead", false)]
        [Range(0.0f, 120.0f)]
        public float sampleRate = 30.0f;

        #endregion //deprecatedV0

        const uint kAssetRewriteVersion = 1;
        const uint kCurrentSerializeVersion = 1;
        internal const string k_DefaultMetricName = "Default";
        internal const string k_MetricsPropertyPath = "m_Data.metrics";
        internal const string k_SampleRatePropertyPath = "m_Data.sampleRate";
        internal const string k_TimeHorizonPropertyPath = "m_Data.timeHorizon";
        internal const string k_MetricsCountPropertyPath = "m_Data.metricCount";

        [SerializeField]
        AssetData m_Data;

        [SerializeField]
        uint m_SerializeVersion;

        Asset()
        {
            m_Data = AssetDataFactory.Produce();
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            m_SerializeVersion = kCurrentSerializeVersion;
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (m_SerializeVersion < kAssetRewriteVersion)
            {
                ConvertToV1();
            }

            EnsureUniqueMetricNames();


            //Notify Listeners that the asset was deserialized
            EditorApplication.delayCall += () => { AssetWasDeserialized?.Invoke(this); };
        }

        internal void OnSave()
        {
            AssetWasDeserialized?.Invoke(this);
        }

        private void EnsureUniqueMetricNames()
        {
            var uniqueNames = new HashSet<string>();
            for (int i = 0; i < m_Data.metrics.Count; i++)
            {
                string name = m_Data.metrics[i].name;

                if (!uniqueNames.Add(name))
                {
                    string newName;
                    int count = 0;
                    do
                    {
                        count++;
                        newName = string.Format("{0}{1}", name, count);
                    }
                    while (!uniqueNames.Add(newName));

                    var modified = m_Data.metrics[i];
                    modified.name = newName;
                    m_Data.metrics[i] = modified;
                }
            }
        }

        private void ConvertToV1()
        {
#pragma warning disable 612, 618
            m_Data = AssetDataFactory.ConvertToV1(animationLibrary, destinationAvatar, metrics, timeHorizon, sampleRate);
            animationLibrary = null;
            metrics = null;
            destinationAvatar = null;
            timeHorizon = -1;
            sampleRate = -1;
#pragma warning restore 612, 618
        }
    }
}
