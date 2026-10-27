using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace LuxTicker;

/// <summary>
/// Draws live text. No bitmap cache: a cached strip resampled on a fractional scroll is what made the type soft.
/// Font size and the scroll offset snap to whole device pixels so ClearType (or grayscale, when the window is translucent) stays put.
/// </summary>
public sealed class TickerStrip : Grid
{
    readonly Grid _row = new();
    readonly StackPanel _pinned = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Stretch };
    // A Grid layout-clips a child to the cell before RenderTransform, so a second
    // copy past that cell never enters and the bar freezes after one pass.
    // Canvas keeps both copies and only clips the viewport.
    readonly Canvas _viewport = new()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch,
        ClipToBounds = true,
        Background = Brushes.Transparent
    };
    readonly TextBlock _badge = new()
    {
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
        Margin = new Thickness(6, 1, 0, 0),
        FontWeight = FontWeights.Bold,
        IsHitTestVisible = false
    };
    FrameworkElement? _marquee;
    FrameworkElement? _lead;
    FontFamily _font = new("Consolas");
    IReadOnlyList<DisplayItem> _items = Array.Empty<DisplayItem>();
    IReadOnlyList<string> _pinnedSymbols = Array.Empty<string>();
    string _signature = "";
    readonly DispatcherTimer _scrollTimer;
    double _loopWidth;
    double _offset;
    DateTime _lastTick;
    bool _hover;
    double _builtDpi = -1;
    double _builtHeight = -1;
    double _textScale = 1;
    double _symbolGap = TickerSpacing.Default;
    bool _translucent = true;
    string _badgeText = "";
    bool _liveUpdates;
    readonly List<TapeChunk> _chunks = new();
    readonly Dictionary<string, decimal?> _tapePrices = new(StringComparer.OrdinalIgnoreCase);

    public TickerStrip()
    {
        ClipToBounds = true;
        Background = Brushes.Transparent;
        SnapsToDevicePixels = true;
        UseLayoutRounding = true;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
        RenderOptions.SetClearTypeHint(this, ClearTypeHint.Enabled);
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);
        TextOptions.SetTextHintingMode(this, TextHintingMode.Fixed);

        _row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(_pinned, 0);
        Grid.SetColumn(_viewport, 1);
        _row.Children.Add(_pinned);
        _row.Children.Add(_viewport);
        Children.Add(_row);
        Children.Add(_badge);
        ApplyText(this);
        MouseEnter += (_, _) => _hover = true;
        MouseLeave += (_, _) => _hover = false;
        SizeChanged += (_, _) => Rebuild(force: true);
        _scrollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _scrollTimer.Tick += (_, _) => OnTick();
        Loaded += (_, _) => _scrollTimer.Start();
        Unloaded += (_, _) => _scrollTimer.Stop();
    }

    public event Action<string>? SymbolClicked;

    public FontFamily TickerFontFamily
    {
        get => _font;
        set
        {
            _font = value;
            Rebuild(force: true);
        }
    }

    /// <summary>Physical pixels per second.</summary>
    public double PixelsPerSecond { get; set; } = 90;

    public bool MenuOpen { get; set; }

    public bool ScrollEnabled { get; set; } = true;

    public bool ScrollLoop { get; set; } = true;

    public bool ScrollRight { get; set; }

    /// <summary>When on, a price change rewrites the moving type and flashes up green or down red.</summary>
    public bool LiveUpdates
    {
        get => _liveUpdates;
        set => _liveUpdates = value;
    }

    public string UpFlashColor { get; set; } = TickerTextColor.UpDefault;

    public string DownFlashColor { get; set; } = TickerTextColor.DownDefault;

    public double SymbolGap
    {
        get => _symbolGap;
        set
        {
            var next = TickerSpacing.Clamp(value);
            if (Math.Abs(next - _symbolGap) < 0.0001)
                return;
            _symbolGap = next;
            Rebuild(force: true);
        }
    }

    /// <summary>Grayscale when the paper is translucent, so the type stays readable.</summary>
    public bool TranslucentBackground
    {
        get => _translucent;
        set
        {
            if (_translucent == value)
                return;
            _translucent = value;
            ApplyText(this);
            Rebuild(force: true);
        }
    }

    public double TextScale
    {
        get => _textScale;
        set
        {
            var next = value < 0.5 ? 1 : value;
            if (Math.Abs(next - _textScale) < 0.001)
                return;
            _textScale = next;
            Rebuild(force: true);
        }
    }

    public string Badge
    {
        get => _badgeText;
        set
        {
            _badgeText = value ?? "";
            _badge.Text = _badgeText;
            _badge.Visibility = _badgeText.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    public IReadOnlyList<string> PinnedSymbols
    {
        get => _pinnedSymbols;
        set
        {
            _pinnedSymbols = value ?? Array.Empty<string>();
            Rebuild(force: true);
        }
    }

    public void SetItems(IReadOnlyList<DisplayItem> items)
    {
        var directions = Directions(items);
        _items = items;
        if (_liveUpdates && TryPatch(directions))
            return;
        Rebuild(force: false, directions);
    }

    public string? SymbolAt(Point point)
    {
        var hit = VisualTreeHelper.HitTest(this, point);
        DependencyObject? node = hit?.VisualHit as DependencyObject;
        while (node != null)
        {
            if (node is FrameworkElement element && element.Tag is string symbol && symbol.Length > 0)
                return symbol;
            node = VisualTreeHelper.GetParent(node);
        }

        return null;
    }

    void OnTick()
    {
        if (!IsMouseOver)
            _hover = false;

        var now = DateTime.UtcNow;
        var elapsed = _lastTick == default ? 0 : (now - _lastTick).TotalSeconds;
        _lastTick = now;

        var dpi = PixelsPerInch();
        if (Math.Abs(dpi - _builtDpi) > 0.5 || Math.Abs(ActualHeight - _builtHeight) > 0.5)
            Rebuild(force: true);
        if (_items.Count > 1 && _viewport.Children.Count == 0 && ActualWidth > 80 && ActualHeight >= 1)
            Rebuild(force: true);

        ExpireFlashes(now);

        if (_loopWidth <= 1 || _hover || MenuOpen || !ScrollEnabled)
            return;

        _offset = TickerMotion.Advance(_offset, _loopWidth, elapsed, PixelsPerSecond, dpi, ScrollLoop, ScrollRight);
        if (_lead != null && _lead.ActualWidth > 1)
            _loopWidth = _lead.ActualWidth;
        if (_marquee != null)
            Canvas.SetLeft(_marquee, -TickerPixels.SnapDip(_offset, dpi));
    }

    void Rebuild(bool force, IReadOnlyDictionary<string, int>? directions = null)
    {
        var dpi = PixelsPerInch();
        double height = ActualHeight < 1 ? 0 : ActualHeight;
        if (height < 1 && _items.Count > 0)
            return;

        var signature = Signature(_items)
            + "|" + ActualHeight.ToString("0.0", CultureInfo.InvariantCulture)
            + "|" + _textScale.ToString("0.00", CultureInfo.InvariantCulture)
            + "|" + _symbolGap.ToString("0.00", CultureInfo.InvariantCulture)
            + "|" + string.Join(",", _pinnedSymbols);
        if (!force && signature == _signature && (_viewport.Children.Count > 0 || _pinned.Children.Count > 0))
            return;

        _signature = signature;
        _viewport.Children.Clear();
        _pinned.Children.Clear();
        _chunks.Clear();
        _marquee = null;
        _lead = null;
        var previousLoop = _loopWidth;
        _loopWidth = 0;

        if (_items.Count == 0)
        {
            if (height < 1)
                height = 16;
            var empty = MakeText("No tickers yet. Add one above.", PaperInk.For(TickerPalette.Flat), TickerPixels.FontDip(Math.Max(8, height), _textScale, dpi), new Thickness(8, 0, 0, 0));
            empty.Tag = "";
            empty.HorizontalAlignment = HorizontalAlignment.Left;
            _viewport.Children.Add(empty);
            _builtDpi = dpi;
            _builtHeight = ActualHeight;
            return;
        }

        double fontSize = TickerPixels.FontDip(height, _textScale, dpi);
        double dot = TickerPixels.SnapDip(TickerMetrics.DotSize(height), dpi);
        if (dot < 2)
            dot = 2;
        double gap = TickerPixels.SnapDip(Math.Max(4, fontSize * 0.38), dpi);
        double dotGap = TickerSpacing.GapDip(fontSize, _symbolGap, dpi);
        _badge.FontSize = Math.Max(8, fontSize * 0.42);
        _badge.Foreground = Brush(PaperInk.For(TickerPalette.Tag));

        var pinned = new List<DisplayItem>();
        var scrolling = new List<DisplayItem>();
        foreach (var item in _items)
        {
            if (IsPinned(item.Symbol))
                pinned.Add(item);
            else
                scrolling.Add(item);
        }

        foreach (var item in pinned)
            _pinned.Children.Add(BuildChunk(item, fontSize, dot, gap, dotGap, height, trailing: true, scrolling: false, directions));

        if (scrolling.Count == 0)
        {
            _builtDpi = dpi;
            _builtHeight = height;
            return;
        }

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Height = height,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            SnapsToDevicePixels = true,
            UseLayoutRounding = true,
            ClipToBounds = false
        };
        ApplyText(row);
        var first = BuildCopy(scrolling, fontSize, dot, gap, dotGap, height, directions);
        var second = BuildCopy(scrolling, fontSize, dot, gap, dotGap, height, directions);
        row.Children.Add(first);
        row.Children.Add(second);
        first.Measure(new Size(double.PositiveInfinity, height));
        var measured = first.DesiredSize.Width;
        _loopWidth = measured > 1 ? measured : previousLoop;
        _lead = first;
        _marquee = row;
        first.SizeChanged += (_, _) =>
        {
            if (first.ActualWidth > 1)
                _loopWidth = first.ActualWidth;
        };
        _builtDpi = dpi;
        _builtHeight = height;
        Canvas.SetTop(row, 0);
        Canvas.SetLeft(row, -TickerPixels.SnapDip(_offset, dpi));
        _viewport.Children.Add(row);
        if (_loopWidth > 1)
            _offset %= _loopWidth;
    }

    bool IsPinned(string symbol)
    {
        foreach (var pin in _pinnedSymbols)
        {
            if (string.Equals(pin, symbol, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    StackPanel BuildCopy(IReadOnlyList<DisplayItem> items, double fontSize, double dot, double gap, double dotGap, double height, IReadOnlyDictionary<string, int>? directions)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Height = height, SnapsToDevicePixels = true };
        ApplyText(panel);
        foreach (var item in items)
            panel.Children.Add(BuildChunk(item, fontSize, dot, gap, dotGap, height, trailing: true, scrolling: true, directions));
        return panel;
    }

    FrameworkElement BuildChunk(DisplayItem item, double fontSize, double dot, double gap, double dotGap, double height, bool trailing, bool scrolling, IReadOnlyDictionary<string, int>? directions)
    {
        var chunk = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Height = height,
            Opacity = item.Stale ? TickerPalette.StaleOpacity : 1,
            Tag = item.Symbol,
            Cursor = System.Windows.Input.Cursors.Hand,
            SnapsToDevicePixels = true,
            UseLayoutRounding = true,
            Background = Brushes.Transparent
        };
        ApplyText(chunk);
        chunk.MouseLeftButtonUp += (_, _) =>
        {
            if (chunk.Tag is string symbol && symbol.Length > 0)
                SymbolClicked?.Invoke(symbol);
        };

        var direction = 0;
        if (directions != null && directions.TryGetValue(item.Symbol, out var found))
            direction = found;
        var blocks = new TextBlock[item.Runs.Count];
        TextBlock? priceBlock = null;
        var restore = "";
        for (int i = 0; i < item.Runs.Count; i++)
        {
            var run = item.Runs[i];
            var color = run.Color;
            if (run.Role == TextRole.Price)
            {
                restore = run.Color;
                if (_liveUpdates && direction != 0)
                    color = direction > 0 ? UpFlashColor : DownFlashColor;
            }

            var margin = new Thickness(0, 0, i == item.Runs.Count - 1 ? 0 : gap, 0);
            var block = MakeText(run.Text, color, fontSize, margin);
            blocks[i] = block;
            if (run.Role == TextRole.Price)
                priceBlock = block;
            chunk.Children.Add(block);
        }

        _chunks.Add(new TapeChunk
        {
            Symbol = item.Symbol,
            Scrolling = scrolling,
            SparkKey = SparkKey(item),
            Chunk = chunk,
            Blocks = blocks,
            PriceBlock = priceBlock,
            RestoreColor = restore,
            FlashUntil = _liveUpdates && direction != 0 && priceBlock != null
                ? DateTime.UtcNow.AddMilliseconds(TapeFlash.HoldMilliseconds)
                : default
        });

        if (item.Spark is { Count: >= 2 })
            chunk.Children.Add(Spark(item.Spark, fontSize, gap));

        var wrap = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Height = height,
            SnapsToDevicePixels = true
        };
        wrap.Children.Add(chunk);
        if (trailing)
        {
            wrap.Children.Add(new Ellipse
            {
                Width = dot,
                Height = dot,
                Fill = Brush(PaperInk.For(TickerPalette.Dot)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(dotGap, 0, dotGap, 0),
                Opacity = item.Stale ? TickerPalette.StaleOpacity : 1,
                IsHitTestVisible = false,
                SnapsToDevicePixels = true
            });
        }

        return wrap;
    }

    Shape Spark(IReadOnlyList<decimal> values, double fontSize, double gap)
    {
        double width = TickerPixels.SnapDip(Math.Max(28, fontSize * 2.4), PixelsPerInch());
        double height = TickerPixels.SnapDip(Math.Max(8, fontSize * 0.62), PixelsPerInch());
        decimal min = values[0];
        decimal max = values[0];
        for (int i = 1; i < values.Count; i++)
        {
            if (values[i] < min)
                min = values[i];
            if (values[i] > max)
                max = values[i];
        }

        var span = max - min;
        var points = new PointCollection();
        for (int i = 0; i < values.Count; i++)
        {
            double x = values.Count == 1 ? 0 : width * i / (values.Count - 1);
            double y = span == 0 ? height / 2 : (double)((max - values[i]) / span) * (height - 2) + 1;
            points.Add(new Point(x, y));
        }

        return new Polyline
        {
            Points = points,
            Stroke = Brush(PaperInk.Accent),
            StrokeThickness = 1,
            Width = width,
            Height = height,
            Margin = new Thickness(gap, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
            SnapsToDevicePixels = true
        };
    }

    TextBlock MakeText(string text, string color, double fontSize, Thickness margin)
    {
        var block = new TextBlock
        {
            Text = text,
            Foreground = Brush(color),
            FontFamily = _font,
            FontSize = fontSize,
            FontWeight = FontWeights.Bold,
            LineHeight = fontSize,
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = margin,
            SnapsToDevicePixels = true,
            UseLayoutRounding = true,
            IsHitTestVisible = false
        };
        ApplyText(block);
        return block;
    }

    void ApplyText(DependencyObject target)
    {
        TextOptions.SetTextFormattingMode(target, TextFormattingMode.Display);
        TextOptions.SetTextRenderingMode(target, _translucent ? TextRenderingMode.Grayscale : TextRenderingMode.ClearType);
        TextOptions.SetTextHintingMode(target, TextHintingMode.Fixed);
        RenderOptions.SetClearTypeHint(target, _translucent ? ClearTypeHint.Auto : ClearTypeHint.Enabled);
        RenderOptions.SetBitmapScalingMode(target, BitmapScalingMode.NearestNeighbor);
    }

    double PixelsPerInch()
    {
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerInchX;
        return dpi < 1 ? 96 : dpi;
    }

    static string Signature(IReadOnlyList<DisplayItem> items)
    {
        var parts = new string[items.Count];
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var spark = item.Spark == null ? "" : item.Spark.Count + ":" + (item.Spark.Count == 0 ? "" : item.Spark[^1].ToString(CultureInfo.InvariantCulture));
            parts[i] = (item.Stale ? "1:" : "0:") + item.Symbol + ":" + spark + ":" + string.Concat(item.Runs.Select(run => run.Text + run.Color));
        }

        return string.Join("|", parts);
    }

    Dictionary<string, int> Directions(IReadOnlyList<DisplayItem> items)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            if (map.ContainsKey(item.Symbol))
                continue;
            var known = _tapePrices.ContainsKey(item.Symbol);
            _tapePrices.TryGetValue(item.Symbol, out var previous);
            map[item.Symbol] = known && _liveUpdates ? TapeFlash.Direction(previous, item.TapePrice) : 0;
            _tapePrices[item.Symbol] = item.TapePrice;
        }

        return map;
    }

    bool TryPatch(IReadOnlyDictionary<string, int> directions)
    {
        if (_chunks.Count == 0)
            return false;

        var pinned = new List<DisplayItem>();
        var scrolling = new List<DisplayItem>();
        foreach (var item in _items)
        {
            if (IsPinned(item.Symbol))
                pinned.Add(item);
            else
                scrolling.Add(item);
        }

        var pinViews = new List<TapeChunk>();
        var scrollViews = new List<TapeChunk>();
        foreach (var chunk in _chunks)
        {
            if (chunk.Scrolling)
                scrollViews.Add(chunk);
            else
                pinViews.Add(chunk);
        }

        if (pinViews.Count != pinned.Count)
            return false;
        if (scrolling.Count == 0)
        {
            if (scrollViews.Count != 0)
                return false;
        }
        else if (scrollViews.Count != scrolling.Count * 2)
        {
            return false;
        }

        for (var i = 0; i < pinned.Count; i++)
        {
            if (!Fits(pinViews[i], pinned[i]))
                return false;
        }

        for (var i = 0; i < scrollViews.Count; i++)
        {
            if (!Fits(scrollViews[i], scrolling[i % scrolling.Count]))
                return false;
        }

        for (var i = 0; i < pinned.Count; i++)
            Paint(pinViews[i], pinned[i], directions);
        for (var i = 0; i < scrollViews.Count; i++)
            Paint(scrollViews[i], scrolling[i % scrolling.Count], directions);
        return true;
    }

    static bool Fits(TapeChunk view, DisplayItem item)
        => string.Equals(view.Symbol, item.Symbol, StringComparison.OrdinalIgnoreCase)
           && view.Blocks.Length == item.Runs.Count
           && view.SparkKey == SparkKey(item);

    void Paint(TapeChunk view, DisplayItem item, IReadOnlyDictionary<string, int> directions)
    {
        directions.TryGetValue(item.Symbol, out var direction);
        view.Chunk.Opacity = item.Stale ? TickerPalette.StaleOpacity : 1;
        for (var i = 0; i < item.Runs.Count; i++)
        {
            var run = item.Runs[i];
            var color = run.Color;
            view.Blocks[i].Text = run.Text;
            if (run.Role == TextRole.Price)
            {
                view.RestoreColor = run.Color;
                view.PriceBlock = view.Blocks[i];
                if (_liveUpdates && direction != 0)
                {
                    color = direction > 0 ? UpFlashColor : DownFlashColor;
                    view.FlashUntil = DateTime.UtcNow.AddMilliseconds(TapeFlash.HoldMilliseconds);
                }
                else if (view.FlashUntil != default && DateTime.UtcNow < view.FlashUntil)
                {
                    continue;
                }
            }

            view.Blocks[i].Foreground = Brush(color);
        }
    }

    void ExpireFlashes(DateTime now)
    {
        foreach (var view in _chunks)
        {
            if (view.FlashUntil == default || now < view.FlashUntil || view.PriceBlock == null)
                continue;
            view.PriceBlock.Foreground = Brush(view.RestoreColor);
            view.FlashUntil = default;
        }
    }

    static string SparkKey(DisplayItem item)
    {
        if (item.Spark is not { Count: >= 2 } spark)
            return "";
        return spark.Count.ToString(CultureInfo.InvariantCulture) + ":" + spark[^1].ToString(CultureInfo.InvariantCulture);
    }

    sealed class TapeChunk
    {
        public required string Symbol { get; init; }
        public required bool Scrolling { get; init; }
        public required string SparkKey { get; init; }
        public required StackPanel Chunk { get; init; }
        public required TextBlock[] Blocks { get; init; }
        public TextBlock? PriceBlock { get; set; }
        public string RestoreColor { get; set; } = "";
        public DateTime FlashUntil { get; set; }
    }

    static readonly Dictionary<string, SolidColorBrush> BrushCache = new(StringComparer.OrdinalIgnoreCase);

    static SolidColorBrush Brush(string hex)
    {
        if (BrushCache.TryGetValue(hex, out var brush))
            return brush;
        var color = (Color)ColorConverter.ConvertFromString(hex);
        brush = new SolidColorBrush(color);
        brush.Freeze();
        BrushCache[hex] = brush;
        return brush;
    }
}
