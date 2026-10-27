namespace LuxTicker;

/// <summary>
/// The ticker is a borderless app bar. Window opacity below 1 makes it a layered window,
/// and Windows 11 does not paint that strip. The HWND stays fully opaque.
/// </summary>
public static class BarVisibility
{
    public const double PaintOpacity = 1;
    public const int MinimumWidthPx = 8;
    public const int MinimumHeightPx = 8;

    public static double SanitizeOpacity(double opacity)
    {
        if (double.IsNaN(opacity) || double.IsInfinity(opacity) || opacity < 0.2)
            return 0.9;
        return Math.Clamp(opacity, 0.35, 1);
    }

    public static double WindowOpacity(double requested) => PaintOpacity;

    public static AppSettings Sanitize(AppSettings settings)
        => settings.Clamped() with { Opacity = SanitizeOpacity(settings.Opacity) };

    public static bool IsOnScreen(PxRect bar, IReadOnlyList<PxRect> monitors)
    {
        if (bar.Width < MinimumWidthPx || bar.Height < MinimumHeightPx)
            return false;

        if (monitors == null || monitors.Count == 0)
            return bar.Right > 0 && bar.Bottom > 0;

        foreach (var monitor in monitors)
        {
            if (monitor.Width < MinimumWidthPx || monitor.Height < MinimumHeightPx)
                continue;
            int left = Math.Max(bar.X, monitor.X);
            int top = Math.Max(bar.Y, monitor.Y);
            int right = Math.Min(bar.Right, monitor.Right);
            int bottom = Math.Min(bar.Bottom, monitor.Bottom);
            if (right - left >= MinimumWidthPx && bottom - top >= MinimumHeightPx)
                return true;
        }

        return false;
    }
}
