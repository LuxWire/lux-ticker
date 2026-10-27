using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace LuxTicker;

/// <summary>
/// Pins paper windows to the article cream. Backdrop none so Windows does not lay Mica or a wallpaper tint on top.
/// </summary>
static class PaperChrome
{
    public static void Lock(Window window)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
                return;
            int light = 0;
            NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref light, sizeof(int));
            int backdrop = NativeMethods.DWMSBT_NONE;
            NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
            int caption = PaperInk.ColorRef(PaperInk.Background);
            int text = PaperInk.ColorRef(PaperInk.Ink);
            int border = PaperInk.ColorRef(PaperInk.Rule);
            NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_CAPTION_COLOR, ref caption, sizeof(int));
            NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_TEXT_COLOR, ref text, sizeof(int));
            NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_BORDER_COLOR, ref border, sizeof(int));
        }
        catch (Exception ex)
        {
            Log.Error("paper chrome " + ex.Message);
        }
    }
}

/// <summary>
/// One translucent paper layer. The window opacity stays 1 so the type is not faded,
/// and the frame is extended so the desktop shows through the paper.
/// </summary>
static class BarSurface
{
    public static void Ensure(Window window)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
                return;
            var source = HwndSource.FromHwnd(hwnd);
            if (source?.CompositionTarget != null)
                source.CompositionTarget.BackgroundColor = Colors.Transparent;
            var margins = new NativeMethods.MARGINS
            {
                cxLeftWidth = -1,
                cxRightWidth = -1,
                cyTopHeight = -1,
                cyBottomHeight = -1
            };
            NativeMethods.DwmExtendFrameIntoClientArea(hwnd, ref margins);
        }
        catch (Exception ex)
        {
            Log.Error("bar surface " + ex.Message);
        }
    }
}

static class PaperBrushes
{
    public static SolidColorBrush Freeze(string hex, byte alpha = 255)
    {
        var (r, g, b) = PaperInk.Rgb(hex);
        var brush = new SolidColorBrush(Color.FromArgb(alpha, r, g, b));
        if (brush.CanFreeze)
            brush.Freeze();
        return brush;
    }
}
