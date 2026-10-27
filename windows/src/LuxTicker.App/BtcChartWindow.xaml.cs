using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace LuxTicker;

public partial class BtcChartWindow : Window
{
    readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };
    readonly DispatcherTimer _refresh = new();
    readonly List<Button> _presets = new();
    CancellationTokenSource? _loadCts;
    int _generation;
    BtcChartPreset _preset = BtcChart.DefaultPreset;
    BtcChartStyle _style = BtcChartStyle.Line;
    string _yahoo = BtcChart.Symbol;
    string _title = BtcChart.Pair;
    readonly DispatcherTimer _searchTimer = new() { Interval = TimeSpan.FromMilliseconds(280) };
    List<ChartPick> _recent = new();
    List<ChartPick> _suggestions = new();
    bool _searching;
    int _searchGeneration;
    string? _loadedInterval;
    DateOnly? _from;
    DateOnly? _to;
    bool _haveSeries;
    WindowIcons? _icons;

    public BtcChartWindow()
    {
        InitializeComponent();
        _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", FeedEndpoints.UserAgent);
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
        BuildPresets();
        MarkStyle();
        Plot.SetCrosshair(true);
        _searchTimer.Tick += (_, _) =>
        {
            var generation = ++_searchGeneration;
            _ = SearchAsync(generation);
        };
        _recent = LoadRecent();
        SuggestList.DisplayMemberPath = nameof(ChartPick.ListLabel);
        var today = BtcChart.EasternDate(DateTimeOffset.Now);
        FromDate.DisplayDateStart = BtcChart.Earliest.UtcDateTime;
        ToDate.DisplayDateStart = BtcChart.Earliest.UtcDateTime;
        FromDate.DisplayDateEnd = today.ToDateTime(TimeOnly.MinValue);
        ToDate.DisplayDateEnd = today.ToDateTime(TimeOnly.MinValue);
        FromDate.SelectedDate = today.AddDays(-30).ToDateTime(TimeOnly.MinValue);
        ToDate.SelectedDate = today.ToDateTime(TimeOnly.MinValue);
        _refresh.Tick += (_, _) => _ = LoadAsync(quiet: true);
        Loaded += (_, _) => _ = LoadAsync(quiet: false);
        Closed += (_, _) => Shutdown();
        SourceInitialized += (_, _) =>
        {
            PaperChrome.Lock(this);
            try
            {
                _icons = WindowIcons.Apply(this);
            }
            catch (Exception ex)
            {
                Log.Error("btc chart icon " + ex.Message);
            }
        };
    }

    void BuildPresets()
    {
        foreach (var preset in BtcChart.Presets)
        {
            var button = new Button
            {
                Content = BtcChart.PresetLabel(preset),
                Tag = preset,
                MinWidth = preset == BtcChartPreset.Custom ? 76 : 48,
                ToolTip = preset switch
                {
                    BtcChartPreset.Hour => "Last 60 minutes",
                    BtcChartPreset.YearToDate => "Year to date, Eastern time",
                    _ => BtcChart.PresetLabel(preset)
                }
            };
            button.Click += (_, _) => SelectPreset(preset);
            _presets.Add(button);
            PresetBar.Children.Add(button);
        }

        MarkPresets();
    }

    void SelectPreset(BtcChartPreset preset)
    {
        _preset = preset;
        MarkPresets();
        CustomPanel.Visibility = preset == BtcChartPreset.Custom ? Visibility.Visible : Visibility.Collapsed;
        if (preset == BtcChartPreset.Custom)
        {
            _refresh.Stop();
            CaptionText.Text = "Pick From and To, then Apply. Dates use Eastern time.";
            return;
        }

        _ = LoadAsync(quiet: _haveSeries);
    }

    void Line_Click(object sender, RoutedEventArgs e) => SelectStyle(BtcChartStyle.Line);

    void Candles_Click(object sender, RoutedEventArgs e) => SelectStyle(BtcChartStyle.Candles);

    void SelectStyle(BtcChartStyle style)
    {
        if (style == _style)
            return;
        _style = style;
        MarkStyle();
        Plot.SetStyle(style);
        if (!BtcChart.TryRange(_preset, DateTimeOffset.Now, _from, _to, out var start, out var end, out _, BtcChart.HistoryStart(_yahoo)))
            return;
        if (BtcChart.IntervalFor(start, end, style) != _loadedInterval)
            _ = LoadAsync(quiet: _haveSeries);
    }

    void ApplyCustom_Click(object sender, RoutedEventArgs e)
    {
        _from = Selected(FromDate);
        _to = Selected(ToDate);
        _preset = BtcChartPreset.Custom;
        MarkPresets();
        _ = LoadAsync(quiet: _haveSeries);
    }

    void Retry_Click(object sender, RoutedEventArgs e) => _ = LoadAsync(quiet: _haveSeries);

    async Task LoadAsync(bool quiet)
    {
        var now = DateTimeOffset.Now;
        if (!BtcChart.TryRange(_preset, now, _from, _to, out var start, out var end, out var rangeError, BtcChart.HistoryStart(_yahoo)))
        {
            StatusText.Text = rangeError;
            RetryButton.Visibility = Visibility.Collapsed;
            return;
        }

        var generation = ++_generation;
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        var token = _loadCts.Token;
        if (!quiet)
        {
            EmptyText.Visibility = Visibility.Visible;
            EmptyText.Text = "Loading prices…";
            SymbolText.Text = _title;
            PriceText.Text = "—";
            ChangeText.Text = "";
        }
        else
        {
            StatusText.Text = "Updating…";
        }

        RetryButton.Visibility = Visibility.Collapsed;
        try
        {
            var url = BtcChart.ChartUrl(_yahoo, start, end, _style);
            using var response = await _http.GetAsync(url, token);
            if (generation != _generation)
                return;
            if (!response.IsSuccessStatusCode)
            {
                Log.Error("btc chart http " + (int)response.StatusCode);
                ShowFailure(quiet, "Couldn't load Bitcoin prices. Try again.");
                return;
            }

            var json = await response.Content.ReadAsStringAsync(token);
            if (generation != _generation)
                return;
            if (!BtcChart.TryParse(json, out var series, out var failure))
            {
                Log.Error("btc chart parse " + failure);
                var unknown = failure != null && failure.Contains("No data", StringComparison.OrdinalIgnoreCase);
                ShowFailure(quiet, unknown ? ChartSymbols.Missing(_title) : "Couldn't load Bitcoin prices. Try again.");
                return;
            }

            if (series == null)
            {
                ShowFailure(quiet, "No prices in that range.");
                return;
            }

            _haveSeries = true;
            SymbolText.Text = _title;
            Title = _title;
            Remember(_yahoo, _title);
            _loadedInterval = BtcChart.IntervalFor(start, end, _style);
            Plot.SetStyle(_style);
            Plot.SetPoints(series.Points);
            EmptyText.Visibility = Visibility.Collapsed;
            PriceText.Text = "$" + PriceFormat.Format(series.Last, "USD");
            if (series.ChangePercent is { } change)
            {
                ChangeText.Text = PriceFormat.FormatChange(change);
                ChangeText.Foreground = change < 0
                    ? PaperBrushes.Freeze("#C0392B")
                    : PaperBrushes.Freeze("#1B7A3A");
            }
            else
            {
                ChangeText.Text = "";
            }

            CaptionText.Text = BtcChart.PresetLabel(_preset) + " · " + BtcChart.Caption(_preset, start, end, _from, _to);
            var eastern = TimeZoneInfo.ConvertTime(DateTimeOffset.Now, EasternTime.Zone);
            StatusText.Text = "Updated " + eastern.ToString("h:mm tt", System.Globalization.CultureInfo.InvariantCulture) + " ET · Yahoo Finance";
            RetryButton.Visibility = Visibility.Collapsed;
            _refresh.Interval = BtcChart.RefreshInterval(start, end);
            _refresh.Start();
        }
        catch (OperationCanceledException)
        {
            // A newer range replaced this load.
        }
        catch (Exception ex)
        {
            if (generation != _generation)
                return;
            Log.Error("btc chart " + ex.Message);
            ShowFailure(quiet, "Couldn't load Bitcoin prices. Try again.");
        }
    }

    void ShowFailure(bool quiet, string message)
    {
        StatusText.Text = message;
        RetryButton.Visibility = Visibility.Visible;
        if (!quiet || !_haveSeries)
        {
            Plot.SetPoints(Array.Empty<BtcPoint>());
            EmptyText.Visibility = Visibility.Visible;
            EmptyText.Text = message;
            _haveSeries = false;
        }
    }

    void MarkPresets()
    {
        foreach (var button in _presets)
        {
            var on = button.Tag is BtcChartPreset preset && preset == _preset;
            button.Background = on ? PaperBrushes.Freeze(PaperInk.Accent) : PaperBrushes.Freeze(PaperInk.Panel);
            button.Foreground = on ? PaperBrushes.Freeze("#FFFFFF") : PaperBrushes.Freeze(PaperInk.Ink);
            button.BorderBrush = on ? PaperBrushes.Freeze(PaperInk.Accent) : PaperBrushes.Freeze(PaperInk.Rule);
            button.FontWeight = on ? FontWeights.SemiBold : FontWeights.Normal;
        }
    }

    void MarkStyle()
    {
        PaintStyle(LineButton, _style == BtcChartStyle.Line);
        PaintStyle(CandlesButton, _style == BtcChartStyle.Candles);
    }

    static void PaintStyle(Button button, bool on)
    {
        button.Background = on ? PaperBrushes.Freeze(PaperInk.Accent) : PaperBrushes.Freeze(PaperInk.Panel);
        button.Foreground = on ? PaperBrushes.Freeze("#FFFFFF") : PaperBrushes.Freeze(PaperInk.Ink);
        button.BorderBrush = on ? PaperBrushes.Freeze(PaperInk.Accent) : PaperBrushes.Freeze(PaperInk.Rule);
        button.FontWeight = on ? FontWeights.SemiBold : FontWeights.Normal;
    }

    public void Configure(string? range, string? style, bool crosshair)
    {
        _preset = BtcChart.ParsePreset(range);
        _style = string.Equals(style, "candles", StringComparison.OrdinalIgnoreCase) ? BtcChartStyle.Candles : BtcChartStyle.Line;
        MarkPresets();
        MarkStyle();
        Plot.SetStyle(_style);
        Plot.SetCrosshair(crosshair);
    }

    void SymbolSearch_Changed(object sender, TextChangedEventArgs e)
    {
        if (_searching)
            return;
        _searchTimer.Stop();
        _searchTimer.Start();
    }

    void SymbolSearch_GotFocus(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SymbolSearch.Text))
            ShowPicks(_recent);
    }

    void SymbolSearch_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Enter)
            return;
        e.Handled = true;
        if (SuggestList.SelectedItem is ChartPick selected && SuggestList.Visibility == Visibility.Visible)
            CommitPick(selected);
        else
            CommitText(SymbolSearch.Text);
    }

    void Suggest_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not System.Windows.DependencyObject source)
            return;
        if (ItemsControl.ContainerFromElement(SuggestList, source) is not ListBoxItem item)
            return;
        if (item.DataContext is ChartPick pick)
            CommitPick(pick);
    }

    void Suggest_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter && SuggestList.SelectedItem is ChartPick pick)
        {
            e.Handled = true;
            CommitPick(pick);
        }
    }

    async Task SearchAsync(int generation)
    {
        _searchTimer.Stop();
        var text = SymbolSearch.Text.Trim();
        if (generation != _searchGeneration)
            return;
        if (text.Length == 0)
        {
            ShowPicks(_recent);
            return;
        }

        try
        {
            var json = await _http.GetStringAsync(ChartSymbols.SearchUrl(text));
            if (generation != _searchGeneration || !string.Equals(SymbolSearch.Text.Trim(), text, StringComparison.Ordinal))
                return;
            _suggestions = ChartSymbols.ParseSearch(json).ToList();
            ShowPicks(_suggestions);
        }
        catch (Exception ex)
        {
            Log.Error("chart search " + ex.Message);
        }
    }

    void ShowPicks(IReadOnlyList<ChartPick> picks)
    {
        SuggestList.ItemsSource = null;
        SuggestList.ItemsSource = picks.ToList();
        SuggestList.Visibility = picks.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    void CommitText(string text)
    {
        var pick = ChartSymbols.Parse(text);
        if (pick == null)
        {
            StatusText.Text = "Enter a symbol.";
            return;
        }

        var match = _suggestions.FirstOrDefault(item =>
            item.Yahoo.Equals(pick.Yahoo, StringComparison.OrdinalIgnoreCase)
            || item.Symbol.Equals(text.Trim(), StringComparison.OrdinalIgnoreCase));
        CommitPick(match ?? pick);
    }

    void CommitPick(ChartPick pick)
    {
        _searchTimer.Stop();
        _searchGeneration++;
        _searching = true;
        SymbolSearch.Text = pick.Symbol;
        _searching = false;
        SuggestList.Visibility = Visibility.Collapsed;
        _yahoo = pick.Yahoo;
        _title = string.IsNullOrWhiteSpace(pick.Title) ? pick.Symbol : pick.Title;
        SymbolText.Text = _title;
        Title = _title;
        _haveSeries = false;
        _ = LoadAsync(quiet: false);
    }

    void Remember(string yahoo, string title)
    {
        var pick = new ChartPick(title, title, yahoo);
        _recent = ChartSymbols.Remember(_recent, pick).ToList();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AppPaths.ChartRecentPath())!);
            JsonFile.AtomicWrite(AppPaths.ChartRecentPath(), JsonSerializer.Serialize(_recent, JsonFile.Options) + "\n");
        }
        catch (Exception ex)
        {
            Log.Error("chart recent " + ex.Message);
        }
    }

    static List<ChartPick> LoadRecent()
    {
        try
        {
            if (!File.Exists(AppPaths.ChartRecentPath()))
                return new List<ChartPick>();
            var parsed = JsonSerializer.Deserialize<List<ChartPick>>(File.ReadAllText(AppPaths.ChartRecentPath()), JsonFile.Options);
            return parsed ?? new List<ChartPick>();
        }
        catch
        {
            return new List<ChartPick>();
        }
    }

    static DateOnly? Selected(DatePicker picker)
        => picker.SelectedDate is { } date ? DateOnly.FromDateTime(date.Date) : null;

    void Shutdown()
    {
        _searchTimer.Stop();
        _refresh.Stop();
        try
        {
            _loadCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already closed.
        }

        _http.Dispose();
        _loadCts?.Dispose();
        _icons?.Dispose();
        _icons = null;
    }

}
