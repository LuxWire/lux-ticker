using System.Runtime.InteropServices;

namespace LuxTicker;

/// <summary>Device names for the Settings monitor list. Primary stays a null device in settings.</summary>
static class MonitorCatalog
{
    public static IReadOnlyList<string> Devices()
    {
        var found = new List<string>();
        NativeMethods.MonitorEnumProc callback = (monitor, _, _, _) =>
        {
            var info = new MonitorInfoEx { cbSize = Marshal.SizeOf<MonitorInfoEx>() };
            if (NativeMethods.GetMonitorInfoEx(monitor, ref info) && !string.IsNullOrWhiteSpace(info.szDevice))
                found.Add(info.szDevice.Trim());
            return true;
        };
        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
        GC.KeepAlive(callback);
        return found;
    }
}
