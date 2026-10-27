namespace LuxTicker;

public enum DockEdge
{
    Top,
    Bottom
}

public enum TaskbarEdge
{
    Unknown,
    Left,
    Top,
    Right,
    Bottom
}

/// <summary>Axis-aligned rectangle in physical pixels.</summary>
public readonly record struct PxRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;

    public static PxRect FromLtrb(int left, int top, int right, int bottom)
        => new(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
}

/// <summary>
/// Pure placement math for the ticker bar. Inputs and outputs are physical pixels
/// (after DPI scaling), not WPF device-independent pixels.
/// A 45px taskbar produces a 23px bar (half, rounded half away from zero).
/// </summary>
public static class AppBarMath
{
    public const int FallbackTaskbarThicknessPx = 48;

    public static int TickerHeightPx(int taskbarThicknessPx)
    {
        if (taskbarThicknessPx <= 0)
            return TickerHeightPx(FallbackTaskbarThicknessPx);

        return Math.Max(1, (int)Math.Round(taskbarThicknessPx / 2.0, MidpointRounding.AwayFromZero));
    }

    public static int DipToPhysical(double dip, double dpi)
    {
        if (dpi <= 0)
            dpi = 96;
        return (int)Math.Round(dip * dpi / 96.0, MidpointRounding.AwayFromZero);
    }

    public static double PhysicalToDip(int px, double dpi)
    {
        if (dpi <= 0)
            dpi = 96;
        return px * 96.0 / dpi;
    }

    public static bool Overlaps(PxRect a, PxRect b)
    {
        if (a.Width <= 0 || a.Height <= 0 || b.Width <= 0 || b.Height <= 0)
            return false;
        return a.X < b.Right && b.X < a.Right && a.Y < b.Bottom && b.Y < a.Bottom;
    }

    public static TaskbarEdge DetectEdge(PxRect monitor, PxRect taskbar)
    {
        if (taskbar.Width <= 0 || taskbar.Height <= 0)
            return TaskbarEdge.Unknown;

        // A taskbar on another monitor must not pull this monitor's bar off-screen.
        if (!Overlaps(monitor, taskbar))
            return TaskbarEdge.Unknown;

        if (taskbar.Width >= taskbar.Height)
        {
            int distTop = Math.Abs(taskbar.Y - monitor.Y);
            int distBottom = Math.Abs(taskbar.Bottom - monitor.Bottom);
            return distTop <= distBottom ? TaskbarEdge.Top : TaskbarEdge.Bottom;
        }

        int distLeft = Math.Abs(taskbar.X - monitor.X);
        int distRight = Math.Abs(taskbar.Right - monitor.Right);
        return distLeft <= distRight ? TaskbarEdge.Left : TaskbarEdge.Right;
    }

    /// <summary>The taskbar's thin side, in physical pixels. Zero when the rect is empty.</summary>
    public static int Thickness(PxRect taskbar)
    {
        if (taskbar.Width <= 0 || taskbar.Height <= 0)
            return 0;
        return Math.Min(taskbar.Width, taskbar.Height);
    }

    public static PxRect Place(
        PxRect monitor,
        PxRect taskbar,
        TaskbarEdge taskbarEdge,
        bool autoHide,
        DockEdge dock,
        int tickerHeightPx)
    {
        int height = Math.Max(1, tickerHeightPx);
        int x = monitor.X;
        int width = monitor.Width;

        // A vertical taskbar still occupies a strip. Inset so the ticker does not cover it.
        // Auto-hide does not reserve that strip.
        if (!autoHide && taskbarEdge == TaskbarEdge.Left && taskbar.Right > monitor.X && taskbar.Right < monitor.Right)
        {
            x = taskbar.Right;
            width = monitor.Right - x;
        }
        else if (!autoHide && taskbarEdge == TaskbarEdge.Right && taskbar.X > monitor.X && taskbar.X < monitor.Right)
        {
            width = taskbar.X - monitor.X;
        }

        if (width < 1)
        {
            x = monitor.X;
            width = monitor.Width;
        }

        int y;
        if (dock == DockEdge.Top)
        {
            y = !autoHide && taskbarEdge == TaskbarEdge.Top ? taskbar.Bottom : monitor.Y;
        }
        else if (!autoHide && taskbarEdge == TaskbarEdge.Bottom)
        {
            y = taskbar.Y - height;
        }
        else
        {
            y = monitor.Bottom - height;
        }

        if (y < monitor.Y)
            y = monitor.Y;
        if (y + height > monitor.Bottom)
            y = Math.Max(monitor.Y, monitor.Bottom - height);

        return new PxRect(x, y, width, height);
    }

    /// <summary>
    /// Windows 11 ABM_QUERYPOS / ABM_SETPOS for a bottom app bar returns the screen
    /// bottom, which lands inside the taskbar. The reserved band is always
    /// <see cref="Place"/> — a shell rect that intersects the visible taskbar is ignored.
    /// Auto-hide keeps the flush screen-edge band even when that overlaps the hidden taskbar.
    /// </summary>
    public static PxRect Resolve(
        PxRect monitor,
        PxRect taskbar,
        TaskbarEdge taskbarEdge,
        bool autoHide,
        DockEdge dock,
        int tickerHeightPx,
        PxRect shellSuggested)
    {
        var ideal = Place(monitor, taskbar, taskbarEdge, autoHide, dock, tickerHeightPx);
        if (autoHide)
            return ideal;

        if (taskbar.Width > 0 && taskbar.Height > 0 && Overlaps(shellSuggested, taskbar))
            return ideal;

        if (shellSuggested.Width <= 0 || shellSuggested.Height <= 0)
            return ideal;

        if (shellSuggested.X != ideal.X || shellSuggested.Y != ideal.Y
            || shellSuggested.Width != ideal.Width || shellSuggested.Height != ideal.Height)
            return ideal;

        return ideal;
    }

    /// <summary>
    /// Work area maximized windows should use: the monitor, minus a visible taskbar,
    /// minus the ticker band. An empty ticker rect only removes the taskbar.
    /// Auto-hide does not reserve taskbar space.
    /// </summary>
    public static PxRect WorkArea(
        PxRect monitor,
        PxRect taskbar,
        TaskbarEdge taskbarEdge,
        bool autoHide,
        DockEdge dock,
        PxRect ticker)
    {
        int left = monitor.X;
        int top = monitor.Y;
        int right = monitor.Right;
        int bottom = monitor.Bottom;

        if (!autoHide && taskbar.Width > 0 && taskbar.Height > 0 && Overlaps(monitor, taskbar))
        {
            switch (taskbarEdge)
            {
                case TaskbarEdge.Left:
                    left = Math.Clamp(taskbar.Right, monitor.X, monitor.Right);
                    break;
                case TaskbarEdge.Right:
                    right = Math.Clamp(taskbar.X, monitor.X, monitor.Right);
                    break;
                case TaskbarEdge.Top:
                    top = Math.Clamp(taskbar.Bottom, monitor.Y, monitor.Bottom);
                    break;
                case TaskbarEdge.Bottom:
                    bottom = Math.Clamp(taskbar.Y, monitor.Y, monitor.Bottom);
                    break;
            }
        }

        if (ticker.Width > 0 && ticker.Height > 0)
        {
            if (dock == DockEdge.Top)
                top = Math.Clamp(Math.Max(top, ticker.Bottom), monitor.Y, monitor.Bottom);
            else
                bottom = Math.Clamp(Math.Min(bottom, ticker.Y), monitor.Y, monitor.Bottom);

            if (ticker.X > left)
                left = Math.Min(ticker.X, right);
            if (ticker.Right < right)
                right = Math.Max(left, ticker.Right);
        }

        if (right <= left || bottom <= top)
            return monitor;

        return PxRect.FromLtrb(left, top, right, bottom);
    }

    /// <summary>
    /// Shifts a dock baseline by a physical-pixel offset. Positive is into the desktop,
    /// away from the dock edge. Negative is toward that edge (onto or over the taskbar).
    /// Zero returns <paramref name="baseline"/>. The bar stays fully on the monitor.
    /// </summary>
    public static PxRect WithVerticalOffset(PxRect baseline, DockEdge dock, int offsetPx, PxRect monitor)
    {
        if (offsetPx == 0 || baseline.Height <= 0)
            return baseline;

        int y = baseline.Y + (dock == DockEdge.Top ? offsetPx : -offsetPx);
        if (monitor.Height > 0)
        {
            if (y < monitor.Y)
                y = monitor.Y;
            if (y + baseline.Height > monitor.Bottom)
                y = Math.Max(monitor.Y, monitor.Bottom - baseline.Height);
        }

        return baseline with { Y = y };
    }

    /// <summary>ABE_LEFT=0, ABE_TOP=1, ABE_RIGHT=2, ABE_BOTTOM=3.</summary>
    public static TaskbarEdge FromShellEdge(uint edge) => edge switch
    {
        0 => TaskbarEdge.Left,
        1 => TaskbarEdge.Top,
        2 => TaskbarEdge.Right,
        3 => TaskbarEdge.Bottom,
        _ => TaskbarEdge.Unknown
    };

    /// <summary>
    /// A taskbar is a thin band on one monitor edge. A full-monitor Shell_TrayWnd
    /// (Windows 11 sometimes reports that) is not a taskbar.
    /// </summary>
    public static bool IsEdgeBand(PxRect monitor, PxRect rect, out TaskbarEdge edge)
    {
        edge = DetectEdge(monitor, rect);
        if (edge == TaskbarEdge.Unknown || monitor.Width <= 0 || monitor.Height <= 0)
            return false;
        var thick = Thickness(rect);
        if (thick < 8 || thick > 160)
            return false;
        const int slack = 8;
        return edge switch
        {
            TaskbarEdge.Bottom => Math.Abs(rect.Bottom - monitor.Bottom) <= slack && rect.Height * 2 < monitor.Height,
            TaskbarEdge.Top => Math.Abs(rect.Y - monitor.Y) <= slack && rect.Height * 2 < monitor.Height,
            TaskbarEdge.Left => Math.Abs(rect.X - monitor.X) <= slack && rect.Width * 2 < monitor.Width,
            TaskbarEdge.Right => Math.Abs(rect.Right - monitor.Right) <= slack && rect.Width * 2 < monitor.Width,
            _ => false
        };
    }

    public static bool TryFromWorkArea(PxRect monitor, PxRect work, out PxRect taskbar, out TaskbarEdge edge)
    {
        taskbar = default;
        edge = TaskbarEdge.Unknown;
        if (monitor.Width <= 0 || monitor.Height <= 0 || work.Width <= 0 || work.Height <= 0)
            return false;
        if (!Overlaps(monitor, work))
            return false;

        int left = work.X - monitor.X;
        int top = work.Y - monitor.Y;
        int right = monitor.Right - work.Right;
        int bottom = monitor.Bottom - work.Bottom;
        int best = 0;
        var side = TaskbarEdge.Unknown;
        void Consider(int inset, TaskbarEdge candidate)
        {
            if (inset > best)
            {
                best = inset;
                side = candidate;
            }
        }

        Consider(left, TaskbarEdge.Left);
        Consider(top, TaskbarEdge.Top);
        Consider(right, TaskbarEdge.Right);
        Consider(bottom, TaskbarEdge.Bottom);
        edge = side;
        if (best < 8 || best > 160 || edge == TaskbarEdge.Unknown)
            return false;

        taskbar = edge switch
        {
            TaskbarEdge.Left => new PxRect(monitor.X, monitor.Y, best, monitor.Height),
            TaskbarEdge.Right => new PxRect(monitor.Right - best, monitor.Y, best, monitor.Height),
            TaskbarEdge.Top => new PxRect(monitor.X, monitor.Y, monitor.Width, best),
            _ => new PxRect(monitor.X, monitor.Bottom - best, monitor.Width, best)
        };
        return true;
    }

    public readonly record struct TaskbarChoice(PxRect Rect, TaskbarEdge Edge, string Source);

    /// <summary>
    /// Picks the visible taskbar. A short window rect that sits inside the real
    /// taskbar is rejected in favor of the thicker band, so the ticker is not
    /// placed on top of Start.
    /// </summary>
    public static TaskbarChoice ChooseTaskbar(PxRect monitor, PxRect trayWindow, PxRect shellMessage, uint shellEdge, PxRect work)
    {
        var found = new List<(PxRect Rect, TaskbarEdge Edge, string Source)>();
        if (IsEdgeBand(monitor, shellMessage, out var messageEdge))
        {
            var mapped = FromShellEdge(shellEdge);
            found.Add((shellMessage, mapped == TaskbarEdge.Unknown ? messageEdge : mapped, "appbar"));
        }

        if (IsEdgeBand(monitor, trayWindow, out var trayEdge))
            found.Add((trayWindow, trayEdge, "window"));
        if (TryFromWorkArea(monitor, work, out var fromWork, out var workEdge))
            found.Add((fromWork, workEdge, "work"));

        if (found.Count == 0)
        {
            var thick = Math.Min(FallbackTaskbarThicknessPx, Math.Max(8, monitor.Height / 4));
            return new TaskbarChoice(
                new PxRect(monitor.X, Math.Max(monitor.Y, monitor.Bottom - thick), Math.Max(1, monitor.Width), thick),
                TaskbarEdge.Bottom,
                "fallback");
        }

        var workHit = found.FirstOrDefault(item => item.Source == "work");
        if (workHit.Source == "work")
        {
            var same = found.Where(item => item.Edge == workHit.Edge).OrderByDescending(item => Thickness(item.Rect)).First();
            return Thickness(workHit.Rect) >= Thickness(same.Rect)
                ? new TaskbarChoice(workHit.Rect, workHit.Edge, workHit.Source)
                : new TaskbarChoice(same.Rect, same.Edge, same.Source);
        }

        var best = found.OrderByDescending(item => Thickness(item.Rect)).First();
        return new TaskbarChoice(best.Rect, best.Edge, best.Source);
    }
}

/// <summary>
/// Vertical nudge from the dock baseline, in device-independent pixels.
/// 0 is the 1.4.2 placement. Negative moves toward the taskbar or screen edge.
/// </summary>
public static class BarOffset
{
    public const int DefaultDip = 0;
    public const int MinDip = -240;
    public const int MaxDip = 640;

    public static int Clamp(int dip) => Math.Clamp(dip, MinDip, MaxDip);

    public static string Format(int dip)
        => dip > 0
            ? "+" + dip.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : dip.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
