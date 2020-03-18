using System;

namespace Unity.SnapshotDebugger.Editor
{
    // Code taken from UnityEngine.Timeline
    internal static class TimelineUtility
    {
        // chosen because it will cause no rounding errors between time/frames for frames values up to at least 10 million
        public static readonly double kTimeEpsilon = 1e-14;

        static void ValidateFrameRate(double frameRate)
        {
            if (frameRate <= kTimeEpsilon)
                throw new ArgumentException("frame rate cannot be 0 or negative");
        }

        public static double RoundToFrame(double time, double frameRate)
        {
            ValidateFrameRate(frameRate);

            var frameBefore = (int)Math.Floor(time * frameRate) / frameRate;
            var frameAfter = (int)Math.Ceiling(time * frameRate) / frameRate;

            return Math.Abs(time - frameBefore) < Math.Abs(time - frameAfter) ? frameBefore : frameAfter;
        }

        public static string TimeToTimeFrameStr(float time, int frameRate)
        {
            int frameDigits = frameRate != 0 ? (frameRate - 1).ToString().Length : 1;
            return time.ToString("0.00") + ":" + ((int)Math.Abs(time * frameRate)).ToString().PadLeft(frameDigits, '0');
        }
    }
}
