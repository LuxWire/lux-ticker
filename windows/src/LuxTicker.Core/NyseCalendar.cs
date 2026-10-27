namespace LuxTicker;

/// <summary>
/// NYSE full closes and 13:00 ET early closes.
/// Weekends are not listed; the session clock treats Saturday and Sunday as closed.
/// A holiday that falls on Saturday is observed Friday; Sunday is observed Monday.
/// </summary>
public sealed class NyseCalendar
{
    public static NyseCalendar Default { get; } = new();

    readonly Dictionary<int, HashSet<DateOnly>> _cache = new();

    public bool IsFullClose(DateOnly date)
    {
        // Next year's New Year's Day can be observed on this year's December 31.
        return Holidays(date.Year).Contains(date) || Holidays(date.Year + 1).Contains(date);
    }

    public bool IsEarlyClose(DateOnly date)
    {
        if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            return false;
        if (IsFullClose(date))
            return false;

        var thanksgiving = NthWeekday(date.Year, 11, DayOfWeek.Thursday, 4);
        if (date == thanksgiving.AddDays(1))
            return true;

        if (date.Month == 12 && date.Day == 24)
            return true;

        // July 3 is an early close only when July 4 itself is a weekday holiday.
        // When July 4 is Saturday, July 3 is a full observed close instead.
        if (date.Month == 7 && date.Day == 3)
        {
            var july4 = new DateOnly(date.Year, 7, 4);
            if (july4.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
                return true;
        }

        return false;
    }

    public HashSet<DateOnly> Holidays(int year)
    {
        lock (_cache)
        {
            if (_cache.TryGetValue(year, out var cached))
                return cached;
        }

        var set = new HashSet<DateOnly>
        {
            Observe(new DateOnly(year, 1, 1)),
            NthWeekday(year, 1, DayOfWeek.Monday, 3),
            NthWeekday(year, 2, DayOfWeek.Monday, 3),
            EasterSunday(year).AddDays(-2),
            LastWeekday(year, 5, DayOfWeek.Monday),
            Observe(new DateOnly(year, 6, 19)),
            Observe(new DateOnly(year, 7, 4)),
            NthWeekday(year, 9, DayOfWeek.Monday, 1),
            NthWeekday(year, 11, DayOfWeek.Thursday, 4),
            Observe(new DateOnly(year, 12, 25))
        };

        lock (_cache)
            _cache[year] = set;
        return set;
    }

    public static DateOnly Observe(DateOnly date)
    {
        if (date.DayOfWeek == DayOfWeek.Saturday)
            return date.AddDays(-1);
        if (date.DayOfWeek == DayOfWeek.Sunday)
            return date.AddDays(1);
        return date;
    }

    public static DateOnly NthWeekday(int year, int month, DayOfWeek day, int n)
    {
        var first = new DateOnly(year, month, 1);
        int delta = ((int)day - (int)first.DayOfWeek + 7) % 7;
        return first.AddDays(delta + (n - 1) * 7);
    }

    public static DateOnly LastWeekday(int year, int month, DayOfWeek day)
    {
        var last = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        int delta = ((int)last.DayOfWeek - (int)day + 7) % 7;
        return last.AddDays(-delta);
    }

    /// <summary>Anonymous Gregorian computus. Returns Easter Sunday.</summary>
    public static DateOnly EasterSunday(int year)
    {
        int a = year % 19;
        int b = year / 100;
        int c = year % 100;
        int d = b / 4;
        int e = b % 4;
        int f = (b + 8) / 25;
        int g = (b - f + 1) / 3;
        int h = (19 * a + b - d - g + 15) % 30;
        int i = c / 4;
        int k = c % 4;
        int l = (32 + 2 * e + 2 * i - h - k) % 7;
        int m = (a + 11 * h + 22 * l) / 451;
        int month = (h + l - 7 * m + 114) / 31;
        int day = ((h + l - 7 * m + 114) % 31) + 1;
        return new DateOnly(year, month, day);
    }
}
