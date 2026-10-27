using System.Runtime.InteropServices;

namespace LuxTicker.Tests;

public class StartupReportTests
{
    [Fact]
    public void Startup_log_contains_the_full_exception_including_the_inner_one()
    {
        var dir = Path.Combine(Path.GetTempPath(), "lux-startup-" + Guid.NewGuid().ToString("N"));
        try
        {
            var ex = new InvalidOperationException("outer failure", new IOException("inner detail"));
            var path = StartupReport.Write(ex, dir);
            var text = File.ReadAllText(path);
            Assert.Equal(Path.Combine(dir, "startup.log"), path);
            Assert.Contains("InvalidOperationException", text);
            Assert.Contains("outer failure", text);
            Assert.Contains("IOException", text);
            Assert.Contains("inner detail", text);
            Assert.Contains("----", text);

            StartupReport.Write(new ApplicationException("second failure"), dir);
            var again = File.ReadAllText(path);
            Assert.Contains("outer failure", again);
            Assert.Contains("second failure", again);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Run_notes_tell_jonathan_how_to_unblock_the_exe()
    {
        var root = Repo.Root();
        var run = File.ReadAllText(Path.Combine(root, "dist", "RUN.txt"));
        Assert.Contains("not an installer", run, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Unblock", run);
        Assert.Contains("More info", run);
        Assert.Contains("Run anyway", run);
        Assert.Contains("Unblock-File", run);
        Assert.Contains("64-bit", run);
        Assert.Contains("startup.log", run);

        var cmd = File.ReadAllText(Path.Combine(root, "dist", "Run Lux Ticker.cmd"));
        Assert.Contains("Unblock-File -LiteralPath", cmd);
        Assert.Contains("Start-Process", cmd);
        Assert.Contains("LuxTicker.exe", cmd);
    }

    [Fact]
    public void A_missing_native_entry_is_named_with_its_dll_in_startup_log()
    {
        Exception? caught = null;
        try
        {
            MissingNativeEntry();
        }
        catch (Exception ex)
        {
            caught = ex;
        }

        Assert.NotNull(caught);
        Assert.Equal("libc.so.6!lux_ticker_missing_entry_point", NativeExport.Describe(caught!));

        var dir = Path.Combine(Path.GetTempPath(), "lux-startup-" + Guid.NewGuid().ToString("N"));
        try
        {
            var path = StartupReport.Write(caught!, dir);
            var text = File.ReadAllText(path);
            Assert.Contains("Native entry point: libc.so.6!lux_ticker_missing_entry_point", text);
            Assert.Contains("Entry point", text, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Toast_shortcut_does_not_call_the_missing_propsys_export_or_abort_startup()
    {
        var root = Repo.Root();
        var shortcut = File.ReadAllText(Path.Combine(root, "src", "LuxTicker.App", "ToastShortcut.cs"));
        Assert.DoesNotContain("DllImport(\"propsys.dll\"", shortcut);
        Assert.Contains("StringToCoTaskMemUni", shortcut);
        Assert.Contains("PropVariantClear", shortcut);

        var app = File.ReadAllText(Path.Combine(root, "src", "LuxTicker.App", "App.xaml.cs"));
        var prepare = app.IndexOf("luxWireNotifier.Prepare()", StringComparison.Ordinal);
        var window = app.IndexOf("new MainWindow", StringComparison.Ordinal);
        Assert.True(prepare > 0 && window > prepare);
        var between = app[prepare..window];
        Assert.Contains("NoteToastSetupFailed", between);
        Assert.DoesNotContain("StartupGuard.Report", between);
        Assert.DoesNotContain("Shutdown()", between);
    }

    [DllImport("libc.so.6", ExactSpelling = true, EntryPoint = "lux_ticker_missing_entry_point")]
    static extern int MissingNativeEntry();
}
