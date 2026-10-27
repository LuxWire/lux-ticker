using System.Net;
using System.Windows;
using System.Windows.Threading;

namespace LuxTicker;

public partial class App : Application
{
    Mutex? _mutex;
    bool _ownsMutex;
    CancellationTokenSource? _appStop;
    HttpClient? _http;
    EventWaitHandle? _openSettings;
    Thread? _settingsWatch;
    int _watchSettings;

    public App()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            var ex = args.ExceptionObject as Exception
                ?? new Exception(args.ExceptionObject?.ToString() ?? "Unknown startup failure");
            StartupGuard.Report(ex);
        };
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            LuxWireConfig.Load();
            DispatcherUnhandledException += (_, args) =>
            {
                Log.Error("UI " + args.Exception);
                if (!StartupGuard.IsStarted)
                    StartupGuard.Report(args.Exception);
                args.Handled = true;
            };
            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                Log.Error("task " + args.Exception);
                if (!StartupGuard.IsStarted)
                    StartupGuard.Report(args.Exception);
                args.SetObserved();
            };

            base.OnStartup(e);

            if (LaunchArgs.StartupChoice(e.Args) is bool startWithWindows)
            {
                ApplyStartupChoice(startWithWindows);
                Shutdown();
                return;
            }

            try
            {
                ToastShortcut.Identify();
            }
            catch (Exception ex)
            {
                NoteToastSetupFailed(ex);
            }

            if (!AcquireSingleInstance())
            {
                if (LaunchArgs.WantsSettings(e.Args))
                    SignalOpenSettings();
                Shutdown();
                return;
            }

            WatchSettingsRequests();

            var settingsStore = new SettingsStore(AppPaths.SettingsPath());
            var symbolStore = new SymbolListStore(AppPaths.SymbolsPath());
            var settings = settingsStore.Load();
            var loaded = symbolStore.Load();
            if (loaded.MissingFile)
                symbolStore.Save(loaded.Symbols);
            else if (loaded.Error != null)
                Log.Error("symbols file unreadable, using defaults: " + loaded.Error);

            TickerFont.EnsureExtracted();

            var handler = new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(10),
                AutomaticDecompression = DecompressionMethods.All,
                ConnectTimeout = TimeSpan.FromSeconds(8)
            };
            _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
            _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", FeedEndpoints.UserAgent);
            _http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");

            var book = new QuoteBook();
            _appStop = new CancellationTokenSource();
            var poller = new YahooPoller(_http, book);
            var btc = new CoinbaseFeed(_http, book);
            var probe = new QuoteProbeClient(_http);

            if (settings.StartWithWindows)
            {
                try
                {
                    StartupRegistrar.Apply(true, Environment.ProcessPath);
                }
                catch (Exception ex)
                {
                    Log.Error("startup key " + ex.Message);
                }
            }

            var luxWireNotifier = new LuxWireNotifier();
            try
            {
                luxWireNotifier.Prepare();
            }
            catch (Exception ex)
            {
                // Shortcut / toast COM setup must not stop the ticker or Settings.
                NoteToastSetupFailed(ex);
            }

            var luxWire = new LuxWireFeed(
                AppPaths.LuxWireSeenPath(),
                AppPaths.XAuthPath(),
                luxWireNotifier,
                settings.LuxWirePosts,
                settings.LuxWirePollMinutes);
            var account = new LuxAccountService(_http);
            var window = new MainWindow(settingsStore, symbolStore, book, poller, btc, probe, luxWire, luxWireNotifier, _appStop.Token, settings, loaded.Symbols, account);
            account.Attach(window.CaptureAccount, window.ApplyAccount);
            MainWindow = window;
            window.Show();
            account.Start(_appStop.Token);
            if (LaunchArgs.WantsSettings(e.Args))
                window.ShowSettings();
            try
            {
                luxWire.Start();
            }
            catch (Exception ex)
            {
                Log.Error("lux wire start " + ex);
                StartupGuard.Report(ex);
            }
            poller.Start(_appStop.Token);
            btc.SetEnabled(
                loaded.Symbols.Any(s => s.Kind == SymbolKind.Crypto && s.Symbol.Equals("BTC", StringComparison.OrdinalIgnoreCase)),
                _appStop.Token);
            Log.Info($"started {loaded.Symbols.Count} symbols dock={settings.Dock} speed={settings.Speed} startup={settings.StartWithWindows} luxWire={settings.LuxWirePosts}");
            StartupGuard.MarkStarted();
        }
        catch (Exception ex)
        {
            Log.Error("startup failed " + ex);
            StartupGuard.Report(ex);
            try
            {
                Shutdown();
            }
            catch
            {
                // Already closing.
            }
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Volatile.Write(ref _watchSettings, 0);
        try
        {
            _appStop?.Cancel();
            _http?.Dispose();
            _openSettings?.Set();
        }
        catch
        {
            // Shutting down.
        }

        if (_ownsMutex)
        {
            try
            {
                _mutex?.ReleaseMutex();
            }
            catch
            {
                // Already abandoned.
            }
        }

        _mutex?.Dispose();
        base.OnExit(e);
    }

    bool AcquireSingleInstance()
    {
        _mutex = new Mutex(false, @"Local\LuxTicker.SingleInstance");
        try
        {
            _ownsMutex = _mutex.WaitOne(0);
        }
        catch (AbandonedMutexException)
        {
            _ownsMutex = true;
        }

        return _ownsMutex;
    }

    static void ApplyStartupChoice(bool enabled)
    {
        try
        {
            var store = new SettingsStore(AppPaths.SettingsPath());
            var settings = store.Load() with { StartWithWindows = enabled };
            store.Save(settings);
            StartupRegistrar.Apply(enabled, Environment.ProcessPath);
        }
        catch (Exception ex)
        {
            Log.Error("startup flag " + ex);
            try
            {
                StartupReport.Write(ex);
            }
            catch
            {
                // The installer records a non-zero exit only if we rethrow. Keep going.
            }
        }
    }

    void WatchSettingsRequests()
    {
        try
        {
            Volatile.Write(ref _watchSettings, 1);
            _openSettings = new EventWaitHandle(false, EventResetMode.AutoReset, LaunchArgs.OpenSettingsEvent);
            _settingsWatch = new Thread(WaitForSettingsRequests)
            {
                IsBackground = true,
                Name = "settings-request"
            };
            _settingsWatch.Start();
        }
        catch (Exception ex)
        {
            Log.Error("settings watch " + ex.Message);
        }
    }

    void WaitForSettingsRequests()
    {
        var handle = _openSettings;
        if (handle == null)
            return;
        try
        {
            while (Volatile.Read(ref _watchSettings) == 1)
            {
                if (!handle.WaitOne(400))
                    continue;
                if (Volatile.Read(ref _watchSettings) != 1)
                    return;
                Dispatcher.BeginInvoke(() =>
                {
                    if (MainWindow is MainWindow window)
                        window.ShowSettings();
                });
            }
        }
        catch (Exception ex)
        {
            Log.Error("settings watch " + ex.Message);
        }
    }

    static void SignalOpenSettings()
    {
        try
        {
            using var ev = EventWaitHandle.OpenExisting(LaunchArgs.OpenSettingsEvent);
            ev.Set();
        }
        catch (Exception ex)
        {
            Log.Error("settings signal " + ex.Message);
        }
    }

    static void NoteToastSetupFailed(Exception exception)
    {
        var native = NativeExport.Describe(exception);
        var note = string.IsNullOrEmpty(native)
            ? "Toast shortcut setup failed and was skipped. The ticker will still start."
            : "Toast shortcut setup failed and was skipped. Entry point was not found: " + native + ". The ticker will still start.";
        var wrapped = new InvalidOperationException(note, exception);
        Log.Error("toast shortcut " + wrapped);
        try
        {
            StartupReport.Write(wrapped);
        }
        catch
        {
            // The ticker still starts. startup.log is the best-effort record.
        }
    }
}
