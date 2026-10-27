namespace LuxTicker.Tests;

public class TickerMotionTests
{
    [Fact]
    public void A_long_gap_still_moves_and_is_capped()
    {
        var moved = TickerMotion.Advance(10, 500, 0.1, 96, 96);
        Assert.Equal(10 + 96 * TickerMotion.MaxStepSeconds, moved, 3);

        var hitch = TickerMotion.Advance(0, 500, 2, 90, 96);
        Assert.Equal(90 * TickerMotion.MaxStepSeconds, hitch, 3);
        Assert.True(hitch < 90);
    }

    [Fact]
    public void A_zero_loop_or_a_pause_leaves_the_offset()
    {
        Assert.Equal(12, TickerMotion.Advance(12, 0, 0.03, 90, 96));
        Assert.Equal(12, TickerMotion.Advance(12, 400, 0, 90, 96));
        Assert.Equal(12, TickerMotion.Advance(12, 400, 0.03, 0, 96));
    }

    [Fact]
    public void Offset_keeps_wrapping_over_many_cycles()
    {
        const double loop = 180;
        const double step = 1.0 / 30.0;
        double offset = 0;
        var wraps = 0;
        var previous = offset;
        for (var i = 0; i < 30 * 90; i++)
        {
            offset = TickerMotion.Advance(offset, loop, step, 90, 96);
            Assert.InRange(offset, 0, loop);
            Assert.NotEqual(previous, offset);
            if (offset < previous)
                wraps++;
            previous = offset;
        }

        Assert.True(wraps >= 40, "a minute and a half of motion should lap the strip many times");
    }

    [Fact]
    public void A_step_past_the_loop_wraps_instead_of_parking_at_the_end()
    {
        var wrapped = TickerMotion.Advance(10, 80, 0.05, 5000, 96);
        Assert.Equal(20, wrapped, 3);
        Assert.True(wrapped < 80);
    }

    [Fact]
    public void The_offset_wraps_the_loop()
    {
        var next = TickerMotion.Advance(180, 200, 0.05, 800, 96);
        Assert.InRange(next, 0, 200);
        Assert.True(next < 180);
    }

    [Fact]
    public void Rightward_motion_wraps_and_a_stopped_loop_parks()
    {
        var right = TickerMotion.Advance(10, 80, 0.05, 96, 96, loop: true, toTheRight: true);
        Assert.Equal(5.2, right, 3);
        var wrapped = TickerMotion.Advance(2, 80, 0.05, 96, 96, loop: true, toTheRight: true);
        Assert.Equal(77.2, wrapped, 3);
        var parked = TickerMotion.Advance(70, 80, 0.05, 5000, 96, loop: false);
        Assert.Equal(80, parked, 3);
        Assert.Equal(0, TickerMotion.Advance(2, 80, 0.05, 96, 96, loop: false, toTheRight: true), 3);
    }

    [Fact]
    public void Show_desktop_cannot_hide_the_saved_rect_and_click_through_is_separate()
    {
        var locked = DeskBand.Lock(DeskBand.SwpHideWindow | 0x4, 1, 2, 3, 4, 10, 20, 100, 24);
        Assert.True(locked.RefusedHide);
        Assert.Equal(0u, locked.Flags & DeskBand.SwpHideWindow);
        Assert.Equal(DeskBand.SwpShowWindow, locked.Flags & DeskBand.SwpShowWindow);
        Assert.Equal(10, locked.X);
        Assert.Equal(20, locked.Y);
        Assert.Equal(100, locked.Cx);
        Assert.Equal(24, locked.Cy);

        var plain = DeskBand.Compose(DeskBand.Layered | DeskBand.AppWindow | DeskBand.Transparent, clickThrough: false);
        Assert.Equal(0, plain & DeskBand.Layered);
        Assert.Equal(0, plain & DeskBand.AppWindow);
        Assert.Equal(0, plain & DeskBand.Transparent);
        Assert.NotEqual(0, plain & DeskBand.ToolWindow);
        var through = DeskBand.Compose(0, clickThrough: true);
        Assert.Equal(0, through & DeskBand.Transparent);
        Assert.Equal(ClickThroughHit.HtClient, ClickThroughHit.Code(true, true));
        Assert.Equal(ClickThroughHit.HtTransparent, ClickThroughHit.Code(true, false));
        Assert.Equal(ClickThroughHit.HtClient, ClickThroughHit.Code(false, false));
        Assert.True(ClickThroughHit.Contains(10, 10, 0, 0, 20, 20));
        Assert.False(ClickThroughHit.Contains(30, 10, 0, 0, 20, 20));
        Assert.Equal(48, IconMetrics.ForTrayDpi(384));
        Assert.Equal(16, IconMetrics.ForTrayDpi(96));
        Assert.Equal(-1, ZOrder.InsertAfter(alwaysOnTop: true, overTaskbar: false));
        Assert.Equal(-1, ZOrder.InsertAfter(alwaysOnTop: false, overTaskbar: true));
        Assert.Equal(0, ZOrder.InsertAfter(alwaysOnTop: false, overTaskbar: false));
    }

    [Theory]
    [InlineData(96, 16)]
    [InlineData(120, 20)]
    [InlineData(144, 24)]
    [InlineData(192, 32)]
    [InlineData(240, 40)]
    [InlineData(288, 48)]
    [InlineData(384, 64)]
    public void Icon_frame_follows_dpi(int dpi, int frame)
    {
        Assert.Equal(frame, IconMetrics.ForDpi(dpi));
    }

    [Fact]
    public void A_covering_window_is_fullscreen_and_a_small_one_is_not()
    {
        var monitor = new PxRect(0, 0, 1920, 1080);
        Assert.True(BarRecovery.Covers(monitor, monitor));
        Assert.True(BarRecovery.Covers(monitor, new PxRect(-10, -10, 2000, 1200)));
        Assert.False(BarRecovery.Covers(monitor, new PxRect(100, 100, 800, 600)));
        Assert.True(BarRecovery.StuckFullscreen(true, false));
        Assert.False(BarRecovery.StuckFullscreen(true, true));
        Assert.False(BarRecovery.StuckFullscreen(false, false));
    }
}

public class UpdateCheckTests
{
    [Fact]
    public void A_feed_is_a_version_and_a_setup_url()
    {
        Assert.True(UpdateCheck.TryParse("1.4.4\nhttps://example.test/LuxTicker-Setup-1.4.4.exe\n", out var version, out var url));
        Assert.Equal("1.4.4", version);
        Assert.Equal("https://example.test/LuxTicker-Setup-1.4.4.exe", url);
        Assert.False(UpdateCheck.IsNewer("1.4.4", "1.4.4"));
        Assert.True(UpdateCheck.IsNewer("1.4.3", "1.4.4"));
        Assert.False(UpdateCheck.IsNewer("1.4.4", "1.4.3"));
        Assert.True(UpdateCheck.IsNewer("1.4.4", "1.5"));
    }

    [Fact]
    public void Html_is_not_a_feed()
    {
        const string html = "<!DOCTYPE html><html><body>sign in</body></html>";
        Assert.False(UpdateCheck.TryParse(html, out _, out _));
        Assert.False(UpdateCheck.TryParse("   ", out _, out _));
        Assert.False(UpdateCheck.TryParse("hello", out _, out _));
    }

    [Fact]
    public void A_drive_confirm_form_becomes_the_download_url()
    {
        const string page = """
            <html><body>
            <form action="https://drive.usercontent.google.com/download" method="get">
            <input type="hidden" name="id" value="FILEID">
            <input type="hidden" name="confirm" value="t">
            <input type="hidden" name="uuid" value="abc-uuid">
            </form>
            </body></html>
            """;
        var next = UpdateCheck.NextDownloadUrl("https://drive.google.com/uc?export=download&id=FILEID", page);
        Assert.NotNull(next);
        Assert.Contains("confirm=t", next);
        Assert.Contains("uuid=abc-uuid", next);
        Assert.Contains("id=FILEID", next);
    }
}
