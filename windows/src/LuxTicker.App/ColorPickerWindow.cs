using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LuxTicker;

/// <summary>Any background color, as hex or as red, green, and blue.</summary>
public static class ColorPicker
{
    public static bool TryPick(Window owner, string current, out string hex)
    {
        var window = new ColorPickerWindow(TickerTextColor.Parse(current, PaperInk.Background))
        {
            Owner = owner
        };
        var ok = window.ShowDialog() == true;
        hex = window.SelectedHex;
        return ok && TickerTextColor.Parse(hex, "") == hex;
    }
}

sealed class ColorPickerWindow : Window
{
    readonly TextBox _hex;
    readonly Slider _red;
    readonly Slider _green;
    readonly Slider _blue;
    readonly TextBox _redBox;
    readonly TextBox _greenBox;
    readonly TextBox _blueBox;
    readonly Border _swatch;
    bool _syncing;

    public ColorPickerWindow(string current)
    {
        Title = "Background color";
        Width = 420;
        Height = 360;
        MinWidth = 360;
        MinHeight = 320;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = PaperBrushes.Freeze(PaperInk.Background);
        Foreground = PaperBrushes.Freeze(PaperInk.Ink);
        FontFamily = new FontFamily("Segoe UI");
        FontSize = 13;
        ShowInTaskbar = false;

        SelectedHex = TickerTextColor.Parse(current, PaperInk.Background);
        var (r, g, b) = PaperInk.Rgb(SelectedHex);
        _swatch = new Border
        {
            Width = 72,
            Height = 72,
            BorderBrush = PaperBrushes.Freeze(PaperInk.Rule),
            BorderThickness = new Thickness(1),
            Background = PaperBrushes.Freeze(SelectedHex),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        _hex = Box(SelectedHex);
        _red = Slider(r);
        _green = Slider(g);
        _blue = Slider(b);
        _redBox = Box(r.ToString(CultureInfo.InvariantCulture));
        _greenBox = Box(g.ToString(CultureInfo.InvariantCulture));
        _blueBox = Box(b.ToString(CultureInfo.InvariantCulture));
        _hex.TextChanged += (_, _) => FromHex();
        _red.ValueChanged += (_, _) => FromSliders();
        _green.ValueChanged += (_, _) => FromSliders();
        _blue.ValueChanged += (_, _) => FromSliders();
        _redBox.TextChanged += (_, _) => FromChannel(_red, _redBox);
        _greenBox.TextChanged += (_, _) => FromChannel(_green, _greenBox);
        _blueBox.TextChanged += (_, _) => FromChannel(_blue, _blueBox);

        var ok = new Button
        {
            Content = "Use this color",
            Background = PaperBrushes.Freeze(PaperInk.Accent),
            Foreground = Brushes.White,
            BorderBrush = PaperBrushes.Freeze(PaperInk.Accent),
            Padding = new Thickness(12, 6, 12, 6),
            FontWeight = FontWeights.SemiBold,
            IsDefault = true,
            Margin = new Thickness(0, 0, 8, 0)
        };
        ok.Click += (_, _) =>
        {
            if (TickerTextColor.Parse(_hex.Text, "") != TickerTextColor.Parse(_hex.Text, "no"))
                return;
            SelectedHex = TickerTextColor.Parse(_hex.Text, SelectedHex);
            DialogResult = true;
        };
        var cancel = new Button
        {
            Content = "Cancel",
            Background = PaperBrushes.Freeze(PaperInk.Body),
            Foreground = Brushes.White,
            BorderBrush = PaperBrushes.Freeze(PaperInk.Body),
            Padding = new Thickness(12, 6, 12, 6),
            IsCancel = true
        };

        var root = new StackPanel { Margin = new Thickness(18) };
        root.Children.Add(new TextBlock
        {
            Text = "Custom color",
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Foreground = PaperBrushes.Freeze(PaperInk.Ink),
            Margin = new Thickness(0, 0, 0, 8)
        });
        root.Children.Add(new TextBlock
        {
            Text = "Any hex, or red, green, and blue from 0 to 255.",
            Foreground = PaperBrushes.Freeze(PaperInk.Muted),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        });
        var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        head.Children.Add(_swatch);
        var hexCol = new StackPanel { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        hexCol.Children.Add(Label("Hex"));
        _hex.Width = 120;
        hexCol.Children.Add(_hex);
        head.Children.Add(hexCol);
        root.Children.Add(head);
        root.Children.Add(Channel("Red", _red, _redBox));
        root.Children.Add(Channel("Green", _green, _greenBox));
        root.Children.Add(Channel("Blue", _blue, _blueBox));
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 16, 0, 0) };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        root.Children.Add(buttons);
        Content = root;
    }

    public string SelectedHex { get; private set; }

    static TextBlock Label(string text) => new()
    {
        Text = text,
        Foreground = PaperBrushes.Freeze(PaperInk.Ink),
        Margin = new Thickness(0, 0, 0, 4)
    };

    static TextBox Box(string text) => new()
    {
        Text = text,
        Background = PaperBrushes.Freeze(PaperInk.Panel),
        Foreground = PaperBrushes.Freeze(PaperInk.Body),
        BorderBrush = PaperBrushes.Freeze(PaperInk.Rule),
        CaretBrush = PaperBrushes.Freeze(PaperInk.Body),
        Padding = new Thickness(8, 4, 8, 4),
        Width = 64
    };

    static Slider Slider(byte value) => new()
    {
        Minimum = 0,
        Maximum = 255,
        Value = value,
        TickFrequency = 1,
        IsSnapToTickEnabled = true,
        Margin = new Thickness(0, 0, 8, 0)
    };

    static UIElement Channel(string name, Slider slider, TextBox box)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
        panel.Children.Add(Label(name));
        var row = new DockPanel();
        DockPanel.SetDock(box, Dock.Right);
        row.Children.Add(box);
        row.Children.Add(slider);
        panel.Children.Add(row);
        return panel;
    }

    void FromHex()
    {
        if (_syncing)
            return;
        var parsed = TickerTextColor.Parse(_hex.Text, "");
        if (parsed.Length != 7)
            return;
        var (r, g, b) = PaperInk.Rgb(parsed);
        _syncing = true;
        _red.Value = r;
        _green.Value = g;
        _blue.Value = b;
        _redBox.Text = r.ToString(CultureInfo.InvariantCulture);
        _greenBox.Text = g.ToString(CultureInfo.InvariantCulture);
        _blueBox.Text = b.ToString(CultureInfo.InvariantCulture);
        Paint(parsed);
        _syncing = false;
    }

    void FromSliders()
    {
        if (_syncing)
            return;
        var r = (byte)Math.Round(_red.Value);
        var g = (byte)Math.Round(_green.Value);
        var b = (byte)Math.Round(_blue.Value);
        var hex = "#" + r.ToString("X2", CultureInfo.InvariantCulture) + g.ToString("X2", CultureInfo.InvariantCulture) + b.ToString("X2", CultureInfo.InvariantCulture);
        _syncing = true;
        _hex.Text = hex;
        _redBox.Text = r.ToString(CultureInfo.InvariantCulture);
        _greenBox.Text = g.ToString(CultureInfo.InvariantCulture);
        _blueBox.Text = b.ToString(CultureInfo.InvariantCulture);
        Paint(hex);
        _syncing = false;
    }

    void FromChannel(Slider slider, TextBox box)
    {
        if (_syncing)
            return;
        if (!int.TryParse(box.Text?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var value))
            return;
        slider.Value = Math.Clamp(value, 0, 255);
    }

    void Paint(string hex)
    {
        SelectedHex = hex;
        _swatch.Background = PaperBrushes.Freeze(hex);
    }
}
