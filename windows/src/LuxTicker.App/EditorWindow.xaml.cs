using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;

namespace LuxTicker;

public partial class EditorWindow : Window
{
    readonly IQuoteProbe _probe;
    readonly Action<IReadOnlyList<SymbolEntry>> _apply;
    List<SymbolEntry> _items;
    Point _dragStart;
    SymbolEntry? _dragItem;
    CancellationTokenSource? _probeCts;
    WindowIcons? _icons;

    public EditorWindow(IReadOnlyList<SymbolEntry> symbols, IQuoteProbe probe, Action<IReadOnlyList<SymbolEntry>> apply)
    {
        _items = symbols.ToList();
        _probe = probe;
        _apply = apply;
        InitializeComponent();
        PathText.Text = AppPaths.SymbolsPath();
        Bind(0);
        SourceInitialized += (_, _) =>
        {
            UseDarkTitle();
            try
            {
                _icons = WindowIcons.Apply(this);
            }
            catch (Exception ex)
            {
                Log.Error("editor icon " + ex.Message);
            }
        };
        Closed += (_, _) =>
        {
            _probeCts?.Cancel();
            _icons?.Dispose();
            _icons = null;
        };
    }

    public void Reload(IReadOnlyList<SymbolEntry> symbols)
    {
        if (_items.SequenceEqual(symbols))
            return;
        var selected = SelectedIndex();
        _items = symbols.ToList();
        Bind(selected);
    }

    void Bind(int selectIndex)
    {
        SymbolList.ItemsSource = null;
        SymbolList.ItemsSource = _items;
        if (_items.Count == 0)
            return;
        if (selectIndex < 0)
            selectIndex = 0;
        if (selectIndex >= _items.Count)
            selectIndex = _items.Count - 1;
        SymbolList.SelectedIndex = selectIndex;
        SymbolList.ScrollIntoView(_items[selectIndex]);
    }

    void Commit(IReadOnlyList<SymbolEntry> next, int selectIndex)
    {
        _items = next.ToList();
        _apply(_items);
        ErrorText.Text = "";
        Bind(selectIndex);
    }

    async void Add_Click(object sender, RoutedEventArgs e)
    {
        var typed = SymbolBox.Text;
        var label = LabelBox.Text;
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

        var inferred = SymbolEntry.Infer(typed, label);
        var preflight = SymbolListEditor.Add(_items, inferred, ProbeStatus.Found);
        if (!preflight.Ok && _items.Any(s => s.Symbol.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
        {
            ErrorText.Text = preflight.Error;
            return;
        }

        _probeCts?.Cancel();
        _probeCts = new CancellationTokenSource();
        var token = _probeCts.Token;
        AddButton.IsEnabled = false;
        AddButton.Content = "Checking…";
        ErrorText.Text = "";
        try
        {
            var probe = await _probe.ProbeAsync(normalized, token);
            var entry = inferred.WithQuoteCurrency(probe.Currency);
            var result = SymbolListEditor.Add(_items, entry, probe.Status);
            if (!result.Ok)
            {
                ErrorText.Text = result.Error ?? "Couldn't add that symbol.";
                return;
            }

            Commit(result.Symbols, result.Symbols.Count - 1);
            SymbolBox.Clear();
            LabelBox.Clear();
            SymbolBox.Focus();
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
            AddButton.Content = "Add";
        }
    }

    void Up_Click(object sender, RoutedEventArgs e)
    {
        int index = SelectedIndex();
        var next = SymbolListEditor.Move(_items, index, -1);
        if (ReferenceEquals(next, _items))
            return;
        Commit(next, index - 1);
    }

    void Down_Click(object sender, RoutedEventArgs e)
    {
        int index = SelectedIndex();
        var next = SymbolListEditor.Move(_items, index, 1);
        if (ReferenceEquals(next, _items))
            return;
        Commit(next, index + 1);
    }

    void Remove_Click(object sender, RoutedEventArgs e)
    {
        int index = SelectedIndex();
        if (index < 0)
            return;
        Commit(SymbolListEditor.RemoveAt(_items, index), index);
    }

    void Reset_Click(object sender, RoutedEventArgs e)
    {
        var answer = MessageBox.Show(
            this,
            "Replace the current list with the built-in " + LuxWireEdition.ProductName + " symbols?",
            "Reset to default list",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question);
        if (answer != MessageBoxResult.OK)
            return;
        Commit(SymbolListEditor.Reset(DefaultSymbols.Load()), 0);
    }

    void SymbolBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Add_Click(sender, e);
            e.Handled = true;
        }
    }

    void List_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete)
        {
            Remove_Click(sender, e);
            e.Handled = true;
        }
    }

    void List_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(null);
        _dragItem = ItemFromPoint(e.GetPosition(SymbolList));
    }

    void List_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragItem == null)
            return;
        var pos = e.GetPosition(null);
        if (Math.Abs(pos.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(pos.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        var symbol = _dragItem.Symbol;
        _dragItem = null;
        DragDrop.DoDragDrop(SymbolList, new DataObject(DataFormats.UnicodeText, symbol), DragDropEffects.Move);
    }

    void List_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.UnicodeText) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    void List_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.UnicodeText) is not string source || string.IsNullOrWhiteSpace(source))
            return;
        int from = _items.FindIndex(s => s.Symbol.Equals(source, StringComparison.OrdinalIgnoreCase));
        int to = IndexFromPoint(e.GetPosition(SymbolList));
        if (from < 0)
            return;
        if (to < 0)
            to = _items.Count - 1;
        Commit(SymbolListEditor.MoveTo(_items, from, to), to);
    }

    int SelectedIndex() => SymbolList.SelectedIndex;

    SymbolEntry? ItemFromPoint(Point point)
    {
        int index = IndexFromPoint(point);
        return index >= 0 && index < _items.Count ? _items[index] : null;
    }

    int IndexFromPoint(Point point)
    {
        var hit = SymbolList.InputHitTest(point) as DependencyObject;
        while (hit != null && hit is not ListBoxItem)
            hit = System.Windows.Media.VisualTreeHelper.GetParent(hit);
        if (hit is not ListBoxItem item)
            return -1;
        return SymbolList.ItemContainerGenerator.IndexFromContainer(item);
    }

    void UseDarkTitle()
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int on = 1;
            NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, sizeof(int));
        }
        catch
        {
            // Dark title bar is cosmetic.
        }
    }
}
