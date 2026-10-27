using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace LuxTicker;

public sealed class BtcChartPlot : FrameworkElement
{
    const double PlotLeft = 72;
    const double PlotRight = 12;
    const double PlotTop = 12;
    const double PlotBottom = 28;

    IReadOnlyList<BtcPoint> _points = Array.Empty<BtcPoint>();
    BtcChartStyle _style = BtcChartStyle.Line;
    bool _crosshair = true;
    int _scrub = -1;
    bool _dragging;

    public BtcChartPlot()
    {
        SnapsToDevicePixels = true;
        Cursor = Cursors.Cross;
        // A transparent fill is hit-testable, so a click on the paper still lands on the series.
        Background = Brushes.Transparent;
    }

    public Brush Background
    {
        get => (Brush)GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public static readonly DependencyProperty BackgroundProperty = DependencyProperty.Register(
        nameof(Background),
        typeof(Brush),
        typeof(BtcChartPlot),
        new FrameworkPropertyMetadata(Brushes.Transparent, FrameworkPropertyMetadataOptions.AffectsRender));

    public void SetPoints(IReadOnlyList<BtcPoint>? points)
    {
        _points = points ?? Array.Empty<BtcPoint>();
        _scrub = -1;
        _dragging = false;
        InvalidateVisual();
    }

    public void SetCrosshair(bool enabled)
    {
        _crosshair = enabled;
        if (!enabled)
        {
            _scrub = -1;
            _dragging = false;
            if (IsMouseCaptured)
                ReleaseMouseCapture();
        }

        InvalidateVisual();
    }

    public void SetStyle(BtcChartStyle style)
    {
        if (_style == style)
            return;
        _style = style;
        InvalidateVisual();
    }

    protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters)
        => new PointHitTestResult(this, hitTestParameters.HitPoint);

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (!_crosshair || _points.Count == 0)
            return;
        CaptureMouse();
        _dragging = true;
        MoveScrub(e.GetPosition(this).X);
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_dragging)
            return;
        MoveScrub(e.GetPosition(this).X);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (IsMouseCaptured)
            ReleaseMouseCapture();
        _dragging = false;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        _dragging = false;
    }

    void MoveScrub(double x)
    {
        var plotWidth = Math.Max(1, ActualWidth - PlotLeft - PlotRight);
        var next = BtcChart.NearestIndex(_points, (x - PlotLeft) / plotWidth);
        if (next == _scrub)
            return;
        _scrub = next;
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 640 : availableSize.Width;
        var height = double.IsInfinity(availableSize.Height) ? 280 : availableSize.Height;
        return new Size(Math.Max(0, width), Math.Max(0, height));
    }

    protected override void OnRender(DrawingContext dc)
    {
        var width = ActualWidth;
        var height = ActualHeight;
        if (width < 8 || height < 8)
            return;

        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, width, height));
        if (_points.Count == 0)
            return;

        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var plotWidth = Math.Max(1, width - PlotLeft - PlotRight);
        var plotHeight = Math.Max(1, height - PlotTop - PlotBottom);

        decimal min;
        decimal max;
        if (_style == BtcChartStyle.Candles)
        {
            var first = BtcChart.Ohlc(_points[0]);
            min = first.Low;
            max = first.High;
            foreach (var point in _points)
            {
                var bar = BtcChart.Ohlc(point);
                if (bar.Low < min)
                    min = bar.Low;
                if (bar.High > max)
                    max = bar.High;
            }
        }
        else
        {
            min = _points[0].Close;
            max = min;
            foreach (var point in _points)
            {
                if (point.Close < min)
                    min = point.Close;
                if (point.Close > max)
                    max = point.Close;
            }
        }

        var pad = (max - min) * 0.08m;
        if (pad <= 0)
            pad = max == 0 ? 1 : max * 0.02m;
        min -= pad;
        max += pad;
        if (min < 0)
            min = 0;
        if (max <= min)
            max = min + 1;

        var start = _points[0].Time;
        var end = _points[^1].Time;
        var span = (end - start).TotalSeconds;
        if (span <= 0)
            span = 1;

        Point At(BtcPoint point)
        {
            var x = PlotLeft + (point.Time - start).TotalSeconds / span * plotWidth;
            var y = PlotTop + (double)((max - point.Close) / (max - min)) * plotHeight;
            return new Point(x, y);
        }

        var grid = Brush(PaperInk.Rule);
        var ink = Brush(PaperInk.Sub);
        var orange = Brush(BtcChart.SeriesColor);
        var fill = Brush(BtcChart.SeriesColor, 48);
        for (int i = 0; i <= 3; i++)
        {
            var y = PlotTop + plotHeight * i / 3.0;
            dc.DrawLine(new Pen(grid, 1), new Point(PlotLeft, y), new Point(PlotLeft + plotWidth, y));
            var price = max - (max - min) * i / 3m;
            DrawText(dc, PriceFormat.Format(price, "USD"), 4, y - 8, 64, ink, 11, dpi, rightAlign: true);
        }

        if (_style == BtcChartStyle.Candles)
            DrawCandles(dc, min, max, plotHeight, start, span, plotWidth);
        else
            DrawLine(dc, fill, orange, min, max, plotHeight, start, span, plotWidth);

        var ticks = 4;
        for (int i = 0; i <= ticks; i++)
        {
            var when = start + TimeSpan.FromSeconds(span * i / ticks);
            var x = PlotLeft + plotWidth * i / ticks;
            var label = AxisLabel(when, end - start);
            DrawText(dc, label, x - 36, PlotTop + plotHeight + 6, 72, ink, 11, dpi, rightAlign: false);
        }

        if (_scrub >= 0 && _scrub < _points.Count)
            DrawScrub(dc, At(_points[_scrub]), _points[_scrub], plotWidth, plotHeight, dpi);
    }

    void DrawScrub(DrawingContext dc, Point at, BtcPoint point, double plotWidth, double plotHeight, double dpi)
    {
        var orange = Brush(BtcChart.SeriesColor);
        var vertical = new Pen(orange, 1);
        vertical.Freeze();
        var horizontal = new Pen(Brush(PaperInk.Ink), 1);
        horizontal.Freeze();
        dc.DrawLine(vertical, new Point(at.X, PlotTop), new Point(at.X, PlotTop + plotHeight));
        dc.DrawLine(horizontal, new Point(PlotLeft, at.Y), new Point(PlotLeft + plotWidth, at.Y));
        dc.DrawEllipse(Brush(PaperInk.Background), null, at, 6.5, 6.5);
        dc.DrawEllipse(orange, null, at, 3.5, 3.5);

        var lines = new List<FormattedText>();
        if (_style == BtcChartStyle.Candles)
        {
            foreach (var row in BtcChart.ScrubOhlc(point).Split('\n'))
                lines.Add(ChipText(row, 13, Brush(PaperInk.Body), dpi));
        }
        else
        {
            lines.Add(ChipText(BtcChart.ScrubPrice(point.Close), 13, Brush(PaperInk.Body), dpi));
        }

        lines.Add(ChipText(BtcChart.ScrubStamp(point), 11, Brush(PaperInk.Sub), dpi));
        const double padX = 8;
        const double padY = 5;
        var chipW = lines.Max(line => line.Width) + padX * 2;
        var chipH = lines.Sum(line => line.Height) + padY * 2 + (lines.Count - 1);
        var chipX = at.X + 12;
        var chipY = at.Y - chipH - 10;
        if (chipX + chipW > PlotLeft + plotWidth)
            chipX = at.X - chipW - 12;
        if (chipX < PlotLeft)
            chipX = PlotLeft;
        if (chipY < PlotTop)
            chipY = at.Y + 12;
        if (chipY + chipH > PlotTop + plotHeight)
            chipY = Math.Max(PlotTop, PlotTop + plotHeight - chipH);

        var border = new Pen(orange, 1);
        border.Freeze();
        dc.DrawRoundedRectangle(Brush(PaperInk.Panel), border, new Rect(chipX, chipY, chipW, chipH), 3, 3);
        var textY = chipY + padY;
        foreach (var line in lines)
        {
            dc.DrawText(line, new Point(chipX + padX, textY));
            textY += line.Height + 1;
        }
    }

    void DrawLine(DrawingContext dc, Brush fill, Brush orange, decimal min, decimal max, double plotHeight, DateTimeOffset start, double span, double plotWidth)
    {
        Point At(BtcPoint point)
        {
            var x = PlotLeft + (point.Time - start).TotalSeconds / span * plotWidth;
            var y = PlotTop + (double)((max - point.Close) / (max - min)) * plotHeight;
            return new Point(x, y);
        }

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            var first = At(_points[0]);
            ctx.BeginFigure(new Point(first.X, PlotTop + plotHeight), true, true);
            ctx.LineTo(first, true, true);
            foreach (var point in _points.Skip(1))
                ctx.LineTo(At(point), true, true);
            var last = At(_points[^1]);
            ctx.LineTo(new Point(last.X, PlotTop + plotHeight), true, true);
        }

        geometry.Freeze();
        dc.DrawGeometry(fill, null, geometry);

        var line = new Pen(orange, 2.5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        line.Freeze();
        var stroke = new StreamGeometry();
        using (var ctx = stroke.Open())
        {
            ctx.BeginFigure(At(_points[0]), false, false);
            foreach (var point in _points.Skip(1))
                ctx.LineTo(At(point), true, true);
        }

        stroke.Freeze();
        dc.DrawGeometry(null, line, stroke);
        dc.DrawEllipse(orange, null, At(_points[^1]), 3.5, 3.5);
    }

    void DrawCandles(DrawingContext dc, decimal min, decimal max, double plotHeight, DateTimeOffset start, double span, double plotWidth)
    {
        var up = Brush(BtcChart.CandleUp);
        var down = Brush(BtcChart.CandleDown);
        var upPen = new Pen(up, 1);
        var downPen = new Pen(down, 1);
        upPen.Freeze();
        downPen.Freeze();
        var slot = plotWidth / Math.Max(1, _points.Count);
        var bodyW = Math.Max(1, Math.Min(14, slot * 0.72));

        double Y(decimal price) => PlotTop + (double)((max - price) / (max - min)) * plotHeight;

        foreach (var point in _points)
        {
            var bar = BtcChart.Ohlc(point);
            var x = PlotLeft + (point.Time - start).TotalSeconds / span * plotWidth;
            var rising = bar.Close >= bar.Open;
            var pen = rising ? upPen : downPen;
            var body = rising ? up : down;
            dc.DrawLine(pen, new Point(x, Y(bar.High)), new Point(x, Y(bar.Low)));
            var top = Math.Min(Y(bar.Open), Y(bar.Close));
            var bodyH = Math.Max(1, Math.Abs(Y(bar.Close) - Y(bar.Open)));
            dc.DrawRectangle(body, null, new Rect(x - bodyW / 2, top, bodyW, bodyH));
        }
    }

    static FormattedText ChipText(string text, double size, Brush brush, double dpi)
        => new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, brush, dpi);

    static string AxisLabel(DateTimeOffset when, TimeSpan span)
    {
        var eastern = TimeZoneInfo.ConvertTime(when, EasternTime.Zone);
        var pattern = span.TotalDays <= 2 ? "HH:mm" : span.TotalDays <= 120 ? "d MMM" : "MMM yyyy";
        return eastern.ToString(pattern, CultureInfo.InvariantCulture);
    }

    static void DrawText(DrawingContext dc, string text, double x, double y, double width, Brush brush, double size, double dpi, bool rightAlign)
    {
        var formatted = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, brush, dpi);
        var originX = rightAlign ? x + width - formatted.Width : x;
        dc.DrawText(formatted, new Point(originX, y));
    }

    static SolidColorBrush Brush(string hex, byte alpha = 255)
    {
        var color = (Color)ColorConverter.ConvertFromString(hex);
        color.A = alpha;
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
