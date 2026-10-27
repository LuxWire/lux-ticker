using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Navigation;

namespace LuxTicker;

public sealed class SettingsSession
{
    public required Func<AppSettings> Read { get; init; }
    public required Func<IReadOnlyList<SymbolEntry>> Symbols { get; init; }
    public required Func<IReadOnlyList<PriceAlert>> Alerts { get; init; }
    public required Action<AppSettings> Apply { get; init; }
    public required Action<IReadOnlyList<SymbolEntry>> ReplaceSymbols { get; init; }
    public required Action<IReadOnlyList<PriceAlert>> ReplaceAlerts { get; init; }
    public required Action Simulate { get; init; }
    public required Action RefreshQuotes { get; init; }
    public required IQuoteProbe Probe { get; init; }
    public required Func<string> AuthStatus { get; init; }
    public required Func<string?> ClientId { get; init; }
    public required Action<string?, string?> SaveAuth { get; init; }
    public required Func<string?, Task<string?>> SignIn { get; init; }
    public required Func<bool, Task<string>> CheckForUpdates { get; init; }
    public required Func<bool> AlertsAvailable { get; init; }
    public required Action OpenChart { get; init; }
    public required ILuxAccount Account { get; init; }
}

public partial class SettingsWindow : Window
{
    readonly SettingsSession _session;
    bool _syncing;
    WindowIcons? _icons;
    CancellationTokenSource? _probeCts;
    List<SymbolEntry> _symbols = new();
    List<PriceAlert> _alerts = new();
    bool _accountBusy;

    public SettingsWindow(SettingsSession session)
    {
        _session = session;
        InitializeComponent();
        FillLooks();
        LoadFonts();
        VersionText.Text = LuxWireEdition.ProductName + " " + LuxWireEdition.Version;
        UpdatesVersion.Text = LuxWireEdition.ProductName + " " + LuxWireEdition.Version;
        AboutTitle.Text = LuxWireEdition.ProductName + " " + LuxWireEdition.Version;
        BuildNotes();
        _session.Account.Changed += OnAccountChanged;
        ShowSection("Tickers");
        Refresh(_session.Read());
        ReloadSymbols(_session.Symbols());
        ReloadAlerts(_session.Alerts());
        SourceInitialized += (_, _) =>
        {
            PaperChrome.Lock(this);
            try
            {
                _icons = WindowIcons.Apply(this);
            }
            catch (Exception ex)
            {
                Log.Error("settings icon " + ex.Message);
            }
        };
        Closed += (_, _) =>
        {
            _probeCts?.Cancel();
            _session.Account.Changed -= OnAccountChanged;
            _icons?.Dispose();
            _icons = null;
        };
    }

    public void Refresh(AppSettings settings)
    {
        _syncing = true;
        try
        {
            AboveRadio.IsChecked = settings.Dock != DockEdge.Top;
            TopRadio.IsChecked = settings.Dock == DockEdge.Top;
            LuxBox.IsChecked = settings.LuxWirePosts;
            ScrollBox.IsChecked = settings.ScrollEnabled;
            PinBox.IsChecked = settings.PinBtc;
            PreBox.IsChecked = settings.ShowPre;
            AhBox.IsChecked = settings.ShowAfterHours;
            LeftRadio.IsChecked = !settings.ScrollRight;
            RightRadio.IsChecked = settings.ScrollRight;
            LoopBox.IsChecked = settings.ScrollLoop;
            CrosshairBox.IsChecked = settings.ChartCrosshair;
            TopBox.IsChecked = settings.AlwaysOnTop;
            SparkBox.IsChecked = settings.ShowSparklines;
            PercentBox.IsChecked = settings.ShowPercent;
            ClickBox.IsChecked = settings.ClickThrough;
            StartBox.IsChecked = settings.StartWithWindows;
            LiveBox.IsChecked = settings.LiveScrollUpdates;
            CompactRadio.IsChecked = settings.CompactLayout;
            ExpandedRadio.IsChecked = !settings.CompactLayout;
            TextSlider.Value = Math.Clamp(settings.TextScale, TextSlider.Minimum, TextSlider.Maximum);
            OpacitySlider.Value = Math.Clamp(settings.BackgroundOpacity * 100, OpacitySlider.Minimum, OpacitySlider.Maximum);
            SpeedSlider.Value = Math.Clamp(settings.ScrollPixelsPerSecond, SpeedSlider.Minimum, SpeedSlider.Maximum);
            GapSlider.Value = Math.Clamp(settings.SymbolGap, GapSlider.Minimum, GapSlider.Maximum);
            SelectFont(settings.TickerFontName);
            ShowColor(SymbolColorBox, SymbolSwatch, SymbolPreset, settings.TextColor);
            ShowColor(PriceColorBox, PriceSwatch, PricePreset, settings.PriceColor);
            ShowColor(UpColorBox, UpSwatch, UpPreset, settings.ChangeUpColor);
            ShowColor(DownColorBox, DownSwatch, DownPreset, settings.ChangeDownColor);
            ShowColor(BackgroundColorBox, BackgroundSwatch, BackgroundPreset, settings.BackgroundColor, PaperInk.Background);
            SelectLook(settings.AppearancePreset);
            SelectContent(RangeBox, AppSettings.CleanRange(settings.ChartRange));
            SelectContent(StyleBox, string.Equals(settings.ChartStyle, "candles", StringComparison.OrdinalIgnoreCase) ? "Candles" : "Line");
            LoadMonitors(settings.MonitorDevice);
            PollBox.SelectedIndex = settings.LuxWirePollMinutes switch
            {
                2 => 0,
                5 => 2,
                _ => 1
            };
            TextSizeLabel.Text = Math.Round(TextSlider.Value * 100).ToString("0") + "%";
            OpacityLabel.Text = Math.Round(OpacitySlider.Value).ToString("0") + "%";
            SpeedLabel.Text = Math.Round(SpeedSlider.Value).ToString("0") + " px/s";
            GapLabel.Text = GapSlider.Value.ToString("0.##", CultureInfo.InvariantCulture) + "×";
            OffsetSlider.Minimum = BarOffset.MinDip;
            OffsetSlider.Maximum = BarOffset.MaxDip;
            OffsetSlider.Value = BarOffset.Clamp(settings.VerticalOffsetDip);
            OffsetLabel.Text = BarOffset.Format((int)Math.Round(OffsetSlider.Value));
            ApplyAlertVisibility();
        }
        finally
        {
            _syncing = false;
        }
    }

    public void ReloadSymbols(IReadOnlyList<SymbolEntry> symbols)
    {
        _symbols = symbols.ToList();
        SymbolList.ItemsSource = null;
        SymbolList.ItemsSource = _symbols;
        EmptyText.Visibility = _symbols.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public void ReloadAlerts(IReadOnlyList<PriceAlert> alerts)
    {
        _alerts = alerts.ToList();
        AlertList.ItemsSource = null;
        AlertList.ItemsSource = _alerts.Select(alert => alert.Symbol + (alert.Above ? " above " : " below ") + alert.Price).ToList();
    }

    void LoadFonts()
    {
        var installed = new List<string>();
        foreach (var family in Fonts.SystemFontFamilies)
        {
            var name = family.Source;
            if (!string.IsNullOrWhiteSpace(name))
                installed.Add(name);
        }

        FontBox.Items.Clear();
        foreach (var row in TickerFontList.Build(installed))
        {
            FontBox.Items.Add(new ComboBoxItem
            {
                Content = row.Label,
                Tag = row.Name,
                IsEnabled = !row.Header,
                Focusable = !row.Header,
                FontWeight = row.Header ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = PaperBrushes.Freeze(row.Header ? PaperInk.Muted : PaperInk.Body)
            });
        }
    }

    void SelectFont(string? name)
    {
        var wanted = TickerFont.IsDefault(name) ? TickerFont.DefaultName : name!.Trim();
        foreach (ComboBoxItem choice in FontBox.Items)
        {
            if (choice.IsEnabled && string.Equals(choice.Tag as string, wanted, StringComparison.OrdinalIgnoreCase))
            {
                FontBox.SelectedItem = choice;
                return;
            }
        }

        foreach (ComboBoxItem choice in FontBox.Items)
        {
            if (choice.IsEnabled && string.Equals(choice.Tag as string, TickerFont.DefaultName, StringComparison.OrdinalIgnoreCase))
            {
                FontBox.SelectedItem = choice;
                return;
            }
        }
    }

    string SelectedFont()
    {
        if (FontBox.SelectedItem is ComboBoxItem item && item.Tag is string name && !string.IsNullOrWhiteSpace(name))
            return name;
        return TickerFont.DefaultName;
    }

    void Option_Changed(object sender, RoutedEventArgs e) => Push();

    void Slider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e) => Push();

    void Speed_Changed(object sender, SelectionChangedEventArgs e) => Push();

    void Push()
    {
        if (_syncing || !IsLoaded || PollBox.SelectedIndex < 0 || RangeBox.SelectedItem == null)
            return;
        var pixels = ScrollPhysics.ClampPixels(SpeedSlider.Value);
        var speed = Math.Abs(pixels - ScrollPhysics.PixelsPerSecond(ScrollSpeed.Slow)) < 0.1
            ? ScrollSpeed.Slow
            : Math.Abs(pixels - ScrollPhysics.PixelsPerSecond(ScrollSpeed.Fast)) < 0.1
                ? ScrollSpeed.Fast
                : ScrollSpeed.Normal;
        var current = _session.Read();
        var next = new AppSettings(
            TopRadio.IsChecked == true ? DockEdge.Top : DockEdge.Bottom,
            speed,
            StartBox.IsChecked == true,
            LuxBox.IsChecked == true,
            current.Opacity,
            TopBox.IsChecked == true,
            ScrollBox.IsChecked == true,
            PercentBox.IsChecked == true,
            CompactRadio.IsChecked != false,
            SparkBox.IsChecked == true,
            ClickBox.IsChecked == true,
            PinBox.IsChecked == true,
            TextSlider.Value,
            current.LuxWireDigest,
            PollBox.SelectedIndex switch
            {
                0 => 2,
                2 => 5,
                _ => 3
            },
            MonitorChoice(),
            (int)Math.Round(OffsetSlider.Value),
            OpacitySlider.Value / 100.0,
            pixels,
            SelectedFont(),
            GapSlider.Value,
            PreBox.IsChecked == true,
            AhBox.IsChecked == true,
            RightRadio.IsChecked == true,
            LoopBox.IsChecked != false,
            ColorOr(SymbolColorBox, current.TextColor),
            (RangeBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "1M",
            string.Equals((StyleBox.SelectedItem as ComboBoxItem)?.Content?.ToString(), "Candles", StringComparison.OrdinalIgnoreCase) ? "candles" : "line",
            CrosshairBox.IsChecked != false,
            ColorOr(PriceColorBox, current.PriceColor),
            ColorOr(UpColorBox, current.ChangeUpColor),
            ColorOr(DownColorBox, current.ChangeDownColor),
            current.BackgroundOpacityChosen || Math.Abs(OpacitySlider.Value / 100.0 - current.BackgroundOpacity) > 0.001,
            ColorOr(BackgroundColorBox, current.BackgroundColor),
            LiveBox.IsChecked == true,
            LookName()).Clamped();
        TextSizeLabel.Text = Math.Round(next.TextScale * 100).ToString("0") + "%";
        OpacityLabel.Text = Math.Round(next.BackgroundOpacity * 100).ToString("0") + "%";
        SpeedLabel.Text = Math.Round(next.ScrollPixelsPerSecond).ToString("0") + " px/s";
        GapLabel.Text = next.SymbolGap.ToString("0.##", CultureInfo.InvariantCulture) + "×";
        OffsetLabel.Text = BarOffset.Format(next.VerticalOffsetDip);
        PaintSwatch(SymbolSwatch, next.TextColor);
        PaintSwatch(PriceSwatch, next.PriceColor);
        PaintSwatch(UpSwatch, next.ChangeUpColor);
        PaintSwatch(DownSwatch, next.ChangeDownColor);
        PaintSwatch(BackgroundSwatch, next.BackgroundColor);
        ApplyAlertVisibility();
        _session.Apply(next);
    }

    void FillLooks()
    {
        LookBox.Items.Clear();
        foreach (var look in AppearancePresets.All)
            LookBox.Items.Add(new ComboBoxItem { Content = look.Name, Tag = look.Name });
        LookBox.Items.Add(new ComboBoxItem { Content = AppearancePresets.Custom, Tag = AppearancePresets.Custom });
    }

    void SelectLook(string? name)
    {
        var wanted = AppearancePresets.Canonical(string.IsNullOrWhiteSpace(name) ? AppearancePresets.Custom : name);
        foreach (ComboBoxItem choice in LookBox.Items)
        {
            if (string.Equals(choice.Tag as string, wanted, StringComparison.OrdinalIgnoreCase))
            {
                LookBox.SelectedItem = choice;
                return;
            }
        }
    }

    string LookName()
    {
        var symbol = ColorOr(SymbolColorBox, _session.Read().TextColor);
        var price = ColorOr(PriceColorBox, _session.Read().PriceColor);
        var up = ColorOr(UpColorBox, _session.Read().ChangeUpColor);
        var down = ColorOr(DownColorBox, _session.Read().ChangeDownColor);
        var background = ColorOr(BackgroundColorBox, _session.Read().BackgroundColor);
        return AppearancePresets.Match(background, symbol, price, up, down);
    }

    void Look_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || LookBox.SelectedItem is not ComboBoxItem item || item.Tag is not string name)
            return;
        var look = AppearancePresets.Find(name);
        if (look == null)
        {
            _syncing = true;
            SelectLook(LookName());
            _syncing = false;
            return;
        }
        _syncing = true;
        SymbolColorBox.Text = look.Symbol;
        PriceColorBox.Text = look.Price;
        UpColorBox.Text = look.Up;
        DownColorBox.Text = look.Down;
        BackgroundColorBox.Text = look.Background;
        PaintSwatch(SymbolSwatch, look.Symbol);
        PaintSwatch(PriceSwatch, look.Price);
        PaintSwatch(UpSwatch, look.Up);
        PaintSwatch(DownSwatch, look.Down);
        PaintSwatch(BackgroundSwatch, look.Background);
        SelectPreset(SymbolPreset, look.Symbol);
        SelectPreset(PricePreset, look.Price);
        SelectPreset(UpPreset, look.Up);
        SelectPreset(DownPreset, look.Down);
        SelectPreset(BackgroundPreset, look.Background);
        _syncing = false;
        Push();
    }

    void CustomBackground_Click(object sender, RoutedEventArgs e)
    {
        if (!ColorPicker.TryPick(this, BackgroundColorBox.Text, out var hex))
            return;
        BackgroundColorBox.Text = hex;
        Push();
    }

    void OpenChart_Click(object sender, RoutedEventArgs e) => _session.OpenChart();

    void Color_Changed(object sender, TextChangedEventArgs e) => Push();

    void Preset_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || sender is not ComboBox box || box.SelectedItem is not ComboBoxItem item || item.Tag is not string hex)
            return;
        var target = box.Name switch
        {
            "SymbolPreset" => SymbolColorBox,
            "PricePreset" => PriceColorBox,
            "UpPreset" => UpColorBox,
            "DownPreset" => DownColorBox,
            "BackgroundPreset" => BackgroundColorBox,
            _ => null
        };
        if (target != null && !string.Equals(target.Text?.Trim(), hex, StringComparison.OrdinalIgnoreCase))
            target.Text = hex;
    }

    void ResetSymbolColor_Click(object sender, RoutedEventArgs e)
    {
        SymbolColorBox.Text = AppearancePresets.WallSymbol;
        Push();
    }

    void ResetPriceColor_Click(object sender, RoutedEventArgs e)
    {
        PriceColorBox.Text = AppearancePresets.WallPrice;
        Push();
    }

    void ResetUpColor_Click(object sender, RoutedEventArgs e)
    {
        UpColorBox.Text = AppearancePresets.WallUp;
        Push();
    }

    void ResetDownColor_Click(object sender, RoutedEventArgs e)
    {
        DownColorBox.Text = AppearancePresets.WallDown;
        Push();
    }

    void ResetBackgroundColor_Click(object sender, RoutedEventArgs e)
    {
        BackgroundColorBox.Text = AppearancePresets.WallBackground;
        Push();
    }

    static string ColorOr(TextBox box, string current)
    {
        var raw = box.Text?.Trim() ?? "";
        if (raw.Length == 6 && raw[0] != '#')
            raw = "#" + raw;
        if (raw.Length != 7)
            return current;
        var clamped = TickerTextColor.Clamp(raw);
        return string.Equals(clamped, raw, StringComparison.OrdinalIgnoreCase) ? clamped : current;
    }

    void ShowColor(TextBox box, System.Windows.Controls.Border swatch, ComboBox preset, string hex, string? fallback = null)
    {
        var color = fallback == null
            ? TickerTextColor.Clamp(string.IsNullOrWhiteSpace(hex) ? TickerTextColor.Default : hex)
            : TickerTextColor.Parse(hex, fallback);
        if (fallback == null && box.Name == "PriceColorBox" && string.IsNullOrWhiteSpace(hex))
            color = TickerTextColor.PriceDefault;
        if (fallback == null && box.Name == "UpColorBox" && string.IsNullOrWhiteSpace(hex))
            color = TickerTextColor.UpDefault;
        if (fallback == null && box.Name == "DownColorBox" && string.IsNullOrWhiteSpace(hex))
            color = TickerTextColor.DownDefault;
        box.Text = color;
        PaintSwatch(swatch, color);
        SelectPreset(preset, color);
    }

    static void PaintSwatch(System.Windows.Controls.Border swatch, string hex)
    {
        try
        {
            swatch.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }
        catch (Exception ex)
        {
            Log.Error("color " + ex.Message);
        }
    }

    static void SelectPreset(ComboBox box, string hex)
    {
        foreach (var item in box.Items)
        {
            if (item is ComboBoxItem choice && string.Equals(choice.Tag as string, hex, StringComparison.OrdinalIgnoreCase))
            {
                box.SelectedItem = choice;
                return;
            }
        }

        box.SelectedIndex = -1;
    }

    async void Add_Click(object sender, RoutedEventArgs e)
    {
        var typed = SymbolBox.Text;
        var normalized = SymbolEntry.NormalizeSymbol(typed);
        if (string.IsNullOrEmpty(normalized))
        {
            ErrorText.Text = "Enter a symbol.";
            return;
        }

        if (!SymbolEntry.IsValidSymbol(normalized))
        {
            ErrorText.Text = "Symbols can use letters, numbers, and . - ^ = _.";
            return;
        }

        var inferred = SymbolEntry.Infer(typed, null);
        var preflight = SymbolListEditor.Add(_symbols, inferred, ProbeStatus.Found);
        if (!preflight.Ok && _symbols.Any(item => item.Symbol.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
        {
            ErrorText.Text = preflight.Error;
            return;
        }

        _probeCts?.Cancel();
        _probeCts = new CancellationTokenSource();
        var token = _probeCts.Token;
        AddButton.IsEnabled = false;
        ErrorText.Text = "";
        try
        {
            var probe = await _session.Probe.ProbeAsync(normalized, token);
            var entry = inferred.WithQuoteCurrency(probe.Currency);
            var result = SymbolListEditor.Add(_symbols, entry, probe.Status);
            if (!result.Ok)
            {
                ErrorText.Text = result.Error ?? "Couldn't add that symbol.";
                return;
            }

            _session.ReplaceSymbols(result.Symbols);
            SymbolBox.Clear();
            ErrorText.Text = "";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            ErrorText.Text = "Couldn't reach the quote feed. Check the connection and try again.";
            Log.Error("probe " + ex.Message);
        }
        finally
        {
            AddButton.IsEnabled = true;
        }
    }

    void Remove_Click(object sender, RoutedEventArgs e)
    {
        int index = SymbolList.SelectedIndex;
        if (index < 0)
        {
            ErrorText.Text = "Select a ticker to remove.";
            return;
        }

        var next = SymbolListEditor.RemoveAt(_symbols, index);
        if (ReferenceEquals(next, _symbols))
            return;
        ErrorText.Text = "";
        _session.ReplaceSymbols(next);
    }

    void AddAlert_Click(object sender, RoutedEventArgs e)
    {
        var symbol = SymbolEntry.NormalizeSymbol(AlertSymbol.Text);
        if (string.IsNullOrEmpty(symbol))
        {
            AlertError.Text = "Enter a symbol.";
            return;
        }

        if (!decimal.TryParse(AlertPrice.Text.Trim(), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var price)
            && !decimal.TryParse(AlertPrice.Text.Trim(), out price))
        {
            AlertError.Text = "Enter a price.";
            return;
        }

        if (price <= 0)
        {
            AlertError.Text = "Enter a price.";
            return;
        }

        var above = AlertSide.SelectedIndex != 1;
        var next = _alerts.ToList();
        next.Add(new PriceAlert(symbol, above, price));
        AlertError.Text = "";
        AlertSymbol.Clear();
        AlertPrice.Clear();
        _session.ReplaceAlerts(next);
    }

    void RemoveAlert_Click(object sender, RoutedEventArgs e)
    {
        int index = AlertList.SelectedIndex;
        if (index < 0 || index >= _alerts.Count)
        {
            AlertError.Text = "Select an alert to remove.";
            return;
        }

        var next = _alerts.ToList();
        next.RemoveAt(index);
        AlertError.Text = "";
        _session.ReplaceAlerts(next);
    }

    void Refresh_Click(object sender, RoutedEventArgs e) => _session.RefreshQuotes();

    async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        CheckUpdatesButton.IsEnabled = false;
        UpdateStatus.Text = "Checking for updates…";
        try
        {
            var status = await _session.CheckForUpdates(false);
            if (status.StartsWith("newer:", StringComparison.Ordinal))
            {
                var payload = status["newer:".Length..];
                var split = payload.IndexOf('|');
                var version = split < 0 ? payload : payload[..split];
                var answer = MessageBox.Show(
                    this,
                    LuxWireEdition.ProductName + " " + version + " is available. Download and install it? Your settings stay.",
                    LuxWireEdition.ProductName,
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);
                status = answer == MessageBoxResult.Yes
                    ? await _session.CheckForUpdates(true)
                    : LuxWireEdition.ProductName + " " + version + " is available. Your settings stay if you install it.";
            }

            UpdateStatus.Text = status;
        }
        catch (Exception ex)
        {
            UpdateStatus.Text = UpdateClient.Manual();
            Log.Error("update " + ex.Message);
        }
        finally
        {
            CheckUpdatesButton.IsEnabled = true;
        }
    }

    void Follow_Click(object sender, RoutedEventArgs e) => OpenLink("https://x.com/LuxWire");

    void Subscribe_Click(object sender, RoutedEventArgs e) => OpenLink("https://x.com/LuxWire/creator-subscriptions/subscribe");

    void Privacy_Click(object sender, RoutedEventArgs e) => OpenLink(LuxAccount.PrivacyUrl);

    void Terms_Click(object sender, RoutedEventArgs e) => OpenLink(LuxAccount.TermsUrl);

    void BuildNotes()
    {
        WhatsNewHost.Children.Clear();
        foreach (var note in ReleaseNotes.Displayed)
        {
            WhatsNewHost.Children.Add(new TextBlock
            {
                Text = note.Version + "  ·  " + note.Date,
                FontWeight = FontWeights.SemiBold,
                Foreground = PaperBrushes.Freeze(PaperInk.Ink),
                Margin = new Thickness(0, 10, 0, 4)
            });
            foreach (var bullet in note.Bullets)
            {
                WhatsNewHost.Children.Add(new TextBlock
                {
                    Text = "•  " + bullet,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = PaperBrushes.Freeze(PaperInk.Body),
                    Margin = new Thickness(8, 1, 0, 2)
                });
            }
        }
    }

    void Section_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string section)
        {
            SearchBox.Clear();
            ShowSection(section);
        }
    }

    void ShowSection(string section)
    {
        PageTickers.Visibility = section == "Tickers" ? Visibility.Visible : Visibility.Collapsed;
        PageAppearance.Visibility = section == "Appearance" ? Visibility.Visible : Visibility.Collapsed;
        PageMotion.Visibility = section == "Motion" ? Visibility.Visible : Visibility.Collapsed;
        PagePosition.Visibility = section == "Position" ? Visibility.Visible : Visibility.Collapsed;
        PageCharts.Visibility = section == "Charts" ? Visibility.Visible : Visibility.Collapsed;
        PageLux.Visibility = section == "Lux" && _session.AlertsAvailable() ? Visibility.Visible : Visibility.Collapsed;
        PageAccount.Visibility = section == "Account" ? Visibility.Visible : Visibility.Collapsed;
        PageUpdates.Visibility = section == "Updates" ? Visibility.Visible : Visibility.Collapsed;
        PageAbout.Visibility = section == "About" ? Visibility.Visible : Visibility.Collapsed;
        MarkNav(NavTickers, section == "Tickers");
        MarkNav(NavAppearance, section == "Appearance");
        MarkNav(NavMotion, section == "Motion");
        MarkNav(NavPosition, section == "Position");
        MarkNav(NavCharts, section == "Charts");
        MarkNav(NavLux, section == "Lux");
        MarkNav(NavAccount, section == "Account");
        MarkNav(NavUpdates, section == "Updates");
        MarkNav(NavAbout, section == "About");
        ApplyAlertVisibility();
        ApplySearch();
    }

    void ApplyAlertVisibility()
    {
        var available = _session.AlertsAvailable();
        NavLux.Visibility = available ? Visibility.Visible : Visibility.Collapsed;
        PollPanel.Visibility = LuxBox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        if (!available)
            PageLux.Visibility = Visibility.Collapsed;
    }

    static void MarkNav(Button button, bool on)
    {
        button.Background = on ? PaperBrushes.Freeze("#D4A017") : PaperBrushes.Freeze(PaperInk.Panel);
        button.Foreground = on ? PaperBrushes.Freeze(PaperInk.Body) : PaperBrushes.Freeze(PaperInk.Ink);
        button.FontWeight = on ? FontWeights.SemiBold : FontWeights.Normal;
    }

    void Search_Changed(object sender, TextChangedEventArgs e) => ApplySearch();

    void ApplySearch()
    {
        var query = SearchBox.Text.Trim();
        var searching = query.Length > 0;
        var pages = new[] { PageTickers, PageAppearance, PageMotion, PagePosition, PageCharts, PageLux, PageAccount, PageUpdates, PageAbout };
        if (!searching)
        {
            foreach (var page in pages)
            {
                foreach (var child in page.Children)
                {
                    if (child is FrameworkElement row && row.Tag is string tag && tag.Contains('|'))
                        row.Visibility = Visibility.Visible;
                }
            }

            RefreshAccount();
            return;
        }

        foreach (var page in pages)
        {
            if (page == PageLux && !_session.AlertsAvailable())
            {
                page.Visibility = Visibility.Collapsed;
                continue;
            }

            var any = false;
            foreach (var child in page.Children)
            {
                if (child is not FrameworkElement row || row.Tag is not string tag || !tag.Contains('|'))
                    continue;
                var name = tag[(tag.IndexOf('|') + 1)..];
                var show = name.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || tag.Contains(query, StringComparison.OrdinalIgnoreCase);
                row.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                if (show)
                    any = true;
            }

            page.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
        }

        RefreshAccount();
    }

    public void RefreshAccount()
    {
        var snap = _session.Account.Current;
        SignedOutPanel.Visibility = snap.SignedIn ? Visibility.Collapsed : Visibility.Visible;
        SignedInPanel.Visibility = snap.SignedIn ? Visibility.Visible : Visibility.Collapsed;
        AccountStatus.Text = snap.Status;
        AccountSignedEmail.Text = string.IsNullOrWhiteSpace(snap.Email) ? "Signed in" : snap.Email;
        AccountTierText.Text = "Tier: " + snap.TierLabel;
        AccountPerkText.Text = snap.Tier == AccountTier.Free
            ? "Sync is off on the free tier."
            : "Signed-in perk: settings and the watchlist stay with the account.";
        if (!AccountUsernameBox.IsKeyboardFocusWithin)
            AccountUsernameBox.Text = snap.Username ?? "";
        OAuthPanel.Visibility = LuxAccount.OAuthEnabled && !snap.SignedIn ? Visibility.Visible : Visibility.Collapsed;
    }

    void OnAccountChanged(object? sender, EventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(RefreshAccount);
            return;
        }

        RefreshAccount();
    }

    async void CreateAccount_Click(object sender, RoutedEventArgs e)
        => await RunAccount(() => _session.Account.CreateAccount(AccountIdBox.Text, AccountPassword.Password, AccountNewUsername.Text), clearPassword: true);

    async void SignInAccount_Click(object sender, RoutedEventArgs e)
        => await RunAccount(() => _session.Account.SignIn(AccountIdBox.Text, AccountPassword.Password), clearPassword: true);

    async void ForgotPassword_Click(object sender, RoutedEventArgs e)
        => await RunAccount(() => _session.Account.SendReset(AccountIdBox.Text), clearPassword: false);

    async void SaveUsername_Click(object sender, RoutedEventArgs e)
        => await RunAccount(() => _session.Account.SetUsername(AccountUsernameBox.Text), clearPassword: false);

    async void SignOutAccount_Click(object sender, RoutedEventArgs e)
        => await RunAccount(() => _session.Account.SignOut(), clearPassword: true);

    async void DeleteAccount_Click(object sender, RoutedEventArgs e)
    {
        var answer = MessageBox.Show(
            this,
            "Delete this LuxWire account? The ticker on this PC keeps its settings. The copy stored with the account is removed.",
            LuxWireEdition.ProductName,
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);
        if (answer != MessageBoxResult.OK)
            return;
        await RunAccount(() => _session.Account.DeleteAccount(), clearPassword: true);
    }

    async void GoogleAccount_Click(object sender, RoutedEventArgs e)
        => await RunAccount(() => _session.Account.ContinueWith("google"), clearPassword: false);

    async void XAccount_Click(object sender, RoutedEventArgs e)
        => await RunAccount(() => _session.Account.ContinueWith("x"), clearPassword: false);

    async Task RunAccount(Func<Task<AccountOutcome>> action, bool clearPassword)
    {
        if (_accountBusy)
            return;
        _accountBusy = true;
        try
        {
            AccountStatus.Text = "Working…";
            var outcome = await action();
            if (clearPassword && outcome.Ok)
                AccountPassword.Clear();
            RefreshAccount();
            AccountStatus.Text = outcome.Message;
        }
        catch (Exception ex)
        {
            Log.Error("account " + ex.Message);
            AccountStatus.Text = "Couldn't reach the account service. The ticker still works on this PC.";
        }
        finally
        {
            _accountBusy = false;
        }
    }

    void ResetTickers_Click(object sender, RoutedEventArgs e)
    {
        _session.Apply(SettingsReset.Apply(_session.Read(), SettingsReset.Tickers));
        _session.ReplaceSymbols(DefaultSymbols.Load());
        _session.ReplaceAlerts(Array.Empty<PriceAlert>());
    }

    void ResetAppearance_Click(object sender, RoutedEventArgs e) => _session.Apply(SettingsReset.Apply(_session.Read(), SettingsReset.Appearance));

    void ResetMotion_Click(object sender, RoutedEventArgs e) => _session.Apply(SettingsReset.Apply(_session.Read(), SettingsReset.Motion));

    void ResetPosition_Click(object sender, RoutedEventArgs e) => _session.Apply(SettingsReset.Apply(_session.Read(), SettingsReset.Position));

    void ResetCharts_Click(object sender, RoutedEventArgs e) => _session.Apply(SettingsReset.Apply(_session.Read(), SettingsReset.Charts));

    void ResetLux_Click(object sender, RoutedEventArgs e) => _session.Apply(SettingsReset.Apply(_session.Read(), SettingsReset.LuxWire));

    void ResetAll_Click(object sender, RoutedEventArgs e)
    {
        var answer = MessageBox.Show(
            this,
            "Reset ALL settings to defaults? Tickers and price alerts go back too. The X login and the LuxWire account stay.",
            LuxWireEdition.ProductName,
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);
        if (answer != MessageBoxResult.OK)
            return;
        _session.Apply(SettingsReset.Apply(_session.Read(), null));
        _session.ReplaceSymbols(DefaultSymbols.Load());
        _session.ReplaceAlerts(Array.Empty<PriceAlert>());
    }

    void MoveUp_Click(object sender, RoutedEventArgs e) => MoveSymbol(-1);

    void MoveDown_Click(object sender, RoutedEventArgs e) => MoveSymbol(1);

    void MoveSymbol(int delta)
    {
        int index = SymbolList.SelectedIndex;
        if (index < 0)
        {
            ErrorText.Text = "Select a ticker to move.";
            return;
        }

        var next = SymbolListEditor.Move(_symbols, index, delta);
        if (ReferenceEquals(next, _symbols))
            return;
        ErrorText.Text = "";
        _session.ReplaceSymbols(next);
        var moved = Math.Clamp(index + delta, 0, next.Count - 1);
        SymbolList.SelectedIndex = moved;
    }

    static void SelectContent(ComboBox box, string content)
    {
        foreach (var item in box.Items)
        {
            if (item is ComboBoxItem choice && string.Equals(choice.Content?.ToString(), content, StringComparison.OrdinalIgnoreCase))
            {
                box.SelectedItem = choice;
                return;
            }
        }

        if (box.Items.Count > 0)
            box.SelectedIndex = 0;
    }

    void LoadMonitors(string? selected)
    {
        var devices = new List<string> { "Primary" };
        try
        {
            foreach (var device in MonitorCatalog.Devices())
            {
                if (!devices.Contains(device, StringComparer.OrdinalIgnoreCase))
                    devices.Add(device);
            }
        }
        catch (Exception ex)
        {
            Log.Error("monitors " + ex.Message);
        }

        if (!string.IsNullOrWhiteSpace(selected) && !devices.Contains(selected, StringComparer.OrdinalIgnoreCase))
            devices.Add(selected);
        MonitorBox.ItemsSource = devices;
        MonitorBox.SelectedItem = string.IsNullOrWhiteSpace(selected)
            ? "Primary"
            : devices.First(item => item.Equals(string.IsNullOrWhiteSpace(selected) ? "Primary" : selected, StringComparison.OrdinalIgnoreCase));
    }

    string? MonitorChoice()
    {
        var choice = MonitorBox.SelectedItem as string;
        return string.IsNullOrWhiteSpace(choice) || choice == "Primary" ? null : choice;
    }

    void Contact_Navigate(object sender, RequestNavigateEventArgs e)
    {
        e.Handled = true;
        OpenLink(e.Uri?.AbsoluteUri);
    }

    void Close_Click(object sender, RoutedEventArgs e) => Close();

    static void OpenLink(string? url)
    {
        if (!LuxWireLinks.IsHttp(url) || url == null)
            return;
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Error("contact " + ex.Message);
        }
    }

}
