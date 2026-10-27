namespace LuxTicker;

/// <summary>Command line for the installer and the Start Menu shortcuts.</summary>
public static class LaunchArgs
{
    public const string OpenSettingsEvent = @"Local\LuxTicker.OpenSettings";

    public static bool WantsSettings(IReadOnlyList<string> args)
    {
        foreach (var arg in args)
        {
            if (IsSettings(arg))
                return true;
        }

        return false;
    }

    /// <summary>
    /// <c>--startup on</c> or <c>--startup off</c>. Null when the argument is absent.
    /// </summary>
    public static bool? StartupChoice(IReadOnlyList<string> args)
    {
        for (int i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            if (arg.Equals("--startup", StringComparison.OrdinalIgnoreCase) || arg.Equals("/startup", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Count)
                    return null;
                return ParseOnOff(args[i + 1]);
            }

            const string prefix = "--startup=";
            if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return ParseOnOff(arg[prefix.Length..]);
        }

        return null;
    }

    static bool IsSettings(string arg)
        => arg.Equals("--settings", StringComparison.OrdinalIgnoreCase)
           || arg.Equals("/settings", StringComparison.OrdinalIgnoreCase)
           || arg.Equals("-settings", StringComparison.OrdinalIgnoreCase);

    static bool? ParseOnOff(string value)
    {
        if (value.Equals("on", StringComparison.OrdinalIgnoreCase)
            || value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value == "1")
            return true;
        if (value.Equals("off", StringComparison.OrdinalIgnoreCase)
            || value.Equals("false", StringComparison.OrdinalIgnoreCase)
            || value == "0")
            return false;
        return null;
    }
}
