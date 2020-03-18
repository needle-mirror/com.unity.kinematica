using UnityEngine.Assertions;
using Unity.Mathematics;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.SnapshotDebugger
{
    internal class FrameDebugger
    {
        public void AddFrameDebugProvider<T>(FrameDebugProvider provider) where T : FrameDebugRecord, new()
        {
            if (!m_Providers.Contains(provider))
            {
                m_Providers.Add(provider);
            }

            int providerIdentifier = provider.GetUniqueIdentifier();
            if (!m_Records.ContainsKey(providerIdentifier))
            {
                var debugRecord = new T();
                debugRecord.Init(providerIdentifier, provider.GetDisplayName());
                m_Records.Add(providerIdentifier, debugRecord);
            }
        }

        public void RemoveFrameDebugProvider(FrameDebugProvider provider)
        {
            if (m_Providers.Contains(provider))
            {
                m_Providers.Remove(provider);
                m_Records[provider.GetUniqueIdentifier()].NotifyProviderRemoved();

                RemoveObsoleteRecords();
            }
        }

        public void Update(float time, float deltaTime, float startTimeInSeconds)
        {
            float frameEndTime = time + deltaTime;
            foreach (FrameDebugProvider provider in m_Providers)
            {
                m_Records[provider.GetUniqueIdentifier()].UpdateRecordEntries(time, frameEndTime, provider);
            }

            foreach (FrameDebugRecord record in Records)
            {
                record.PruneFramesBeforeTimestamp(startTimeInSeconds);
            }

            RemoveObsoleteRecords();
        }

        public void Clear()
        {
            m_Providers.Clear();
            m_Records.Clear();
        }

        public IEnumerable<FrameDebugRecord> Records
        {
            get
            {
                foreach (KeyValuePair<int, FrameDebugRecord> pair in m_Records)
                {
                    yield return pair.Value;
                }
            }
        }

        public FrameDebugRecord GetRecord(int providerIdentifier)
        {
            return m_Records[providerIdentifier];
        }

        void RemoveObsoleteRecords()
        {
            List<int> obsoleteRecords = new List<int>();
            foreach (KeyValuePair<int, FrameDebugRecord> pair in m_Records)
            {
                if (pair.Value.IsObsolete)
                {
                    obsoleteRecords.Add(pair.Key);
                }
            }

            foreach (int record in obsoleteRecords)
            {
                m_Records.Remove(record);
            }
        }

        List<FrameDebugProvider>           m_Providers = new List<FrameDebugProvider>();
        Dictionary<int, FrameDebugRecord>  m_Records = new Dictionary<int, FrameDebugRecord>();
    }
}
