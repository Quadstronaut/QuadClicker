using QuadClicker.PInvoke;
using System.Runtime.InteropServices;

namespace QuadClicker.Core;

/// <summary>Wraps GetLastInputInfo to report how long the system has been idle.</summary>
internal static class IdleDetector
{
    internal static TimeSpan GetIdleTime()
    {
        var info = new NativeMethods.LASTINPUTINFO
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.LASTINPUTINFO>()
        };
        NativeMethods.GetLastInputInfo(ref info);
        uint idleMs = unchecked((uint)Environment.TickCount - info.dwTime);
        return TimeSpan.FromMilliseconds(idleMs);
    }
}
