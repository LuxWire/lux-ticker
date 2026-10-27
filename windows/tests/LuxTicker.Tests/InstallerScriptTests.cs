namespace LuxTicker.Tests;

public class InstallerScriptTests
{
    [Fact]
    public void Setup_is_per_user_and_asks_for_shortcuts_and_startup()
    {
        var root = Repo.Root();
        var script = File.ReadAllText(Path.Combine(root, "installer", "LuxTicker.iss"));
        Assert.Contains("PrivilegesRequired=lowest", script);
        Assert.Contains("{localappdata}\\LuxTicker", script);
        Assert.Contains("OutputBaseFilename=LuxTicker-Setup", script);
        Assert.Contains("MyAppVersion \"1.4.14\"", script);
        Assert.Contains("OutputBaseFilename=LuxTicker-Setup-1.4.14", script);
        Assert.Contains("MyAppName \"LuxWire Ticker\"", script);
        Assert.Contains("AppMutex=Local\\LuxTicker.SingleInstance", script);
        Assert.Contains("UsePreviousAppDir=yes", script);
        Assert.Contains("DisableDirPage=auto", script);
        Assert.Contains("taskkill.exe", script);
        Assert.Contains("{{8F4E2C1A-6B7D-4E5F-9A1C-2D3E4F506172}", script);
        Assert.Contains("LuxTicker.ico", script);
        Assert.Contains("UninstallDisplayIcon={app}\\{#MyAppExeName}", script);
        Assert.Contains("{userprograms}\\LuxWire Ticker", script);
        Assert.Contains("{userprograms}\\LuxWire Ticker Settings", script);
        Assert.Contains("{userprograms}\\Lux Ticker.lnk", script);
        Assert.Contains("{userprograms}\\Lux Ticker Settings.lnk", script);
        Assert.Contains("--settings", script);
        Assert.Contains("AppUserModelID: \"Lux.LuxTicker\"", script);
        Assert.Contains("desktopicon", script);
        Assert.Contains("Create a desktop shortcut", script);
        Assert.Contains("pintotaskbar", script);
        Assert.Contains("Pin to taskbar", script);
        Assert.Contains("pintostart", script);
        Assert.Contains("startwithwindows", script);
        Assert.Contains("Start LuxWire Ticker when Windows starts", script);
        Assert.Contains("--startup on", script);
        Assert.Contains("--startup off", script);
        Assert.Contains("Check: NoSavedSettings", script);
        Assert.Contains("function NoSavedSettings", script);
        Assert.Contains("{userappdata}\\LuxTicker\\settings.json", script);
        Assert.Contains("CurrentVersion\\Run", script);
        Assert.Contains("HKCU", script);
        Assert.DoesNotContain("PrivilegesRequired=admin", script);
        Assert.DoesNotContain("PrivilegesRequired=highest", script);
        Assert.DoesNotContain("HKLM", script);
    }
}
