namespace LuxTicker.Tests;

public class AppBarMathTests
{
    [Theory]
    [InlineData(45, 23)]
    [InlineData(40, 20)]
    [InlineData(48, 24)]
    [InlineData(47, 24)]
    [InlineData(41, 21)]
    [InlineData(46, 23)]
    public void Height_is_half_the_taskbar_rounded_half_away_from_zero(int taskbar, int ticker)
        => Assert.Equal(ticker, AppBarMath.TickerHeightPx(taskbar));

    [Fact]
    public void Unknown_taskbar_uses_a_48px_fallback()
    {
        Assert.Equal(24, AppBarMath.TickerHeightPx(0));
        Assert.Equal(24, AppBarMath.TickerHeightPx(-5));
        Assert.Equal(AppBarMath.TickerHeightPx(AppBarMath.FallbackTaskbarThicknessPx), AppBarMath.TickerHeightPx(0));
    }

    [Fact]
    public void Dpi_conversion_uses_physical_pixels()
    {
        // A 30 DIP taskbar at 150% (144 DPI) is 45 physical pixels, and the bar is 23 of those.
        Assert.Equal(45, AppBarMath.DipToPhysical(30, 144));
        Assert.Equal(23, AppBarMath.TickerHeightPx(AppBarMath.DipToPhysical(30, 144)));
        Assert.Equal(23 * 96 / 144.0, AppBarMath.PhysicalToDip(23, 144), precision: 6);
        Assert.Equal(23, AppBarMath.DipToPhysical(23, 96));
    }

    [Fact]
    public void Bottom_dock_sits_on_top_of_a_bottom_taskbar()
    {
        var monitor = new PxRect(0, 0, 1920, 1080);
        var taskbar = new PxRect(0, 1035, 1920, 45);
        Assert.Equal(TaskbarEdge.Bottom, AppBarMath.DetectEdge(monitor, taskbar));
        Assert.Equal(45, AppBarMath.Thickness(taskbar));

        var placed = AppBarMath.Place(monitor, taskbar, TaskbarEdge.Bottom, autoHide: false, DockEdge.Bottom, tickerHeightPx: 23);
        Assert.Equal(new PxRect(0, 1012, 1920, 23), placed);
    }

    [Fact]
    public void Top_dock_uses_the_top_edge_when_the_taskbar_is_at_the_bottom()
    {
        var monitor = new PxRect(0, 0, 1920, 1080);
        var taskbar = new PxRect(0, 1035, 1920, 45);
        var placed = AppBarMath.Place(monitor, taskbar, TaskbarEdge.Bottom, autoHide: false, DockEdge.Top, tickerHeightPx: 23);
        Assert.Equal(new PxRect(0, 0, 1920, 23), placed);
    }

    [Fact]
    public void Top_dock_sits_under_a_top_taskbar_and_bottom_dock_uses_the_screen_bottom()
    {
        var monitor = new PxRect(0, 0, 1920, 1080);
        var taskbar = new PxRect(0, 0, 1920, 48);
        Assert.Equal(TaskbarEdge.Top, AppBarMath.DetectEdge(monitor, taskbar));

        var top = AppBarMath.Place(monitor, taskbar, TaskbarEdge.Top, false, DockEdge.Top, 24);
        var bottom = AppBarMath.Place(monitor, taskbar, TaskbarEdge.Top, false, DockEdge.Bottom, 24);
        Assert.Equal(new PxRect(0, 48, 1920, 24), top);
        Assert.Equal(new PxRect(0, 1056, 1920, 24), bottom);
    }

    [Fact]
    public void Auto_hide_bottom_taskbar_puts_the_bar_flush_with_the_screen()
    {
        var monitor = new PxRect(0, 0, 1920, 1080);
        var taskbar = new PxRect(0, 1035, 1920, 45);
        var placed = AppBarMath.Place(monitor, taskbar, TaskbarEdge.Bottom, autoHide: true, DockEdge.Bottom, 23);
        Assert.Equal(new PxRect(0, 1057, 1920, 23), placed);
    }

    [Fact]
    public void Vertical_taskbar_insets_the_full_width_bar()
    {
        var monitor = new PxRect(0, 0, 1920, 1080);
        var taskbar = new PxRect(0, 0, 62, 1080);
        Assert.Equal(TaskbarEdge.Left, AppBarMath.DetectEdge(monitor, taskbar));
        Assert.Equal(62, AppBarMath.Thickness(taskbar));

        var bottom = AppBarMath.Place(monitor, taskbar, TaskbarEdge.Left, false, DockEdge.Bottom, 31);
        var top = AppBarMath.Place(monitor, taskbar, TaskbarEdge.Left, false, DockEdge.Top, 31);
        Assert.Equal(new PxRect(62, 1049, 1858, 31), bottom);
        Assert.Equal(new PxRect(62, 0, 1858, 31), top);
    }

    [Fact]
    public void Primary_monitor_not_at_the_origin_still_spans_that_monitor()
    {
        var monitor = new PxRect(1920, 0, 2560, 1440);
        var taskbar = new PxRect(1920, 1400, 2560, 40);
        Assert.Equal(TaskbarEdge.Bottom, AppBarMath.DetectEdge(monitor, taskbar));
        var placed = AppBarMath.Place(monitor, taskbar, TaskbarEdge.Bottom, false, DockEdge.Bottom, 20);
        Assert.Equal(new PxRect(1920, 1380, 2560, 20), placed);
    }

    [Fact]
    public void Win11_shell_rect_inside_the_taskbar_is_replaced_with_the_band_above_it()
    {
        var monitor = new PxRect(0, 0, 1920, 1080);
        var taskbar = new PxRect(0, 1032, 1920, 48);
        // QUERYPOS on Windows 11 returns the screen bottom, so a 24px bar starts at 1056,
        // inside the 48px taskbar. The topmost taskbar covers it.
        var shell = new PxRect(0, 1056, 1920, 24);
        Assert.True(AppBarMath.Overlaps(shell, taskbar));

        var resolved = AppBarMath.Resolve(monitor, taskbar, TaskbarEdge.Bottom, autoHide: false, DockEdge.Bottom, 24, shell);
        Assert.Equal(new PxRect(0, 1008, 1920, 24), resolved);
        Assert.False(AppBarMath.Overlaps(resolved, taskbar));
        Assert.Equal(taskbar.Y, resolved.Bottom);

        var area = AppBarMath.WorkArea(monitor, taskbar, TaskbarEdge.Bottom, autoHide: false, DockEdge.Bottom, resolved);
        Assert.Equal(new PxRect(0, 0, 1920, 1008), area);
        Assert.False(AppBarMath.Overlaps(area, resolved));
        Assert.False(AppBarMath.Overlaps(area, taskbar));

        var top = AppBarMath.Resolve(monitor, taskbar, TaskbarEdge.Bottom, false, DockEdge.Top, 24, shell);
        Assert.Equal(new PxRect(0, 0, 1920, 24), top);
        var topArea = AppBarMath.WorkArea(monitor, taskbar, TaskbarEdge.Bottom, false, DockEdge.Top, top);
        Assert.Equal(new PxRect(0, 24, 1920, 1008), topArea);
    }

    [Fact]
    public void Auto_hide_resolve_stays_flush_with_the_screen_edge()
    {
        var monitor = new PxRect(0, 0, 1920, 1080);
        var taskbar = new PxRect(0, 1032, 1920, 48);
        var shell = new PxRect(0, 1008, 1920, 24);
        var resolved = AppBarMath.Resolve(monitor, taskbar, TaskbarEdge.Bottom, autoHide: true, DockEdge.Bottom, 24, shell);
        Assert.Equal(new PxRect(0, 1056, 1920, 24), resolved);
        Assert.Equal(monitor.Bottom, resolved.Bottom);

        var area = AppBarMath.WorkArea(monitor, taskbar, TaskbarEdge.Bottom, autoHide: true, DockEdge.Bottom, resolved);
        Assert.Equal(new PxRect(0, 0, 1920, 1056), area);
    }

    [Fact]
    public void Taskbar_on_another_monitor_does_not_pull_the_bar_off_this_one()
    {
        var monitor = new PxRect(0, 0, 1920, 1080);
        var taskbar = new PxRect(1920, 1400, 2560, 40);
        Assert.Equal(TaskbarEdge.Unknown, AppBarMath.DetectEdge(monitor, taskbar));
        var placed = AppBarMath.Place(monitor, taskbar, TaskbarEdge.Unknown, false, DockEdge.Bottom, 24);
        Assert.Equal(new PxRect(0, 1056, 1920, 24), placed);
        Assert.False(AppBarMath.Overlaps(placed, taskbar));
    }

    [Fact]
    public void Vertical_offset_moves_off_the_dock_baseline_and_stays_on_the_monitor()
    {
        Assert.Equal(0, BarOffset.DefaultDip);
        Assert.Equal(-240, BarOffset.MinDip);
        Assert.Equal(640, BarOffset.MaxDip);
        Assert.Equal(0, BarOffset.Clamp(0));
        Assert.Equal(-240, BarOffset.Clamp(-999));
        Assert.Equal(640, BarOffset.Clamp(999));
        Assert.Equal("+32", BarOffset.Format(32));
        Assert.Equal("-16", BarOffset.Format(-16));
        Assert.Equal("0", BarOffset.Format(0));

        var monitor = new PxRect(0, 0, 1920, 1080);
        var taskbar = new PxRect(0, 1035, 1920, 45);
        var baseline = AppBarMath.Place(monitor, taskbar, TaskbarEdge.Bottom, false, DockEdge.Bottom, 23);
        Assert.Equal(baseline, AppBarMath.WithVerticalOffset(baseline, DockEdge.Bottom, 0, monitor));

        var up = AppBarMath.WithVerticalOffset(baseline, DockEdge.Bottom, 40, monitor);
        Assert.Equal(baseline.Y - 40, up.Y);
        Assert.Equal(baseline.X, up.X);
        Assert.Equal(baseline.Height, up.Height);
        Assert.False(AppBarMath.Overlaps(up, taskbar));

        var down = AppBarMath.WithVerticalOffset(baseline, DockEdge.Bottom, -20, monitor);
        Assert.Equal(baseline.Y + 20, down.Y);
        Assert.True(AppBarMath.Overlaps(down, taskbar));

        var buried = AppBarMath.WithVerticalOffset(baseline, DockEdge.Bottom, -5000, monitor);
        Assert.Equal(monitor.Bottom - baseline.Height, buried.Y);

        var top = AppBarMath.Place(monitor, taskbar, TaskbarEdge.Bottom, false, DockEdge.Top, 23);
        Assert.Equal(0, top.Y);
        Assert.Equal(top, AppBarMath.WithVerticalOffset(top, DockEdge.Top, 0, monitor));
        var into = AppBarMath.WithVerticalOffset(top, DockEdge.Top, 30, monitor);
        Assert.Equal(30, into.Y);
        var toward = AppBarMath.WithVerticalOffset(top, DockEdge.Top, -80, monitor);
        Assert.Equal(monitor.Y, toward.Y);

        var area = AppBarMath.WorkArea(monitor, taskbar, TaskbarEdge.Bottom, false, DockEdge.Bottom, up);
        Assert.Equal(up.Y, area.Bottom);
    }

    [Fact]
    public void A_1024_wide_bottom_taskbar_keeps_the_bar_above_Start()
    {
        var monitor = new PxRect(0, 0, 1024, 768);
        var taskbar = new PxRect(0, 728, 1024, 40);
        Assert.True(AppBarMath.IsEdgeBand(monitor, taskbar, out var edge));
        Assert.Equal(TaskbarEdge.Bottom, edge);
        Assert.False(AppBarMath.IsEdgeBand(monitor, monitor, out _));

        var above = AppBarMath.Place(monitor, taskbar, TaskbarEdge.Bottom, false, DockEdge.Bottom, 20);
        Assert.Equal(new PxRect(0, 708, 1024, 20), above);
        Assert.Equal(taskbar.Y, above.Bottom);
        Assert.False(AppBarMath.Overlaps(above, taskbar));

        var top = AppBarMath.Place(monitor, taskbar, TaskbarEdge.Bottom, false, DockEdge.Top, 20);
        Assert.Equal(0, top.Y);
        Assert.False(AppBarMath.Overlaps(top, taskbar));

        var thin = new PxRect(0, 756, 1024, 12);
        var work = new PxRect(0, 0, 1024, 728);
        var choice = AppBarMath.ChooseTaskbar(monitor, thin, default, 99, work);
        Assert.Equal(40, choice.Rect.Height);
        Assert.Equal(TaskbarEdge.Bottom, choice.Edge);
        Assert.Equal("work", choice.Source);

        var missed = AppBarMath.ChooseTaskbar(monitor, monitor, monitor, 1, monitor);
        Assert.Equal("fallback", missed.Source);
        Assert.Equal(TaskbarEdge.Bottom, missed.Edge);
        Assert.Equal(monitor.Bottom, missed.Rect.Bottom);
        Assert.False(AppBarMath.Overlaps(
            AppBarMath.Place(monitor, missed.Rect, missed.Edge, false, DockEdge.Bottom, 20),
            new PxRect(0, 748, 1024, 20)));
    }
}
