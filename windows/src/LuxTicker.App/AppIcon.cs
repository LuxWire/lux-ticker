using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LuxTicker;

static class AppIcon
{
    public const string ResourceName = "LuxTicker.Assets.LuxTicker.ico";

    public static byte[] Bytes()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("The Lux Wire icon is missing from the app.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    public static ImageSource WindowImage()
    {
        using var stream = new MemoryStream(Bytes());
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        BitmapSource best = decoder.Frames[0];
        foreach (var frame in decoder.Frames)
        {
            if (frame.PixelWidth > best.PixelWidth)
                best = frame;
        }

        if (best.CanFreeze)
            best.Freeze();
        return best;
    }

    public static IntPtr CreateHicon(int size)
    {
        var image = IcoCatalog.Best(IcoCatalog.Read(Bytes()), size);
        var handle = NativeMethods.CreateIconFromResourceEx(
            image.ImageBytes,
            (uint)image.ImageBytes.Length,
            true,
            0x00030000,
            image.Width,
            image.Height,
            0);
        if (handle == IntPtr.Zero)
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        return handle;
    }

    /// <summary>
    /// The same icon group Start and the taskbar use. LoadImage picks the matching frame.
    /// Falls back to the embedded ico when the exe resource is missing.
    /// </summary>
    public static IntPtr LoadShellIcon(int size)
    {
        var module = NativeMethods.GetModuleHandle(null);
        var handle = NativeMethods.LoadImage(module, new IntPtr(NativeMethods.IDI_APPLICATION), NativeMethods.IMAGE_ICON, size, size, 0);
        if (handle != IntPtr.Zero)
            return handle;
        return CreateHicon(size);
    }

    /// <summary>The same orange tile as the exe, at the frame this DPI asked for.</summary>
    public static IntPtr LoadTrayIcon(int size)
    {
        var image = IcoCatalog.Best(IcoCatalog.Read(Bytes()), size);
        var handle = NativeMethods.CreateIconFromResourceEx(
            image.ImageBytes,
            (uint)image.ImageBytes.Length,
            true,
            0x00030000,
            image.Width,
            image.Height,
            0);
        if (handle == IntPtr.Zero)
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        return handle;
    }
}

/// <summary>Assigns the Lux Wire icon to a window's title bar and taskbar button.</summary>
sealed class WindowIcons : IDisposable
{
    const int WM_SETICON = 0x0080;

    readonly IntPtr _hwnd;
    readonly IntPtr _small;
    readonly IntPtr _big;
    bool _disposed;

    WindowIcons(IntPtr hwnd, IntPtr small, IntPtr big)
    {
        _hwnd = hwnd;
        _small = small;
        _big = big;
    }

    public static WindowIcons Apply(Window window)
    {
        window.Icon = AppIcon.WindowImage();
        var hwnd = new WindowInteropHelper(window).Handle;
        var small = AppIcon.LoadShellIcon(SmallIconSize());
        var big = AppIcon.LoadShellIcon(LargeIconSize());
        if (hwnd != IntPtr.Zero)
        {
            NativeMethods.SendMessage(hwnd, WM_SETICON, IntPtr.Zero, small);
            NativeMethods.SendMessage(hwnd, WM_SETICON, new IntPtr(1), big);
        }

        return new WindowIcons(hwnd, small, big);
    }

    public static int SmallIconSize()
    {
        var size = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSMICON);
        return IconMetrics.Closest(size > 0 ? size : 16);
    }

    public static int LargeIconSize()
    {
        var size = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXICON);
        return IconMetrics.Closest(size > 0 ? size : 32);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        if (_hwnd != IntPtr.Zero)
        {
            NativeMethods.SendMessage(_hwnd, WM_SETICON, IntPtr.Zero, IntPtr.Zero);
            NativeMethods.SendMessage(_hwnd, WM_SETICON, new IntPtr(1), IntPtr.Zero);
        }

        if (_small != IntPtr.Zero)
            NativeMethods.DestroyIcon(_small);
        if (_big != IntPtr.Zero)
            NativeMethods.DestroyIcon(_big);
    }
}
