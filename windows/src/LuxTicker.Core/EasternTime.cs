namespace LuxTicker;

/// <summary>US Eastern time, including DST. Windows id and the IANA id are both accepted.</summary>
public static class EasternTime
{
    public static TimeZoneInfo Zone { get; } = Load();

    static TimeZoneInfo Load()
    {
        foreach (var id in new[] { "America/New_York", "Eastern Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        throw new InvalidOperationException("Eastern time zone data is not installed (America/New_York).");
    }
}
