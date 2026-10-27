using System.Globalization;

namespace LuxTicker;

public static class PriceFormat
{
    public static string Format(decimal price, string? currency)
    {
        var cur = currency?.Trim() ?? "USD";
        if (cur is "GBp" or "GBX" or "GBx")
        {
            var text = price.ToString("#,##0.##", CultureInfo.InvariantCulture);
            return text + "p";
        }

        if (cur.Equals("JPY", StringComparison.OrdinalIgnoreCase))
        {
            var whole = price == decimal.Truncate(price);
            var body = price.ToString(whole ? "#,##0" : "#,##0.##", CultureInfo.InvariantCulture);
            return "¥" + body;
        }

        var number = price.ToString("#,##0.00", CultureInfo.InvariantCulture);
        if (cur.Equals("GBP", StringComparison.OrdinalIgnoreCase))
            return "£" + number;
        if (cur.Equals("EUR", StringComparison.OrdinalIgnoreCase))
            return "€" + number;
        if (cur.Equals("HKD", StringComparison.OrdinalIgnoreCase))
            return "HK$" + number;
        if (cur.Equals("CAD", StringComparison.OrdinalIgnoreCase))
            return "C$" + number;
        if (cur.Equals("AUD", StringComparison.OrdinalIgnoreCase))
            return "A$" + number;
        if (cur.Equals("CHF", StringComparison.OrdinalIgnoreCase))
            return "CHF " + number;
        return number;
    }

    public static string FormatChange(decimal percent)
    {
        var rounded = Math.Round(percent, 2, MidpointRounding.AwayFromZero);
        if (rounded == 0)
            return "+0.00%";
        var body = rounded.ToString("0.00", CultureInfo.InvariantCulture);
        if (rounded > 0)
            return "+" + body + "%";
        return body + "%";
    }
}
