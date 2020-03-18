using System;
using System.Collections.Generic;

namespace Unity.SnapshotDebugger
{
    //
    // TODO: Remove the concept of a "FrameDebugProvider".
    //       The snapshot debugger should be able to receive
    //       calls that produce "time ranges" (i.e. this object
    //       was in this state) and "markers" (i.e. something happened).
    //

    public interface IFrameDebugInfo
    {
    }

    public interface FrameDebugProvider
    {
        int     GetUniqueIdentifier();

        string  GetDisplayName();
    }

    public interface FrameDebugProvider<T> : FrameDebugProvider where T : IFrameDebugInfo
    {
        List<T> GetFrameDebugInfo();
    }
}
