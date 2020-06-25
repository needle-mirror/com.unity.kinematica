namespace Unity.Kinematica
{
    public partial struct MotionSynthesizer
    {
        [System.Obsolete("Push() has been removed for clarity, please use PlayFirstSequence() instead. (UnityUpgradable) -> Unity.Kinematica.MotionSynthesizer.PlayFirstSequence(Unity.Kinematica.QueryResult)")]
        public void Push(QueryResult queryResult)
        {
            PlayFirstSequence(queryResult);
        }

        [System.Obsolete("Push() has been removed for clarity, please use PlayAtTime() instead. (UnityUpgradable) -> Unity.Kinematica.MotionSynthesizer.PlayAtTime(Unity.Kinematica.TimeIndex)")]
        public void Push(TimeIndex timeIndex)
        {
            PlayAtTime(timeIndex);
        }

        [System.Obsolete("Push() has been removed for clarity, please use PlayAtTime() instead. (UnityUpgradable) -> Unity.Kinematica.MotionSynthesizer.PlayAtTime(Unity.Kinematica.SamplingTime)")]
        public void Push(SamplingTime samplingTime)
        {
            PlayAtTime(samplingTime);
        }

        [System.Obsolete("GetByType() has been removed for clarity, please use GetChildByType() instead. (UnityUpgradable) -> Unity.Kinematica.MotionSynthesizer.GetChildByType<T>(Unity.Kinematica.MemoryIdentifier)")]
        public MemoryRef<T> GetByType<T>(MemoryIdentifier parent) where T : struct
        {
            return GetChildByType<T>(parent);
        }
    }
}
