namespace LuxTicker.Tests;

public class MarketSessionTests
{
    static DateTimeOffset Utc(int y, int m, int d, int hh, int mm, int ss = 0)
        => new(y, m, d, hh, mm, ss, TimeSpan.Zero);

    [Fact]
    public void Eastern_zone_observes_daylight_saving()
    {
        var zone = EasternTime.Zone;
        Assert.Equal(TimeSpan.FromHours(-4), zone.GetUtcOffset(Utc(2026, 7, 15, 16, 0)));
        Assert.Equal(TimeSpan.FromHours(-5), zone.GetUtcOffset(Utc(2026, 1, 15, 16, 0)));
    }

    [Theory]
    [InlineData(2026, 10, 22, 8, 0, SessionPhase.PreMarket)]    // 04:00 EDT
    [InlineData(2026, 10, 22, 12, 0, SessionPhase.PreMarket)]   // 08:00 EDT
    [InlineData(2026, 10, 22, 13, 29, SessionPhase.PreMarket)]  // 09:29 EDT
    [InlineData(2026, 10, 22, 13, 30, SessionPhase.Regular)]    // 09:30 EDT
    [InlineData(2026, 10, 22, 19, 59, SessionPhase.Regular)]    // 15:59 EDT
    [InlineData(2026, 10, 22, 20, 0, SessionPhase.AfterHours)]  // 16:00 EDT
    [InlineData(2026, 10, 22, 23, 59, SessionPhase.AfterHours)] // 19:59 EDT
    [InlineData(2026, 10, 23, 0, 0, SessionPhase.Closed)]       // 20:00 EDT
    [InlineData(2026, 10, 23, 7, 59, SessionPhase.Closed)]      // 03:59 EDT
    public void Thursday_session_boundaries(int y, int m, int d, int hh, int mm, SessionPhase phase)
        => Assert.Equal(phase, MarketSession.Classify(Utc(y, m, d, hh, mm)));

    [Fact]
    public void Weekend_is_closed()
    {
        Assert.Equal(SessionPhase.Closed, MarketSession.Classify(Utc(2026, 10, 24, 14, 0))); // Saturday
        Assert.Equal(SessionPhase.Closed, MarketSession.Classify(Utc(2026, 10, 25, 14, 0))); // Sunday
    }

    [Fact]
    public void Dst_does_not_shift_the_open()
    {
        // Friday 6 Mar 2026 is still EST. 14:30 UTC = 09:30 EST.
        Assert.Equal(SessionPhase.Regular, MarketSession.Classify(Utc(2026, 3, 6, 14, 30)));
        // Monday 9 Mar 2026 is EDT (spring forward was the 8th). 13:30 UTC = 09:30 EDT.
        Assert.Equal(SessionPhase.Regular, MarketSession.Classify(Utc(2026, 3, 9, 13, 30)));
        // Monday 2 Nov 2026 is EST again (fall back was the 1st). 14:30 UTC = 09:30 EST.
        Assert.Equal(SessionPhase.Regular, MarketSession.Classify(Utc(2026, 11, 2, 14, 30)));
    }

    [Theory]
    [InlineData(2024, 3, 29)]
    [InlineData(2025, 4, 18)]
    [InlineData(2026, 4, 3)]
    [InlineData(2027, 3, 26)]
    public void Good_Friday_is_closed(int year, int month, int day)
    {
        var easter = NyseCalendar.EasterSunday(year);
        Assert.Equal(new DateOnly(year, month, day), easter.AddDays(-2));
        Assert.Equal(SessionPhase.Closed, MarketSession.Classify(new DateTimeOffset(year, month, day, 15, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void Known_2026_holidays_are_full_closes()
    {
        var calendar = NyseCalendar.Default;
        var holidays = new[]
        {
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 19),
            new DateOnly(2026, 2, 16),
            new DateOnly(2026, 4, 3),
            new DateOnly(2026, 5, 25),
            new DateOnly(2026, 6, 19),
            new DateOnly(2026, 7, 3), // July 4 is Saturday
            new DateOnly(2026, 9, 7),
            new DateOnly(2026, 11, 26),
            new DateOnly(2026, 12, 25)
        };

        foreach (var day in holidays)
        {
            Assert.True(calendar.IsFullClose(day), day.ToString());
            Assert.False(calendar.IsEarlyClose(day), day.ToString());
        }

        Assert.Equal(SessionPhase.Closed, MarketSession.Classify(Utc(2026, 11, 26, 15, 0)));
    }

    [Fact]
    public void Weekend_holidays_are_observed_on_the_adjacent_weekday()
    {
        var calendar = NyseCalendar.Default;
        Assert.True(calendar.IsFullClose(new DateOnly(2021, 12, 31))); // New Year 2022 was Saturday
        Assert.True(calendar.IsFullClose(new DateOnly(2021, 12, 24))); // Christmas 2021 was Saturday
        Assert.False(calendar.IsEarlyClose(new DateOnly(2021, 12, 24)));
        Assert.True(calendar.IsFullClose(new DateOnly(2027, 7, 5))); // July 4 2027 is Sunday
        Assert.True(calendar.IsFullClose(new DateOnly(2027, 12, 24))); // Christmas 2027 is Saturday
        Assert.False(calendar.IsEarlyClose(new DateOnly(2027, 12, 24)));
    }

    [Fact]
    public void Early_close_ends_the_regular_session_at_1pm()
    {
        var calendar = NyseCalendar.Default;
        Assert.True(calendar.IsEarlyClose(new DateOnly(2026, 11, 27))); // Black Friday
        Assert.True(calendar.IsEarlyClose(new DateOnly(2026, 12, 24))); // Christmas Eve
        Assert.False(calendar.IsEarlyClose(new DateOnly(2026, 7, 3)));

        // 12:59 EST is still regular; 13:00 EST is after-hours; 17:00 EST is closed.
        Assert.Equal(SessionPhase.Regular, MarketSession.Classify(Utc(2026, 11, 27, 17, 59)));
        Assert.Equal(SessionPhase.AfterHours, MarketSession.Classify(Utc(2026, 11, 27, 18, 0)));
        Assert.Equal(SessionPhase.Closed, MarketSession.Classify(Utc(2026, 11, 27, 22, 0)));
    }

    [Fact]
    public void Tags_follow_us_hours_and_skip_crypto_and_non_us()
    {
        Assert.Equal(SessionTag.Pre, MarketSession.TagFor(SessionPhase.PreMarket, SymbolKind.Us));
        Assert.Equal(SessionTag.None, MarketSession.TagFor(SessionPhase.Regular, SymbolKind.Us));
        Assert.Equal(SessionTag.Ah, MarketSession.TagFor(SessionPhase.AfterHours, SymbolKind.Us));
        Assert.Equal(SessionTag.Ah, MarketSession.TagFor(SessionPhase.Closed, SymbolKind.Us));
        Assert.Equal(SessionTag.None, MarketSession.TagFor(SessionPhase.AfterHours, SymbolKind.Crypto));
        Assert.Equal(SessionTag.None, MarketSession.TagFor(SessionPhase.PreMarket, SymbolKind.International));
        Assert.Equal(SessionTag.None, MarketSession.TagFor(SessionPhase.Closed, SymbolKind.Continuous));
    }
}
