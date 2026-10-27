using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace LuxTicker;

/// <summary>Notification-area icon. The same orange LW tile as the exe, at the frame Windows asked for.</summary>
sealed class TrayIcon : IDisposable
{
    const uint NIM_ADD = 0;
    const uint NIM_DELETE = 2;
    const uint NIF_MESSAGE = 0x1;
    const uint NIF_ICON = 0x2;
    const uint NIF_TIP = 0x4;
    const int WM_LBUTTONDBLCLK = 0x0203;
    const int WM_RBUTTONUP = 0x0205;
    const int WM_CONTEXTMENU = 0x007B;
    const int NIN_BALLOONUSERCLICK = 0x0405;
    const uint NIM_MODIFY = 1;
    const uint NIF_INFO = 0x10;
    const uint NIIF_USER = 0x4;

    readonly IntPtr _hwnd;
    IntPtr _icon;
    readonly uint _callback;
    readonly Action _showMenu;
    readonly Action _openSettings;
    HwndSource? _hook;
    bool _added;
    bool _disposed;
    string? _balloonUrl;

    public TrayIcon(Window window, Action showMenu, Action openSettings)
    {
        _hwnd = new WindowInteropHelper(window).Handle;
        _showMenu = showMenu;
        _openSettings = openSettings;
        _icon = LoadIcon();
        _callback = NativeMethods.RegisterWindowMessage("LuxTickerTrayCallback");
        if (_hwnd == IntPtr.Zero || _callback == 0 || _icon == IntPtr.Zero)
            throw new InvalidOperationException("Could not create the tray icon.");

        var data = CreateData();
        _added = NativeMethods.Shell_NotifyIcon(NIM_ADD, ref data);
        if (!_added)
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Could not add the tray icon.");

        _hook = HwndSource.FromHwnd(_hwnd);
        _hook?.AddHook(WndProc);
    }

    public void Refresh()
    {
        if (_disposed || !_added)
            return;
        var next = LoadIcon();
        var previous = _icon;
        _icon = next;
        var data = CreateData();
        data.uFlags = NIF_ICON;
        NativeMethods.Shell_NotifyIcon(NIM_MODIFY, ref data);
        if (previous != IntPtr.Zero && previous != next)
            NativeMethods.DestroyIcon(previous);
    }

    public void SetTip(string tip)
    {
        if (_disposed || !_added)
            return;
        var data = CreateData();
        data.uFlags = NIF_TIP;
        data.szTip = Trim(string.IsNullOrWhiteSpace(tip) ? LuxWireEdition.ProductName : tip, 127);
        NativeMethods.Shell_NotifyIcon(NIM_MODIFY, ref data);
    }

    public void ShowBalloon(string title, string body, string url)
    {
        if (!LuxWireLinks.IsHttp(url) || _disposed || !_added)
            return;
        _balloonUrl = url;
        var data = CreateData();
        data.uFlags = NIF_INFO;
        data.szInfoTitle = Trim(title, 63);
        data.szInfo = Trim(body, 255);
        data.dwInfoFlags = NIIF_USER;
        data.hBalloonIcon = _icon;
        NativeMethods.Shell_NotifyIcon(NIM_MODIFY, ref data);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _hook?.RemoveHook(WndProc);
        _hook = null;
        if (_added && _hwnd != IntPtr.Zero)
        {
            var data = CreateData();
            NativeMethods.Shell_NotifyIcon(NIM_DELETE, ref data);
            _added = false;
        }

        if (_icon != IntPtr.Zero)
            NativeMethods.DestroyIcon(_icon);
    }

    NativeMethods.NotifyIconData CreateData()
    {
        var data = new NativeMethods.NotifyIconData
        {
            hWnd = _hwnd,
            uID = 1,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = _callback,
            hIcon = _icon,
            szTip = LuxWireEdition.ProductName + " — right-click for Settings",
            szInfo = "",
            szInfoTitle = ""
        };
        data.cbSize = Marshal.SizeOf<NativeMethods.NotifyIconData>();
        return data;
    }

    IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (_callback == 0 || msg != (int)_callback)
            return IntPtr.Zero;

        switch (lParam.ToInt32())
        {
            case WM_RBUTTONUP:
            case WM_CONTEXTMENU:
                NativeMethods.SetForegroundWindow(_hwnd);
                _showMenu();
                handled = true;
                break;
            case WM_LBUTTONDBLCLK:
                _openSettings();
                handled = true;
                break;
            case NIN_BALLOONUSERCLICK:
                OpenBalloon();
                handled = true;
                break;
        }

        return IntPtr.Zero;
    }

    void OpenBalloon()
    {
        var url = _balloonUrl;
        if (!LuxWireLinks.IsHttp(url) || url == null)
            return;
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Error("lux wire open " + ex.Message);
        }
    }

    IntPtr LoadIcon()
    {
        var dpi = _hwnd == IntPtr.Zero ? 96 : (int)NativeMethods.GetDpiForWindow(_hwnd);
        var size = IconMetrics.ForTrayDpi(dpi);
        return AppIcon.LoadTrayIcon(size);
    }

    static string Trim(string value, int max)
        => value.Length <= max ? value : value[..max];
}
