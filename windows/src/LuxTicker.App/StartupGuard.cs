using System.Runtime.InteropServices;

namespace LuxTicker;

/// <summary>Makes a startup failure visible: full exception in startup.log, message on screen.</summary>
static class StartupGuard
{
    const uint MbOk = 0x00000000;
    const uint MbIconError = 0x00000010;
    const uint MbSetForeground = 0x00010000;

    static int _started;

    public static bool IsStarted => Volatile.Read(ref _started) == 1;

    public static void MarkStarted() => Volatile.Write(ref _started, 1);

    public static void Report(Exception exception)
    {
        try
        {
            StartupReport.Write(exception);
        }
        catch
        {
            // The dialog below is the remaining signal.
        }

        try
        {
            var message = string.IsNullOrWhiteSpace(exception.Message)
                ? exception.GetType().Name
                : exception.Message;
            MessageBoxW(
                IntPtr.Zero,
                LuxWireEdition.ProductName + " couldn't start.\n\n" + message + "\n\nDetails: %APPDATA%\\LuxTicker\\startup.log",
                LuxWireEdition.ProductName,
                MbOk | MbIconError | MbSetForeground);
        }
        catch
        {
            // No desktop session.
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "MessageBoxW")]
    static extern int MessageBoxW(IntPtr owner, string text, string caption, uint type);
}
