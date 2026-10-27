using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace LuxTicker;

/// <summary>
/// Registers the ticker as a shell app bar (SHAppBarMessage) so maximized windows
/// leave a strip for it. Height is half the taskbar thickness in physical pixels.
/// </summary>
public sealed class AppBarController : IDisposable
{
    readonly Window _window;
    IntPtr _hwnd;
    HwndSource? _hook;
    uint _callback;
    uint _taskbarCreated;
    bool _registered;
    bool _fullscreen;
    bool _releasedForFullscreen;
    bool _applying;
    int _lastThickness;
    NativeRect _applied;
    NativeRect _target;
    NativeRect _shellWorkArea;
    bool _shellWorkAreaSaved;
    bool _workAreaClaimed;
    bool _loggedOverlap;
    bool _overTaskbar;
    bool _loggedWorkSet;
    NativeRect _loggedWork;
    DockEdge _dock = DockEdge.Bottom;
    readonly List<MonitorHit> _monitors = new();
    NativeMethods.MonitorEnumProc? _enum;
    NativeMethods.WinEventDelegate? _foregroundProc;
    IntPtr _foregroundHook;

    public AppBarController(Window window) => _window = window;

    public int BarHeightPx { get; private set; }

    /// <summary>0.5 is half the taskbar (compact). 1 is the full taskbar thickness (expanded).</summary>
    public double HeightFraction { get; set; } = 0.5;

    /// <summary>DIP offset from the dock baseline. 0 matches the 1.4.2 placement.</summary>
    public int VerticalOffsetDip { get; set; }

    public bool Topmost { get; set; } = true;

    public bool ClickThrough { get; set; }

    /// <summary>Screen pixels. True when the point is over Chart or Settings.</summary>
    public Func<int, int, bool>? ButtonHit { get; set; }

    public string TaskbarSource { get; private set; } = "";

    public string? PreferredDevice { get; set; }

    public string? ActiveDevice { get; private set; }

    /// <summary>Explorer restarted. The tray icon has to be added again.</summary>
    public event Action? ShellRestarted;

    /// <summary>DPI or display changed. The tray frame may need a different size.</summary>
    public event Action? DisplayChanged;

    public void Attach(DockEdge dock)
    {
        _dock = dock;
        _hwnd = new WindowInteropHelper(_window).Handle;
        _hook = HwndSource.FromHwnd(_hwnd);
        _hook?.AddHook(WndProc);

        var style = NativeMethods.GetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE);
        var updated = new IntPtr(DeskBand.Compose(style.ToInt64(), ClickThrough));
        NativeMethods.SetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE, updated);
        try
        {
            var desktop = NativeMethods.GetDesktopWindow();
            if (desktop != IntPtr.Zero)
                NativeMethods.SetWindowLongPtr(_hwnd, NativeMethods.GWLP_HWNDPARENT, desktop);
        }
        catch (Exception ex)
        {
            Log.Error("desktop parent " + ex.Message);
        }

        try
        {
            _foregroundProc = OnForeground;
            _foregroundHook = NativeMethods.SetWinEventHook(
                NativeMethods.EVENT_SYSTEM_FOREGROUND,
                NativeMethods.EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero,
                _foregroundProc,
                0,
                0,
                NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS);
        }
        catch (Exception ex)
        {
            Log.Error("foreground hook " + ex.Message);
        }

        try
        {
            _callback = NativeMethods.RegisterWindowMessage("LuxTickerAppBarCallback");
            _taskbarCreated = NativeMethods.RegisterWindowMessage("TaskbarCreated");
            var data = NativeMethods.NewAppBarData(_hwnd, _callback);
            var ok = NativeMethods.SHAppBarMessage(NativeMethods.ABM_NEW, ref data);
            _registered = ok != 0;
            if (!_registered)
                Log.Error("SHAppBarMessage ABM_NEW failed; positioning without shell reservation.");
            Apply();
        }
        catch (Exception ex)
        {
            Log.Error("appbar attach " + ex);
            if (!StartupGuard.IsStarted)
                StartupGuard.Report(ex);
            Reveal();
        }

        if (!_fullscreen && (_window.Visibility != Visibility.Visible || _window.Left < -1000))
            Reveal();
    }

    /// <summary>
    /// Puts the bar above the taskbar when shell docking fails. Must not throw.
    /// Uses the same placement as a successful dock, not the raw work-area bottom
    /// (that rect is the full screen on some Windows 11 sessions and slides under the taskbar).
    /// </summary>
    public void Reveal()
    {
        try
        {
            if (_hwnd != IntPtr.Zero)
            {
            var monitor = ChooseMonitor();
            var taskbar = MeasureTaskbar(monitor);
            var edge = _taskbarEdge;
            var autoHide = IsAutoHide();
            var height = HeightFor(taskbar);
            var placed = Shift(AppBarMath.Place(monitor, taskbar, edge, autoHide, _dock, height), monitor, taskbar, autoHide);
            MoveTo(ToNative(placed));
            ApplyChrome();
                _window.Visibility = Visibility.Visible;
                NativeMethods.ShowWindow(_hwnd, NativeMethods.SW_SHOWNOACTIVATE);
                EnsureWorkArea(AppBarMath.WorkArea(monitor, taskbar, edge, autoHide, _dock, placed));
                return;
            }

            var area = SystemParameters.WorkArea;
            var dipHeight = Math.Max(_window.Height, 23);
            _window.Width = Math.Max(200, area.Width);
            _window.Height = dipHeight;
            _window.Left = area.Left;
            _window.Top = _dock == DockEdge.Top ? area.Top : Math.Max(area.Top, area.Bottom - dipHeight);
            _window.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            Log.Error("appbar reveal " + ex.Message);
        }
    }

    public void SetDock(DockEdge dock)
    {
        _dock = dock;
        Apply();
    }

    public void Apply()
    {
        if (_hwnd == IntPtr.Zero || _applying)
            return;

        _applying = true;
        try
        {
            if (_fullscreen && !Topmost)
            {
                ReleaseForFullscreen();
                NativeMethods.ShowWindow(_hwnd, NativeMethods.SW_HIDE);
                _applied = default;
                return;
            }

            RestoreAfterFullscreen();

            var monitor = ChooseMonitor();
            var taskbar = MeasureTaskbar(monitor);
            var edge = _taskbarEdge;
            var autoHide = IsAutoHide();
            var height = HeightFor(taskbar);

            NativeRect rect = _registered
                ? ReserveWithShell(monitor, taskbar, edge, autoHide, height)
                : ToNative(Shift(AppBarMath.Place(monitor, taskbar, edge, autoHide, _dock, height), monitor, taskbar, autoHide));

            var windowMatches = NativeMethods.GetWindowRect(_hwnd, out var actual) && Same(rect, actual);
            if (!(Same(rect, _applied) && windowMatches && _window.Visibility == Visibility.Visible))
            {
                MoveTo(rect);
                if (_window.Visibility != Visibility.Visible)
                    _window.Visibility = Visibility.Visible;
                NativeMethods.ShowWindow(_hwnd, NativeMethods.SW_SHOWNOACTIVATE);

                var data = NativeMethods.NewAppBarData(_hwnd, _callback);
                if (_registered)
                    NativeMethods.SHAppBarMessage(NativeMethods.ABM_WINDOWPOSCHANGED, ref data);
            }

            var bar = PxRect.FromLtrb(rect.Left, rect.Top, rect.Right, rect.Bottom);
            EnsureWorkArea(AppBarMath.WorkArea(monitor, taskbar, edge, autoHide, _dock, bar));
            ApplyChrome();
        }
        catch (Exception ex)
        {
            Log.Error("appbar " + ex);
            if (!StartupGuard.IsStarted)
                StartupGuard.Report(ex);
            if (!_fullscreen && (_window.Visibility != Visibility.Visible || _window.Left < -1000))
                Reveal();
        }
        finally
        {
            _applying = false;
        }
    }

    public void Dispose()
    {
        try
        {
            if (_registered && _hwnd != IntPtr.Zero)
            {
                var data = NativeMethods.NewAppBarData(_hwnd, _callback);
                NativeMethods.SHAppBarMessage(NativeMethods.ABM_REMOVE, ref data);
                _registered = false;
            }
        }
        catch (Exception ex)
        {
            Log.Error("appbar remove " + ex.Message);
        }

        RestoreWorkArea();

        if (_foregroundHook != IntPtr.Zero)
        {
            try
            {
                NativeMethods.UnhookWinEvent(_foregroundHook);
            }
            catch (Exception ex)
            {
                Log.Error("foreground unhook " + ex.Message);
            }

            _foregroundHook = IntPtr.Zero;
        }

        if (_hook != null)
        {
            _hook.RemoveHook(WndProc);
            _hook = null;
        }
    }

    NativeRect ReserveWithShell(PxRect monitor, PxRect taskbar, TaskbarEdge edge, bool autoHide, int height)
    {
        // Propose the band above the taskbar. QUERYPOS on Windows 11 rewrites a bottom
        // app bar to the screen bottom, which is inside the taskbar, and the topmost
        // taskbar then covers it. SETPOS must reserve our band, and the window must
        // move there even when the shell echoes the screen edge back.
        var ideal = AppBarMath.Place(monitor, taskbar, edge, autoHide, _dock, height);
        var data = NativeMethods.NewAppBarData(_hwnd, _callback);
        data.uEdge = (uint)(_dock == DockEdge.Top ? NativeMethods.ABE_TOP : NativeMethods.ABE_BOTTOM);
        data.rc = ToNative(ideal);
        NativeMethods.SHAppBarMessage(NativeMethods.ABM_QUERYPOS, ref data);

        var suggested = FromNative(data.rc);
        var resolved = AppBarMath.Resolve(monitor, taskbar, edge, autoHide, _dock, height, suggested);
        if (!_loggedOverlap && !autoHide && AppBarMath.Overlaps(suggested, taskbar))
        {
            _loggedOverlap = true;
            Log.Info($"appbar shell rect {suggested.X},{suggested.Y} {suggested.Width}x{suggested.Height} intersects the taskbar; reserving {resolved.X},{resolved.Y} {resolved.Width}x{resolved.Height}");
        }

        var placed = Shift(resolved, monitor, taskbar, autoHide);
        data.rc = ToNative(placed);
        NativeMethods.SHAppBarMessage(NativeMethods.ABM_SETPOS, ref data);
        var echoed = AppBarMath.Resolve(monitor, taskbar, edge, autoHide, _dock, height, FromNative(data.rc));
        return ToNative(Shift(echoed, monitor, taskbar, autoHide));
    }

    PxRect Shift(PxRect baseline, PxRect monitor, PxRect taskbar, bool autoHide)
    {
        var placed = AppBarMath.WithVerticalOffset(baseline, _dock, OffsetPx(), monitor);
        _overTaskbar = BarOffset.Clamp(VerticalOffsetDip) != 0 && !autoHide && AppBarMath.Overlaps(placed, taskbar);
        return placed;
    }

    int OffsetPx()
    {
        if (_hwnd == IntPtr.Zero)
            return 0;
        uint dpi = NativeMethods.GetDpiForWindow(_hwnd);
        if (dpi < 96)
            dpi = 96;
        return AppBarMath.DipToPhysical(BarOffset.Clamp(VerticalOffsetDip), dpi);
    }

    int HeightFor(PxRect taskbar)
    {
        var thickness = AppBarMath.Thickness(taskbar);
        if (thickness >= 20)
            _lastThickness = thickness;
        var useThickness = _lastThickness > 0 ? _lastThickness : thickness > 0 ? thickness : AppBarMath.FallbackTaskbarThicknessPx;
        var height = HeightFraction >= 0.99
            ? Math.Max(1, useThickness)
            : AppBarMath.TickerHeightPx(useThickness);
        if (height < 16)
            height = 16;
        BarHeightPx = height;
        return height;
    }

    bool _recovering;
    bool _placementLogged;

    void ApplyChrome()
    {
        if (_hwnd == IntPtr.Zero)
            return;

        // Opacity below 1 makes this borderless window layered. Rewriting the extended style
        // then leaves it layered with no paint, so the strip is invisible while Settings (a normal window) still opens.
        try
        {
            _window.Opacity = BarVisibility.WindowOpacity(1);
        }
        catch (Exception ex)
        {
            Log.Error("opacity " + ex.Message);
        }

        // DeskBand.Compose strips WS_EX_LAYERED. Opacity stays 1 so the strip still paints.
        var style = DeskBand.Compose(NativeMethods.GetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE).ToInt64(), ClickThrough);
        NativeMethods.SetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(style));
        try
        {
            int excludePeek = 1;
            NativeMethods.DwmSetWindowAttribute(_hwnd, NativeMethods.DWMWA_EXCLUDED_FROM_PEEK, ref excludePeek, sizeof(int));
        }
        catch (Exception ex)
        {
            Log.Error("peek " + ex.Message);
        }

        // Always on top is HWND_TOPMOST. The layered-window bug was opacity, not this z-order.
        // Click-through still adds WS_EX_TRANSPARENT only when that box is checked.
        var insert = IntPtr.Zero;
        if (Topmost || _overTaskbar)
            insert = NativeMethods.HwndTopmost;
        else
            insert = NativeMethods.HwndNotTopmost;
        NativeMethods.SetWindowPos(
            _hwnd,
            insert,
            0,
            0,
            0,
            0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_FRAMECHANGED | NativeMethods.SWP_SHOWWINDOW);
        BarSurface.Ensure(_window);
    }

    public bool IsOnScreen()
    {
        if (_window.Visibility != Visibility.Visible || _window.Opacity < 0.99)
            return false;
        if (_window.Width < 8 || _window.Height < 4)
            return false;
        if (_hwnd == IntPtr.Zero)
            return _window.Left > -1000 && _window.Top > -1000;

        if (!NativeMethods.GetWindowRect(_hwnd, out var native))
            return false;
        var rect = FromNative(native);
        var monitors = new List<PxRect>(_monitors.Count);
        foreach (var hit in _monitors)
            monitors.Add(hit.Rect);
        if (monitors.Count == 0)
            monitors.Add(PrimaryMonitor());
        return BarVisibility.IsOnScreen(rect, monitors);
    }

    /// <summary>
    /// Brings the bar back without changing the saved dock or vertical offset.
    /// A screenshot overlay sets the fullscreen flag and often never clears it.
    /// Returns true only when the strip was still off-screen and the dock was reset.
    /// </summary>
    public bool Heal()
    {
        if (_hwnd == IntPtr.Zero || _recovering || _applying)
            return false;

        try
        {
            if (_fullscreen && !ForegroundCoversMonitor())
            {
                Log.Info("fullscreen overlay gone; restoring ticker");
                _fullscreen = false;
            }

            var iconic = NativeMethods.IsIconic(_hwnd);
            var shown = NativeMethods.IsWindowVisible(_hwnd);
            if (Topmost && _fullscreen)
                _fullscreen = false;

            if (!_fullscreen && (iconic || !shown || _window.Visibility != Visibility.Visible || !IsOnScreen()))
            {
                if (iconic)
                    NativeMethods.ShowWindow(_hwnd, NativeMethods.SW_RESTORE);
                _applied = default;
                Apply();
            }
            else if (Topmost)
            {
                ApplyChrome();
            }
        }
        catch (Exception ex)
        {
            Log.Error("ticker heal " + ex.Message);
        }

        if (!_fullscreen && !IsOnScreen())
            return RecoverIfHidden();
        return false;
    }

    bool ForegroundCoversMonitor()
    {
        var fg = NativeMethods.GetForegroundWindow();
        if (fg == IntPtr.Zero || fg == _hwnd || !NativeMethods.GetWindowRect(fg, out var native))
            return false;
        return BarRecovery.Covers(ChooseMonitor(), FromNative(native));
    }

    /// <summary>Puts a hidden or off-screen strip above the taskbar on the primary monitor. Returns true when it had to move.</summary>
    public bool RecoverIfHidden()
    {
        if (_recovering || _fullscreen || _hwnd == IntPtr.Zero || IsOnScreen())
            return false;

        _recovering = true;
        try
        {
            StartupReport.Note("ticker hidden " + Describe() + " — placing above the taskbar on the primary monitor");
            PreferredDevice = null;
            ActiveDevice = null;
            _dock = DockEdge.Bottom;
            _applied = default;
            _target = default;
            Apply();
            if (!IsOnScreen())
                Reveal();
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("ticker recover " + ex.Message);
            Reveal();
            return true;
        }
        finally
        {
            _recovering = false;
        }
    }

    public void LogPlacement()
    {
        if (_placementLogged)
            return;
        _placementLogged = true;
        StartupReport.Note("ticker " + Describe());
    }

    public string Describe()
    {
        var rect = "unknown";
        if (_hwnd != IntPtr.Zero && NativeMethods.GetWindowRect(_hwnd, out var native))
        {
            int width = native.Right - native.Left;
            int height = native.Bottom - native.Top;
            rect = native.Left + "," + native.Top + " " + width + "x" + height;
        }

        return "visible=" + _window.Visibility
            + " opacity=" + _window.Opacity.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
            + " dock=" + _dock
            + " monitor=" + (string.IsNullOrWhiteSpace(ActiveDevice) ? "(primary)" : ActiveDevice)
            + " rect=" + rect
            + " clickThrough=" + ClickThrough
            + " alwaysOnTop=" + Topmost
            + " taskbar=" + TaskbarSource
            + "/" + _taskbarEdge
            + " heightPx=" + BarHeightPx;
    }

    PxRect ChooseMonitor()
    {
        _monitors.Clear();
        _enum ??= OnMonitor;
        try
        {
            NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, _enum, IntPtr.Zero);
        }
        catch (Exception ex)
        {
            Log.Error("monitors " + ex.Message);
        }

        if (!string.IsNullOrWhiteSpace(PreferredDevice))
        {
            foreach (var hit in _monitors)
            {
                if (string.Equals(hit.Device, PreferredDevice, StringComparison.OrdinalIgnoreCase))
                {
                    ActiveDevice = hit.Device;
                    return hit.Rect;
                }
            }
        }

        foreach (var hit in _monitors)
        {
            if (!hit.Primary)
                continue;
            ActiveDevice = hit.Device;
            return hit.Rect;
        }

        if (_monitors.Count > 0)
        {
            ActiveDevice = _monitors[0].Device;
            return _monitors[0].Rect;
        }

        ActiveDevice = null;
        return PrimaryMonitor();
    }

    bool OnMonitor(IntPtr hMonitor, IntPtr hdc, IntPtr lprc, IntPtr data)
    {
        var info = new MonitorInfoEx { cbSize = Marshal.SizeOf<MonitorInfoEx>() };
        if (!NativeMethods.GetMonitorInfoEx(hMonitor, ref info))
            return true;
        var rect = PxRect.FromLtrb(info.rcMonitor.Left, info.rcMonitor.Top, info.rcMonitor.Right, info.rcMonitor.Bottom);
        if (rect.Width <= 0 || rect.Height <= 0 || string.IsNullOrWhiteSpace(info.szDevice))
            return true;
        _monitors.Add(new MonitorHit(info.szDevice, rect, (info.dwFlags & 1) != 0));
        return true;
    }

    readonly record struct MonitorHit(string Device, PxRect Rect, bool Primary);

    static NativeRect ToNative(PxRect rect) => new()
    {
        Left = rect.X,
        Top = rect.Y,
        Right = rect.Right,
        Bottom = rect.Bottom
    };

    static PxRect FromNative(NativeRect rect) => PxRect.FromLtrb(rect.Left, rect.Top, rect.Right, rect.Bottom);

    void MoveTo(NativeRect rect)
    {
        _target = rect;
        int width = Math.Max(1, rect.Right - rect.Left);
        int height = Math.Max(1, rect.Bottom - rect.Top);
        uint dpi = NativeMethods.GetDpiForWindow(_hwnd);
        if (dpi < 96)
            dpi = 96;
        double scale = dpi / 96.0;

        _window.Left = rect.Left / scale;
        _window.Top = rect.Top / scale;
        _window.Width = width / scale;
        _window.Height = height / scale;

        var insert = ZOrder.InsertAfter(Topmost, _overTaskbar) < 0 ? NativeMethods.HwndTopmost : IntPtr.Zero;
        NativeMethods.SetWindowPos(
            _hwnd,
            insert,
            rect.Left,
            rect.Top,
            width,
            height,
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);

        if (rect.Left != _applied.Left || rect.Top != _applied.Top || rect.Right != _applied.Right || rect.Bottom != _applied.Bottom)
        {
            _applied = rect;
            Log.Info($"placed dock={_dock} rect={rect.Left},{rect.Top} {width}x{height} dpi={dpi} taskbarPx={_lastThickness} barPx={BarHeightPx} appbar={_registered}");
        }
    }

    void ReleaseForFullscreen()
    {
        if (_registered)
        {
            var data = NativeMethods.NewAppBarData(_hwnd, _callback);
            NativeMethods.SHAppBarMessage(NativeMethods.ABM_REMOVE, ref data);
            _registered = false;
            _releasedForFullscreen = true;
        }

        RestoreWorkArea();
    }

    void RestoreAfterFullscreen()
    {
        if (!_releasedForFullscreen)
            return;
        var data = NativeMethods.NewAppBarData(_hwnd, _callback);
        var ok = NativeMethods.SHAppBarMessage(NativeMethods.ABM_NEW, ref data);
        _registered = ok != 0;
        _releasedForFullscreen = false;
    }

    static bool Same(NativeRect a, NativeRect b)
        => a.Left == b.Left && a.Top == b.Top && a.Right == b.Right && a.Bottom == b.Bottom;

    IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_SYSCOMMAND && (wParam.ToInt32() & 0xFFF0) == NativeMethods.SC_MINIMIZE)
        {
            handled = true;
            if (NativeMethods.IsIconic(hwnd))
                NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);
            ApplyChrome();
            return IntPtr.Zero;
        }

        if (msg == NativeMethods.WM_NCHITTEST && ClickThrough)
        {
            var packed = lParam.ToInt32();
            int x = unchecked((short)(packed & 0xFFFF));
            int y = unchecked((short)((packed >> 16) & 0xFFFF));
            var overButtons = ButtonHit != null && ButtonHit(x, y);
            handled = true;
            return new IntPtr(ClickThroughHit.Code(true, overButtons));
        }

        if (msg == NativeMethods.WM_WINDOWPOSCHANGING && !(_fullscreen && !Topmost) && _target.Right > _target.Left)
            KeepAboveTaskbar(lParam);

        if (_callback != 0 && msg == (int)_callback)
        {
            switch (wParam.ToInt32())
            {
                case NativeMethods.ABN_STATECHANGE:
                case NativeMethods.ABN_POSCHANGED:
                    Apply();
                    handled = true;
                    break;
                case NativeMethods.ABN_FULLSCREENAPP:
                    if (Topmost)
                    {
                        _fullscreen = false;
                        Log.Info(lParam != IntPtr.Zero
                            ? "fullscreen app; ticker stays on top"
                            : "fullscreen app closed; ticker stays on top");
                        ApplyChrome();
                    }
                    else
                    {
                        _fullscreen = lParam != IntPtr.Zero;
                        Log.Info(_fullscreen ? "fullscreen app; hiding ticker" : "fullscreen app closed; restoring ticker");
                        Apply();
                    }
                    handled = true;
                    break;
            }
        }
        else if (_taskbarCreated != 0 && msg == (int)_taskbarCreated)
        {
            Log.Info("taskbar created; re-registering the bar");
            try
            {
                if (_registered)
                {
                    var remove = NativeMethods.NewAppBarData(_hwnd, _callback);
                    NativeMethods.SHAppBarMessage(NativeMethods.ABM_REMOVE, ref remove);
                    _registered = false;
                }

                var created = NativeMethods.NewAppBarData(_hwnd, _callback);
                _registered = NativeMethods.SHAppBarMessage(NativeMethods.ABM_NEW, ref created) != 0;
                _fullscreen = false;
                _releasedForFullscreen = false;
                _applied = default;
                Apply();
                ShellRestarted?.Invoke();
            }
            catch (Exception ex)
            {
                Log.Error("taskbar created " + ex.Message);
            }
        }
        else if (msg is NativeMethods.WM_DISPLAYCHANGE or NativeMethods.WM_DPICHANGED or NativeMethods.WM_SETTINGCHANGE)
        {
            Apply();
            DisplayChanged?.Invoke();
        }

        return IntPtr.Zero;
    }

    void KeepAboveTaskbar(IntPtr lParam)
    {
        try
        {
            var pos = Marshal.PtrToStructure<WindowPos>(lParam);
            var locked = DeskBand.Lock(
                pos.flags,
                pos.x,
                pos.y,
                pos.cx,
                pos.cy,
                _target.Left,
                _target.Top,
                _target.Right - _target.Left,
                _target.Bottom - _target.Top);
            pos.flags = locked.Flags;
            pos.x = locked.X;
            pos.y = locked.Y;
            pos.cx = locked.Cx;
            pos.cy = locked.Cy;
            Marshal.StructureToPtr(pos, lParam, false);
        }
        catch (Exception ex)
        {
            Log.Error("appbar position " + ex.Message);
        }
    }

    void EnsureWorkArea(PxRect desired)
    {
        if (desired.Width <= 0 || desired.Height <= 0 || _hwnd == IntPtr.Zero)
            return;

        var hmon = NativeMethods.MonitorFromWindow(_hwnd, NativeMethods.MONITOR_DEFAULTTOPRIMARY);
        var info = new MonitorInfo { cbSize = Marshal.SizeOf<MonitorInfo>() };
        if (hmon == IntPtr.Zero || !NativeMethods.GetMonitorInfo(hmon, ref info))
            return;

        var current = FromNative(info.rcWork);
        // Shrink from the shell work area, and expand back to it when the offset returns.
        // Never push past that saved area into space the shell already gave the taskbar.
        var limit = _shellWorkAreaSaved ? FromNative(_shellWorkArea) : current;
        int left = Math.Max(desired.X, limit.X);
        int top = Math.Max(desired.Y, limit.Y);
        int right = Math.Min(desired.Right, limit.Right);
        int bottom = Math.Min(desired.Bottom, limit.Bottom);
        if (right <= left || bottom <= top)
            return;
        if (left == current.X && top == current.Y && right == current.Right && bottom == current.Bottom)
            return;

        if (!_shellWorkAreaSaved)
        {
            _shellWorkArea = info.rcWork;
            _shellWorkAreaSaved = true;
        }

        var rect = new NativeRect { Left = left, Top = top, Right = right, Bottom = bottom };
        if (!NativeMethods.SystemParametersInfo(NativeMethods.SPI_SETWORKAREA, 0, ref rect, NativeMethods.SPIF_SENDCHANGE))
        {
            Log.Error("work area reserve failed");
            return;
        }

        _workAreaClaimed = true;
        if (!_loggedWorkSet || !Same(rect, _loggedWork))
        {
            _loggedWork = rect;
            _loggedWorkSet = true;
            Log.Info($"work area {left},{top} {right - left}x{bottom - top}");
        }
    }

    void RestoreWorkArea()
    {
        if (!_workAreaClaimed)
            return;

        try
        {
            NativeRect rect;
            var monitor = _hwnd != IntPtr.Zero ? PrimaryMonitor() : default;
            var taskbar = MeasureTaskbar(monitor);
            if (monitor.Width > 0 && taskbar.Width > 0 && taskbar.Height > 0)
            {
                var edge = _taskbarEdge;
                var natural = AppBarMath.WorkArea(monitor, taskbar, edge, IsAutoHide(), DockEdge.Bottom, default);
                rect = ToNative(natural);
            }
            else if (_shellWorkAreaSaved)
            {
                rect = _shellWorkArea;
            }
            else
            {
                _workAreaClaimed = false;
                return;
            }

            NativeMethods.SystemParametersInfo(NativeMethods.SPI_SETWORKAREA, 0, ref rect, NativeMethods.SPIF_SENDCHANGE);
        }
        catch (Exception ex)
        {
            Log.Error("work area restore " + ex.Message);
        }

        _workAreaClaimed = false;
    }

    PxRect PrimaryMonitor()
    {
        var hmon = NativeMethods.MonitorFromWindow(_hwnd, NativeMethods.MONITOR_DEFAULTTOPRIMARY);
        var info = new MonitorInfo { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MonitorInfo>() };
        if (hmon != IntPtr.Zero && NativeMethods.GetMonitorInfo(hmon, ref info))
            return PxRect.FromLtrb(info.rcMonitor.Left, info.rcMonitor.Top, info.rcMonitor.Right, info.rcMonitor.Bottom);

        return new PxRect(0, 0, 1920, 1080);
    }

    TaskbarEdge _taskbarEdge = TaskbarEdge.Bottom;

    PxRect MeasureTaskbar(PxRect monitor)
    {
        var tray = NativeMethods.FindWindow("Shell_TrayWnd", null);
        var trayRect = default(PxRect);
        if (tray != IntPtr.Zero && NativeMethods.GetWindowRect(tray, out var nativeTray))
            trayRect = PxRect.FromLtrb(nativeTray.Left, nativeTray.Top, nativeTray.Right, nativeTray.Bottom);

        RememberShellWork(tray != IntPtr.Zero ? tray : _hwnd);
        var message = default(PxRect);
        uint shellEdge = 99;
        var data = NativeMethods.NewAppBarData(_hwnd, _callback);
        if (NativeMethods.SHAppBarMessage(NativeMethods.ABM_GETTASKBARPOS, ref data) != 0)
        {
            message = PxRect.FromLtrb(data.rc.Left, data.rc.Top, data.rc.Right, data.rc.Bottom);
            shellEdge = data.uEdge;
        }

        var work = _shellWorkAreaSaved ? FromNative(_shellWorkArea) : default;
        var choice = AppBarMath.ChooseTaskbar(monitor, trayRect, message, shellEdge, work);
        _taskbarEdge = choice.Edge;
        TaskbarSource = choice.Source;
        return choice.Rect;
    }

    void RememberShellWork(IntPtr window)
    {
        if (_shellWorkAreaSaved)
            return;
        var hmon = NativeMethods.MonitorFromWindow(window != IntPtr.Zero ? window : _hwnd, NativeMethods.MONITOR_DEFAULTTOPRIMARY);
        var info = new MonitorInfo { cbSize = Marshal.SizeOf<MonitorInfo>() };
        if (hmon == IntPtr.Zero || !NativeMethods.GetMonitorInfo(hmon, ref info))
            return;
        _shellWorkArea = info.rcWork;
        _shellWorkAreaSaved = true;
    }

    bool IsAutoHide()
    {
        var data = NativeMethods.NewAppBarData(_hwnd, _callback);
        var state = NativeMethods.SHAppBarMessage(NativeMethods.ABM_GETSTATE, ref data);
        return (state & NativeMethods.ABS_AUTOHIDE) != 0;
    }

    void OnForeground(IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        if (!Topmost || _hwnd == IntPtr.Zero || hwnd == _hwnd)
            return;
        try
        {
            if (_window.Dispatcher.CheckAccess())
                ReassertTop();
            else
                _window.Dispatcher.BeginInvoke(ReassertTop);
        }
        catch (Exception ex)
        {
            Log.Error("foreground " + ex.Message);
        }
    }

    void ReassertTop()
    {
        if (_hwnd == IntPtr.Zero || _applying || !Topmost)
            return;
        if (NativeMethods.IsIconic(_hwnd) || !NativeMethods.IsWindowVisible(_hwnd) || _window.Visibility != Visibility.Visible)
        {
            if (NativeMethods.IsIconic(_hwnd))
                NativeMethods.ShowWindow(_hwnd, NativeMethods.SW_RESTORE);
            _fullscreen = false;
            _applied = default;
            Apply();
            return;
        }

        ApplyChrome();
    }
}
