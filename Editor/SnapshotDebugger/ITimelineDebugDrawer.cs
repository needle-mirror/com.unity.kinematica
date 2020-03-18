using UnityEngine;

namespace Unity.SnapshotDebugger.Editor
{
    internal interface ITimelineDebugDrawer
    {
        void Draw(Rect rect, TimelineWidget.DrawInfo drawInfo);
        void DrawTooltip();
        float GetHeight();
    }
}
