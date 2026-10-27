namespace LuxTicker.Tests;

public class LaunchArgsTests
{
    [Theory]
    [InlineData("--settings")]
    [InlineData("/settings")]
    [InlineData("-settings")]
    [InlineData("--SETTINGS")]
    public void Settings_argument_opens_the_window(string arg)
    {
        Assert.True(LaunchArgs.WantsSettings([arg]));
        Assert.False(LaunchArgs.WantsSettings(["--startup", "on"]));
    }

    [Fact]
    public void Startup_choice_is_on_off_or_absent()
    {
        Assert.True(LaunchArgs.StartupChoice(["--startup", "on"]));
        Assert.False(LaunchArgs.StartupChoice(["/startup", "off"]));
        Assert.True(LaunchArgs.StartupChoice(["--startup=true"]));
        Assert.False(LaunchArgs.StartupChoice(["--startup=0"]));
        Assert.Null(LaunchArgs.StartupChoice(["--settings"]));
        Assert.Null(LaunchArgs.StartupChoice(["--startup"]));
        Assert.Null(LaunchArgs.StartupChoice(["--startup", "maybe"]));
    }
}
