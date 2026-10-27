namespace LuxTicker;

public enum SessionPhase
{
    Closed,
    PreMarket,
    Regular,
    AfterHours
}

public enum SessionTag
{
    None,
    Pre,
    Ah
}

public static class MarketSession
{
    public static readonly TimeOnly PreStart = new(4, 0);
    public static readonly TimeOnly RegularOpen = new(9, 30);
    public static readonly TimeOnly RegularClose = new(16, 0);
    public static readonly TimeOnly EarlyClose = new(13, 0);
    public static readonly TimeOnly AfterHoursEnd = new(20, 0);
    public static readonly TimeOnly EarlyAfterHoursEnd = new(17, 0);

    public static SessionPhase Classify(DateTimeOffset instant)
        => Classify(instant, EasternTime.Zone, NyseCalendar.Default);

    public static SessionPhase Classify(DateTimeOffset instant, TimeZoneInfo eastern, NyseCalendar calendar)
    {
        var local = TimeZoneInfo.ConvertTime(instant, eastern);
        var date = DateOnly.FromDateTime(local.DateTime);
        var time = TimeOnly.FromDateTime(local.DateTime);

        if (local.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            return SessionPhase.Closed;
        if (calendar.IsFullClose(date))
            return SessionPhase.Closed;

        var close = calendar.IsEarlyClose(date) ? EarlyClose : RegularClose;
        var ahEnd = calendar.IsEarlyClose(date) ? EarlyAfterHoursEnd : AfterHoursEnd;

        if (time >= PreStart && time < RegularOpen)
            return SessionPhase.PreMarket;
        if (time >= RegularOpen && time < close)
            return SessionPhase.Regular;
        if (time >= close && time < ahEnd)
            return SessionPhase.AfterHours;
        return SessionPhase.Closed;
    }

    /// <summary>
    /// US names get PRE during pre-market and AH after the close, overnight, on weekends, and on holidays.
    /// Crypto, non-US listings, and nearly 24h futures do not get a session tag.
    /// </summary>
    public static SessionTag TagFor(SessionPhase phase, SymbolKind kind)
    {
        if (kind != SymbolKind.Us)
            return SessionTag.None;

        return phase switch
        {
            SessionPhase.PreMarket => SessionTag.Pre,
            SessionPhase.Regular => SessionTag.None,
            _ => SessionTag.Ah
        };
    }
}
