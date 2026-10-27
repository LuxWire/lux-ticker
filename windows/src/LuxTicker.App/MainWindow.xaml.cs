using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace LuxTicker;

public partial class MainWindow : Window
{
    readonly SettingsStore _settingsStore;
    readonly SymbolListStore _symbolStore;
    readonly PriceAlertStore _alertsStore;
    readonly QuoteBook _book;
    readonly YahooPoller _poller;
    readonly CoinbaseFeed _btc;
    readonly IQuoteProbe _probe;
    readonly LuxWireFeed _luxWire;
    readonly LuxWireNotifier _luxWireNotifier;
    readonly ILuxAccount _account;
    readonly CancellationToken _appStop;
    readonly AppBarController _appBar;
    readonly TickerStrip _strip;
    readonly DispatcherTimer _clock;
    readonly DispatcherTimer _heal;
    readonly DispatcherTimer _quoteTimer;
    readonly FileSystemWatcher? _watcher;
    readonly Dictionary<string, List<decimal>> _sparks = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, decimal> _alertPrice = new(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> _alertSeen = new(StringComparer.OrdinalIgnoreCase);

    AppSettings _settings;
    List<SymbolEntry> _symbols;
    List<PriceAlert> _alerts;
    SessionPhase _phase;
    EditorWindow? _editor;
    SettingsWindow? _settingsWindow;
    BtcChartWindow? _chart;
    WindowIcons? _icons;
    TrayIcon? _tray;
    bool _rebuildQueued;
    DateTime _suppressWatchUntil;
    bool _closing;
    bool _applyingRemote;
    string? _pendingSymbol;
    string? _menuSymbol;
    string? _menuPrice;

    internal MainWindow(
        SettingsStore settingsStore,
        SymbolListStore symbolStore,
        QuoteBook book,
        YahooPoller poller,
        CoinbaseFeed btc,
        IQuoteProbe probe,
        LuxWireFeed luxWire,
        LuxWireNotifier luxWireNotifier,
        CancellationToken appStop,
        AppSettings settings,
        IReadOnlyList<SymbolEntry> symbols,
        ILuxAccount account)
    {
        _settingsStore = settingsStore;
        _symbolStore = symbolStore;
        _alertsStore = new PriceAlertStore(AppPaths.AlertsPath());
        _book = book;
        _poller = poller;
        _btc = btc;
        _probe = probe;
        _luxWire = luxWire;
        _luxWireNotifier = luxWireNotifier;
        _account = account;
        _appStop = appStop;
        _settings = BarVisibility.Sanitize(settings);
        _symbols = symbols.ToList();
        _alerts = _alertsStore.Load().ToList();

        InitializeComponent();
        _strip = new TickerStrip { TickerFontFamily = TickerFont.Load() };
        _strip.SymbolClicked += OnSymbolClicked;
        Grid.SetColumn(_strip, 0);
        Root.Children.Insert(0, _strip);
        ContextMenu = BuildMenu();
        ContextMenu.Opened += (_, _) =>
        {
            SyncMenu();
            _strip.MenuOpen = true;
        };
        ContextMenu.Closed += (_, _) => _strip.MenuOpen = false;

        _quoteTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _quoteTimer.Tick += (_, _) =>
        {
            _quoteTimer.Stop();
            var symbol = _pendingSymbol;
            _pendingSymbol = null;
            if (!string.IsNullOrEmpty(symbol))
                OpenQuote(symbol);
        };

        _appBar = new AppBarController(this);
        _appBar.ButtonHit = ButtonScreenHit;
        _appBar.ShellRestarted += RecreateTray;
        _appBar.DisplayChanged += () => _tray?.Refresh();
        _phase = MarketSession.Classify(DateTimeOffset.UtcNow);
        _poller.Updated += OnFeedUpdated;
        _btc.Updated += OnFeedUpdated;
        _poller.SetSymbols(_symbols);
        _luxWire.SetDigest(_settings.LuxWireDigest);
        ApplyChromeFromSettings();
        RenderQuotes();

        SourceInitialized += (_, _) =>
        {
            PaperChrome.Lock(this);
            try
            {
                _icons = WindowIcons.Apply(this);
            }
            catch (Exception ex)
            {
                Log.Error("window icon " + ex.Message);
            }

            try
            {
                _tray = new TrayIcon(this, ShowMenuAtCursor, OpenSettings);
                _luxWireNotifier.UseBalloon(_tray.ShowBalloon);
                UpdateTrayTip();
            }
            catch (Exception ex)
            {
                Log.Error("tray icon " + ex.Message);
            }

            try
            {
                ApplyChromeFromSettings();
                _appBar.Attach(_settings.Dock);
                if (_appBar.RecoverIfHidden())
                {
                    _settings = _settings with { Dock = DockEdge.Bottom, MonitorDevice = null };
                    try
                    {
                        _settingsStore.Save(_settings);
                    }
                    catch (Exception saveEx)
                    {
                        Log.Error("reset placement " + saveEx.Message);
                    }
                }

                RememberMonitor();
                _appBar.LogPlacement();
            }
            catch (Exception ex)
            {
                Log.Error("appbar attach " + ex);
                if (!StartupGuard.IsStarted)
                    StartupGuard.Report(ex);
                _appBar.Reveal();
            }
        };
        Closed += (_, _) => ShutdownFeeds();

        _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _clock.Tick += (_, _) =>
        {
            var phase = MarketSession.Classify(DateTimeOffset.UtcNow);
            if (phase != _phase)
            {
                _phase = phase;
                RenderQuotes();
            }

            if (_closing)
                return;
            _appBar.Apply();
            if (_appBar.Heal())
                ResetHiddenDock();

            RememberMonitor();
        };
        _clock.Start();
        _heal = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _heal.Tick += (_, _) =>
        {
            if (_closing)
                return;
            if (_appBar.Heal())
                ResetHiddenDock();
        };
        _heal.Start();
        _ = QuietUpdateCheckAsync();

        try
        {
            var dir = System.IO.Path.GetDirectoryName(_symbolStore.Path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
                _watcher = new FileSystemWatcher(dir, AppPaths.SymbolsFileName)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName
                };
                _watcher.Changed += (_, _) => QueueReload();
                _watcher.Created += (_, _) => QueueReload();
                _watcher.Renamed += (_, _) => QueueReload();
                _watcher.EnableRaisingEvents = true;
            }
        }
        catch (Exception ex)
        {
            Log.Error("watch " + ex.Message);
        }
    }

    public void ReplaceSymbols(IReadOnlyList<SymbolEntry> symbols)
    {
        _suppressWatchUntil = DateTime.UtcNow.AddMilliseconds(800);
        _symbolStore.Save(symbols);
        AdoptSymbols(symbols);
        NoteAccountEdit();
    }

    void AdoptSymbols(IReadOnlyList<SymbolEntry> symbols)
    {
        if (_symbols.SequenceEqual(symbols))
            return;
        _symbols = symbols.ToList();
        _poller.SetSymbols(_symbols);
        _btc.SetEnabled(_symbols.Any(s => s.Kind == SymbolKind.Crypto && s.Symbol.Equals("BTC", StringComparison.OrdinalIgnoreCase)), _appStop);
        RenderQuotes();
        _settingsWindow?.ReloadSymbols(_symbols);
        _editor?.Reload(_symbols);
    }

    void ReplaceAlerts(IReadOnlyList<PriceAlert> alerts)
    {
        _alerts = alerts.ToList();
        try
        {
            _alertsStore.Save(_alerts);
        }
        catch (Exception ex)
        {
            Log.Error("alerts " + ex.Message);
        }

        _settingsWindow?.ReloadAlerts(_alerts);
    }

    void OnFeedUpdated()
    {
        if (_rebuildQueued)
            return;
        _rebuildQueued = true;
        Dispatcher.BeginInvoke(() =>
        {
            _rebuildQueued = false;
            _phase = MarketSession.Classify(DateTimeOffset.UtcNow);
            RenderQuotes();
        }, DispatcherPriority.Background);
    }

    void RenderQuotes()
    {
        var items = new DisplayItem[_symbols.Count];
        int stale = 0;
        for (int i = 0; i < _symbols.Count; i++)
        {
            var entry = _symbols[i];
            _book.TryGet(entry.Symbol, out var snap);
            IReadOnlyList<decimal>? spark = null;
            var price = snap?.PriceFor(_phase, entry.Kind);
            if (price != null)
            {
                NoteSpark(entry.Symbol, price.Value);
                if (_settings.ShowSparklines && _sparks.TryGetValue(entry.Symbol, out var series) && series.Count >= 2)
                    spark = series.ToArray();
            }

            items[i] = QuotePresenter.Present(
                entry, snap, _phase, _settings.ShowPercent, spark, _settings.ShowPre, _settings.ShowAfterHours,
                _settings.TextColor, _settings.PriceColor, _settings.ChangeUpColor, _settings.ChangeDownColor);
            if (items[i].Stale)
                stale++;
        }

        _strip.Badge = _symbols.Count == 0 ? "" : stale == _symbols.Count ? "Offline" : stale > 0 ? "Stale" : "";
        _strip.PinnedSymbols = _settings.PinBtc ? new[] { "BTC" } : Array.Empty<string>();
        _strip.SetItems(items);
        EvaluateAlerts();
        UpdateTrayTip();
    }

    void NoteSpark(string symbol, decimal price)
    {
        if (!_sparks.TryGetValue(symbol, out var list))
        {
            list = new List<decimal>();
            _sparks[symbol] = list;
        }

        if (list.Count > 0 && list[^1] == price)
            return;
        list.Add(price);
        if (list.Count > 24)
            list.RemoveAt(0);
    }

    void EvaluateAlerts()
    {
        foreach (var alert in _alerts)
        {
            if (!alert.Enabled || !_book.TryGet(alert.Symbol, out var snap) || snap == null || snap.Stale)
                continue;
            var entry = _symbols.FirstOrDefault(item => item.Symbol.Equals(alert.Symbol, StringComparison.OrdinalIgnoreCase));
            var kind = entry?.Kind ?? SymbolKind.Us;
            var price = snap.PriceFor(_phase, kind);
            if (price == null)
                continue;

            var key = AlertKeys.For(alert);
            decimal? previous = _alertSeen.Contains(key) ? _alertPrice[key] : null;
            if (PriceAlertEngine.Crossed(previous, price.Value, alert.Above, alert.Price))
            {
                var word = alert.Above ? "above" : "below";
                var level = alert.Price.ToString(CultureInfo.InvariantCulture);
                var body = alert.Symbol + " is " + PriceFormat.Format(price.Value, snap.Currency ?? entry?.Currency) + ", " + word + " " + level + ".";
                _luxWireNotifier.ShowNotice(alert.Symbol + " " + word + " " + level, body, QuoteLinks.For(alert.Symbol, kind));
            }

            _alertPrice[key] = price.Value;
            _alertSeen.Add(key);
        }
    }

    void ApplyChromeFromSettings()
    {
        // Never the saved opacity. Below 1 the borderless app bar becomes layered and Windows 11 does not paint it.
        // BackgroundOpacity paints only the paper. The type stays opaque.
        Opacity = BarVisibility.WindowOpacity(_settings.Opacity);
        var paper = PaperBrushes.Freeze(_settings.BackgroundColor, BarSurfaceMath.Alpha(_settings.BackgroundOpacity));
        Background = Brushes.Transparent;
        Root.Background = paper;
        _strip.Background = Brushes.Transparent;
        _strip.TranslucentBackground = _settings.BackgroundOpacity < 0.999;
        _strip.PixelsPerSecond = ScrollPhysics.ClampPixels(_settings.ScrollPixelsPerSecond);
        _strip.ScrollEnabled = _settings.ScrollEnabled;
        _strip.ScrollLoop = _settings.ScrollLoop;
        _strip.ScrollRight = _settings.ScrollRight;
        _strip.TextScale = _settings.TextScale;
        _strip.SymbolGap = _settings.SymbolGap;
        _strip.LiveUpdates = _settings.LiveScrollUpdates;
        _strip.UpFlashColor = _settings.ChangeUpColor;
        _strip.DownFlashColor = _settings.ChangeDownColor;
        _strip.TickerFontFamily = TickerFont.Resolve(_settings.TickerFontName);
        _strip.PinnedSymbols = _settings.PinBtc ? new[] { "BTC" } : Array.Empty<string>();
        BtcButton.Background = Brushes.Transparent;
        SettingsButton.Background = Brushes.Transparent;
        _appBar.Topmost = _settings.AlwaysOnTop;
        _appBar.ClickThrough = _settings.ClickThrough;
        _appBar.HeightFraction = _settings.CompactLayout ? 0.5 : 1;
        _appBar.VerticalOffsetDip = _settings.VerticalOffsetDip;
        _appBar.PreferredDevice = _settings.MonitorDevice;
    }

    bool ButtonScreenHit(int x, int y)
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero || !NativeMethods.GetWindowRect(hwnd, out var window))
                return false;
            var dpi = NativeMethods.GetDpiForWindow(hwnd);
            if (dpi < 96)
                dpi = 96;
            var scale = dpi / 96.0;
            if (BarActions.ActualWidth < 1 || BarActions.ActualHeight < 1)
            {
                var fallback = Math.Max(1, (int)Math.Round(140 * scale));
                return ClickThroughHit.Contains(x, y, window.Right - fallback, window.Top, fallback, Math.Max(1, window.Bottom - window.Top));
            }

            var relative = BarActions.TranslatePoint(new Point(0, 0), this);
            var left = window.Left + (int)Math.Round(relative.X * scale);
            var top = window.Top + (int)Math.Round(relative.Y * scale);
            var width = Math.Max(1, (int)Math.Round(BarActions.ActualWidth * scale));
            var height = Math.Max(1, (int)Math.Round(BarActions.ActualHeight * scale));
            return ClickThroughHit.Contains(x, y, left, top, width, height);
        }
        catch (Exception ex)
        {
            Log.Error("click-through hit " + ex.Message);
            return false;
        }
    }

    void RememberMonitor()
    {
        var device = _appBar.ActiveDevice;
        if (!_appBar.IsOnScreen() || string.IsNullOrEmpty(device) || string.Equals(device, _settings.MonitorDevice, StringComparison.OrdinalIgnoreCase))
            return;
        _settings = _settings with { MonitorDevice = device };
        try
        {
            _settingsStore.Save(_settings);
        }
        catch (Exception ex)
        {
            Log.Error("monitor " + ex.Message);
        }
    }

    void UpdateTrayTip()
    {
        if (_tray == null)
            return;
        var pick = _symbols.FirstOrDefault(item => item.Symbol.Equals("BTC", StringComparison.OrdinalIgnoreCase)) ?? _symbols.FirstOrDefault();
        var tip = LuxWireEdition.ProductName;
        if (pick != null && _book.TryGet(pick.Symbol, out var snap) && snap != null)
        {
            var price = snap.PriceFor(_phase, pick.Kind);
            if (price != null)
                tip = pick.DisplayLabel + " " + PriceFormat.Format(price.Value, snap.Currency ?? pick.Currency);
        }

        if (_strip.Badge.Length > 0)
            tip += " · " + _strip.Badge;
        _tray.SetTip(tip);
    }

    void OnSymbolClicked(string symbol)
    {
        _pendingSymbol = symbol;
        _quoteTimer.Stop();
        _quoteTimer.Start();
    }

    void OpenQuote(string symbol)
    {
        var entry = _symbols.FirstOrDefault(item => item.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));
        var kind = entry?.Kind ?? SymbolEntry.Infer(symbol, null).Kind;
        var url = QuoteLinks.For(symbol, kind);
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Error("quote " + ex.Message);
        }
    }

    void QueueReload()
    {
        Dispatcher.BeginInvoke(async () =>
        {
            try
            {
                await Task.Delay(250);
                if (DateTime.UtcNow < _suppressWatchUntil)
                    return;
                var loaded = _symbolStore.Load();
                if (loaded.Error != null)
                {
                    Log.Error("symbols reload " + loaded.Error);
                    return;
                }

                AdoptSymbols(loaded.Symbols);
            }
            catch (Exception ex)
            {
                Log.Error("symbols reload " + ex.Message);
            }
        });
    }

    ContextMenu BuildMenu()
    {
        var menu = new ContextMenu { FontSize = 13 };
        StyleMenu(menu);

        var dockTop = Item("Top of the screen", () => Commit(_settings with { Dock = DockEdge.Top }));
        var dockBottom = Item("Above the taskbar", () => Commit(_settings with { Dock = DockEdge.Bottom }));
        dockTop.IsCheckable = true;
        dockBottom.IsCheckable = true;

        var speed = new MenuItem { Header = "Speed" };
        var slow = Item("Slow", () => Commit(_settings with { Speed = ScrollSpeed.Slow, ScrollPixelsPerSecond = ScrollPhysics.PixelsPerSecond(ScrollSpeed.Slow) }));
        var normal = Item("Normal", () => Commit(_settings with { Speed = ScrollSpeed.Normal, ScrollPixelsPerSecond = ScrollPhysics.PixelsPerSecond(ScrollSpeed.Normal) }));
        var fast = Item("Fast", () => Commit(_settings with { Speed = ScrollSpeed.Fast, ScrollPixelsPerSecond = ScrollPhysics.PixelsPerSecond(ScrollSpeed.Fast) }));
        slow.IsCheckable = true;
        normal.IsCheckable = true;
        fast.IsCheckable = true;
        speed.Items.Add(slow);
        speed.Items.Add(normal);
        speed.Items.Add(fast);

        var startup = Item("Start with Windows", () => Commit(_settings with { StartWithWindows = !_settings.StartWithWindows }));
        startup.IsCheckable = true;
        var luxWire = Item("Lux Wire post alerts", () => Commit(_settings with { LuxWirePosts = !_settings.LuxWirePosts }));
        luxWire.IsCheckable = true;
        var copySymbol = Item("Copy ticker", CopySymbol);
        var copyPrice = Item("Copy price", CopyPrice);

        menu.Items.Add(Item("Chart", OpenChart));
        menu.Items.Add(Item("Settings", OpenSettings));
        menu.Items.Add(copySymbol);
        menu.Items.Add(copyPrice);
        menu.Items.Add(Item("Refresh", () => _poller.RefreshNow()));
        menu.Items.Add(new Separator());
        menu.Items.Add(dockBottom);
        menu.Items.Add(dockTop);
        menu.Items.Add(new Separator());
        menu.Items.Add(speed);
        menu.Items.Add(new Separator());
        menu.Items.Add(startup);
        menu.Items.Add(luxWire);
        menu.Items.Add(Item("Edit tickers", OpenEditor));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Quit", () => Close()));

        menu.Tag = new MenuHandles(dockTop, dockBottom, slow, normal, fast, startup, luxWire, copySymbol, copyPrice);
        return menu;
    }

    void SyncMenu()
    {
        if (ContextMenu?.Tag is not MenuHandles menu)
            return;
        menu.DockTop.IsChecked = _settings.Dock == DockEdge.Top;
        menu.DockBottom.IsChecked = _settings.Dock == DockEdge.Bottom;
        menu.Slow.IsChecked = Math.Abs(_settings.ScrollPixelsPerSecond - ScrollPhysics.PixelsPerSecond(ScrollSpeed.Slow)) < 0.1;
        menu.Normal.IsChecked = Math.Abs(_settings.ScrollPixelsPerSecond - ScrollPhysics.PixelsPerSecond(ScrollSpeed.Normal)) < 0.1;
        menu.Fast.IsChecked = Math.Abs(_settings.ScrollPixelsPerSecond - ScrollPhysics.PixelsPerSecond(ScrollSpeed.Fast)) < 0.1;
        menu.Startup.IsChecked = _settings.StartWithWindows;
        menu.LuxWire.IsChecked = _settings.LuxWirePosts;
        menu.LuxWire.Visibility = XAuthStore.Load(AppPaths.XAuthPath()).CanAlert(DateTimeOffset.UtcNow)
            ? Visibility.Visible
            : Visibility.Collapsed;

        _menuSymbol = _strip.SymbolAt(Mouse.GetPosition(_strip));
        _menuPrice = null;
        if (_menuSymbol != null)
        {
            var entry = _symbols.FirstOrDefault(item => item.Symbol.Equals(_menuSymbol, StringComparison.OrdinalIgnoreCase));
            if (entry != null && _book.TryGet(entry.Symbol, out var snap) && snap != null)
            {
                var price = snap.PriceFor(_phase, entry.Kind);
                if (price != null)
                    _menuPrice = PriceFormat.Format(price.Value, snap.Currency ?? entry.Currency);
            }
        }

        menu.CopySymbol.IsEnabled = _menuSymbol != null;
        menu.CopyPrice.IsEnabled = _menuPrice != null;
    }

    void CopySymbol()
    {
        if (string.IsNullOrEmpty(_menuSymbol))
            return;
        try
        {
            Clipboard.SetText(_menuSymbol);
        }
        catch (Exception ex)
        {
            Log.Error("copy " + ex.Message);
        }
    }

    void CopyPrice()
    {
        if (string.IsNullOrEmpty(_menuPrice))
            return;
        try
        {
            Clipboard.SetText(_menuPrice);
        }
        catch (Exception ex)
        {
            Log.Error("copy " + ex.Message);
        }
    }

    void Commit(AppSettings next)
    {
        next = next.Clamped();
        var prev = _settings;
        if (prev.StartWithWindows != next.StartWithWindows)
        {
            try
            {
                StartupRegistrar.Apply(next.StartWithWindows, Environment.ProcessPath);
            }
            catch (Exception ex)
            {
                Log.Error("startup registration " + ex.Message);
                MessageBox.Show(
                    "Couldn't update the per-user startup entry.\n\n" + ex.Message,
                    LuxWireEdition.ProductName,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                next = next with { StartWithWindows = prev.StartWithWindows };
            }
        }

        _settings = next;
        try
        {
            _settingsStore.Save(_settings);
        }
        catch (Exception ex)
        {
            Log.Error("settings " + ex.Message);
        }

        ApplyChromeFromSettings();
        if (prev.Dock != next.Dock)
            _appBar.SetDock(next.Dock);
        else
            _appBar.Apply();

        if (prev.LuxWirePosts != next.LuxWirePosts)
        {
            _luxWire.AlertsEnabled = next.LuxWirePosts;
            if (next.LuxWirePosts)
                _luxWire.CatchUp();
        }

        if (prev.LuxWireDigest != next.LuxWireDigest)
            _luxWire.SetDigest(next.LuxWireDigest);

        if (prev.LuxWirePollMinutes != next.LuxWirePollMinutes)
            _luxWire.SetPollMinutes(next.LuxWirePollMinutes);

        if (prev.ShowPercent != next.ShowPercent || prev.ShowSparklines != next.ShowSparklines || prev.PinBtc != next.PinBtc || prev.TextScale != next.TextScale
            || prev.TextColor != next.TextColor || prev.PriceColor != next.PriceColor || prev.ChangeUpColor != next.ChangeUpColor || prev.ChangeDownColor != next.ChangeDownColor
            || prev.ShowPre != next.ShowPre || prev.ShowAfterHours != next.ShowAfterHours
            || prev.LiveScrollUpdates != next.LiveScrollUpdates)
            RenderQuotes();

        UpdateTrayTip();
        _settingsWindow?.Refresh(_settings);
        SyncMenu();
        NoteAccountEdit();
    }

    void SimulateLuxWire()
    {
        if (_luxWireNotifier.Show(LuxWirePost.Simulate()))
            return;
        MessageBox.Show(
            "Couldn't show the sample Lux Wire notification.\n\n" + (_luxWireNotifier.LastError ?? "Windows did not accept the toast."),
            LuxWireEdition.ProductName,
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    void ShowMenuAtCursor()
    {
        if (ContextMenu == null)
            return;
        SyncMenu();
        ContextMenu.Placement = PlacementMode.MousePoint;
        ContextMenu.IsOpen = true;
    }

    internal void ShowSettings() => OpenSettings();

    void SettingsButton_Click(object sender, RoutedEventArgs e) => OpenSettings();

    void BtcButton_Click(object sender, RoutedEventArgs e) => OpenChart();

    void OpenChart()
    {
        if (_chart != null)
        {
            if (_chart.WindowState == WindowState.Minimized)
                _chart.WindowState = WindowState.Normal;
            _chart.Activate();
            return;
        }

        _chart = new BtcChartWindow();
        _chart.Configure(_settings.ChartRange, _settings.ChartStyle, _settings.ChartCrosshair);
        _chart.Closed += (_, _) => _chart = null;
        _chart.Show();
        _chart.Activate();
    }

    void Bar_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
            return;
        _quoteTimer.Stop();
        _pendingSymbol = null;
        OpenSettings();
    }

    void OpenSettings()
    {
        if (_settingsWindow != null)
        {
            _settingsWindow.Refresh(_settings);
            _settingsWindow.ReloadSymbols(_symbols);
            _settingsWindow.ReloadAlerts(_alerts);
            _settingsWindow.RefreshAccount();
            _settingsWindow.Activate();
            return;
        }

        var session = new SettingsSession
        {
            Read = () => _settings,
            Symbols = () => _symbols,
            Alerts = () => _alerts,
            Apply = Commit,
            ReplaceSymbols = ReplaceSymbols,
            ReplaceAlerts = ReplaceAlerts,
            Simulate = SimulateLuxWire,
            RefreshQuotes = () => _poller.RefreshNow(),
            Probe = _probe,
            AuthStatus = XAuthStatus,
            ClientId = XClientId,
            SaveAuth = SaveXAuth,
            SignIn = SignInToX,
            CheckForUpdates = prompt => UpdateClient.CheckAsync(prompt),
            AlertsAvailable = () => XAuthStore.Load(AppPaths.XAuthPath()).CanAlert(DateTimeOffset.UtcNow),
            OpenChart = OpenChart,
            Account = _account
        };
        _settingsWindow = new SettingsWindow(session);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    void NoteAccountEdit()
    {
        if (_applyingRemote)
            return;
        try
        {
            _account.NoteLocalChange();
        }
        catch (Exception ex)
        {
            Log.Error("account edit " + ex.Message);
        }
    }

    internal AccountPayload CaptureAccount()
    {
        if (!Dispatcher.CheckAccess())
        {
            if (Dispatcher.HasShutdownStarted)
                return new AccountPayload("{}", "{\"symbols\":[]}", DateTimeOffset.UtcNow, false, false);
            return Dispatcher.Invoke(CaptureAccount);
        }

        var clock = SyncClock.LoadOrSeed(
            AppPaths.SyncStatePath(),
            AccountSync.SeedStamp(_settingsStore.Path, _symbolStore.Path));
        return new AccountPayload(
            SettingsStore.Serialize(_settings),
            SymbolListStore.Serialize(_symbols),
            clock.UpdatedAt,
            clock.Dirty,
            AccountSync.IsUntouched(_settings, _symbols));
    }

    internal bool ApplyAccount(string settingsJson, string watchlistJson)
    {
        if (!Dispatcher.CheckAccess())
        {
            if (Dispatcher.HasShutdownStarted)
                return false;
            return Dispatcher.Invoke(() => ApplyAccount(settingsJson, watchlistJson));
        }

        if (!SettingsStore.TryParse(settingsJson, out var settings))
            return false;
        if (!SymbolListStore.TryParse(watchlistJson, out var symbols, out _))
            return false;
        _applyingRemote = true;
        try
        {
            Commit(settings);
            ReplaceSymbols(symbols);
            return true;
        }
        finally
        {
            _applyingRemote = false;
        }
    }

    string XAuthStatus() => XAuthStore.Load(AppPaths.XAuthPath()).Describe();

    string? XClientId() => XAuthStore.Load(AppPaths.XAuthPath()).ClientId;

    void SaveXAuth(string? bearerOrNull, string? clientId)
    {
        var stored = XAuthStore.Load(AppPaths.XAuthPath(), includeEnvironment: false);
        var next = stored with
        {
            AppBearer = string.IsNullOrWhiteSpace(bearerOrNull) ? stored.AppBearer : bearerOrNull.Trim(),
            ClientId = string.IsNullOrWhiteSpace(clientId) ? null : clientId.Trim()
        };
        try
        {
            XAuthStore.Save(AppPaths.XAuthPath(), next);
        }
        catch (Exception ex)
        {
            Log.Error("lux wire auth save " + ex.Message);
            return;
        }

        _luxWire.CatchUp();
    }

    async Task<string?> SignInToX(string? clientIdFromBox)
    {
        var stored = XAuthStore.Load(AppPaths.XAuthPath(), includeEnvironment: false);
        var clientId = string.IsNullOrWhiteSpace(clientIdFromBox) ? stored.ClientId : clientIdFromBox.Trim();
        if (string.IsNullOrWhiteSpace(clientId))
            clientId = XAuthStore.Load(AppPaths.XAuthPath()).ClientId;
        if (string.IsNullOrWhiteSpace(clientId))
            return "Add an X client id first.";

        var result = await XSignIn.RunAsync(stored with { ClientId = clientId });
        var issued = result.Token;
        if (result.Error != null || issued == null)
            return result.Error ?? "X did not return a token.";

        var saved = stored with
        {
            ClientId = clientId,
            AccessToken = issued.Value.AccessToken,
            RefreshToken = issued.Value.RefreshToken ?? stored.RefreshToken,
            AccessExpires = DateTimeOffset.UtcNow.AddSeconds(issued.Value.ExpiresIn)
        };
        try
        {
            XAuthStore.Save(AppPaths.XAuthPath(), saved);
        }
        catch (Exception ex)
        {
            Log.Error("lux wire auth save " + ex.Message);
            return "Couldn't save the X sign-in.";
        }

        _luxWire.CatchUp();
        return null;
    }

    void OpenEditor()
    {
        if (_editor != null)
        {
            _editor.Activate();
            return;
        }

        _editor = new EditorWindow(_symbols, _probe, ReplaceSymbols);
        _editor.Closed += (_, _) => _editor = null;
        _editor.Show();
    }

    void ResetHiddenDock()
    {
        _settings = _settings with { Dock = DockEdge.Bottom, MonitorDevice = null };
        try
        {
            _settingsStore.Save(_settings);
        }
        catch (Exception saveEx)
        {
            Log.Error("reset placement " + saveEx.Message);
        }
    }

    void RecreateTray()
    {
        try
        {
            _tray?.Dispose();
            _tray = null;
            _tray = new TrayIcon(this, ShowMenuAtCursor, OpenSettings);
            _luxWireNotifier.UseBalloon(_tray.ShowBalloon);
            UpdateTrayTip();
        }
        catch (Exception ex)
        {
            Log.Error("tray restart " + ex.Message);
        }
    }

    async Task QuietUpdateCheckAsync()
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(6), _appStop);
            if (_closing)
                return;
            var status = await UpdateClient.CheckAsync(false);
            if (!status.StartsWith("newer:", StringComparison.Ordinal))
                return;
            var payload = status["newer:".Length..];
            var split = payload.IndexOf('|');
            var version = split < 0 ? payload : payload[..split];
            await Dispatcher.InvokeAsync(() =>
            {
                if (_closing)
                    return;
                var answer = MessageBox.Show(
                    this,
                    LuxWireEdition.ProductName + " " + version + " is available. Download and install it? Your settings stay.",
                    LuxWireEdition.ProductName,
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);
                if (answer == MessageBoxResult.Yes)
                    _ = UpdateClient.CheckAsync(true);
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Log.Error("update " + ex.Message);
        }
    }

    void ShutdownFeeds()
    {
        _closing = true;
        var chart = _chart;
        _chart = null;
        chart?.Close();
        _clock.Stop();
        _heal.Stop();
        _quoteTimer.Stop();
        _tray?.Dispose();
        _tray = null;
        _luxWire.Dispose();
        _icons?.Dispose();
        _icons = null;
        _watcher?.Dispose();
        _poller.Updated -= OnFeedUpdated;
        _btc.Updated -= OnFeedUpdated;
        _appBar.Dispose();
    }

    static MenuItem Item(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        return item;
    }

    static void StyleMenu(ContextMenu menu)
    {
        var background = PaperBrushes.Freeze(PaperInk.Background);
        var ink = PaperBrushes.Freeze(PaperInk.Ink);
        var highlight = PaperBrushes.Freeze(PaperInk.Accent);
        menu.Background = background;
        menu.Foreground = ink;
        menu.BorderBrush = PaperBrushes.Freeze(PaperInk.Rule);
        menu.BorderThickness = new Thickness(1);
        menu.Resources[SystemColors.MenuBrushKey] = background;
        menu.Resources[SystemColors.MenuTextBrushKey] = ink;
        menu.Resources[SystemColors.HighlightBrushKey] = highlight;
        menu.Resources[SystemColors.HighlightTextBrushKey] = Brushes.White;
        menu.Resources[SystemColors.ControlBrushKey] = background;
        menu.Resources[SystemColors.ControlTextBrushKey] = ink;
        menu.Resources[SystemColors.MenuBarBrushKey] = background;
        menu.Resources[SystemColors.InactiveSelectionHighlightBrushKey] = highlight;
        menu.Resources[SystemColors.InactiveSelectionHighlightTextBrushKey] = Brushes.White;
    }

    sealed record MenuHandles(
        MenuItem DockTop,
        MenuItem DockBottom,
        MenuItem Slow,
        MenuItem Normal,
        MenuItem Fast,
        MenuItem Startup,
        MenuItem LuxWire,
        MenuItem CopySymbol,
        MenuItem CopyPrice);
}
