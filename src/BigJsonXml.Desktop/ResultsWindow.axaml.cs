using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace BigJsonXml.Desktop;

public partial class ResultsWindow : Window
{
    private ViewerModel? _watched;
    private bool _syncingSelection;
    private bool _dragging;
    private int _anchor = -1;
    private double _dragStart;
    private double _widthStart;

    public ResultsWindow()
    {
        InitializeComponent();
        ResultsList.TemplateApplied += (_, _) => HookScroll();
        ResultsList.LayoutUpdated += (_, _) => PrefetchFromViewport();
    }

    private ScrollViewer? _scroll;

    private void HookScroll()
    {
        _scroll = ResultsList.FindDescendantOfType<ScrollViewer>();
        if (_scroll is null)
            return;
        _scroll.ScrollChanged -= ScrollChanged;
        _scroll.ScrollChanged += ScrollChanged;
    }

    private void ScrollChanged(object? sender, ScrollChangedEventArgs e) => PrefetchFromViewport();

    private void PrefetchFromViewport()
    {
        if (DataContext is not ViewerModel model || model.HitCount == 0)
            return;
        // Fixed row height in the template is 22px; group row is index 0.
        const double row = 22;
        var offset = _scroll?.Offset.Y ?? 0;
        var viewport = _scroll?.Viewport.Height ?? ResultsList.Bounds.Height;
        if (viewport <= 1)
            viewport = ResultsList.Bounds.Height;
        var first = Math.Max(0, (int)(offset / row) - 1);
        var last = Math.Max(first, (int)((offset + viewport) / row) + 1);
        // Convert list indices to hit indices (skip group row).
        var firstHit = Math.Max(0, first - 1);
        var lastHit = Math.Max(firstHit, last - 1);
        // Only queue work here — ReadHitText runs on the timer tick, not during scroll layout.
        model.PrefetchResultRange(firstHit, lastHit);
    }

    private void ResultSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncingSelection)
            return;
        if (DataContext is not ViewerModel model || model.IsAdjustingResults)
            return;
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is ResultDocumentLine)
        {
            MoveOffGroup(model);
            return;
        }

        if (e.AddedItems.Count > 0 && e.AddedItems[0] is ResultHitLine line)
        {
            _anchor = ResultsList.SelectedIndex;
            model.ChooseResult(line.Result);
            return;
        }

        if (e.RemovedItems.Count > 0 && ResultsList.SelectedItem is null)
            model.ChooseResult(null);
    }

    private void GroupPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not ResultDocumentLine line)
            return;
        if (DataContext is not ViewerModel model)
            return;
        model.ToggleResultDocument(line.Document);
        SelectCurrent(model);
        e.Handled = true;
    }

    private void SplitterPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control thumb || DataContext is not ViewerModel model)
            return;
        _dragging = true;
        _dragStart = e.GetPosition(HeaderGrid).X;
        _widthStart = model.FoundColumnWidth;
        e.Pointer.Capture(thumb);
        e.Handled = true;
    }

    private void SplitterMoved(object? sender, PointerEventArgs e)
    {
        if (!_dragging || DataContext is not ViewerModel model)
            return;
        var dx = e.GetPosition(HeaderGrid).X - _dragStart;
        var max = Math.Max(ViewerModel.FoundColumnMin, HeaderGrid.Bounds.Width - 22 - 80);
        model.SetFoundColumnWidth(Math.Clamp(_widthStart + dx, ViewerModel.FoundColumnMin, max));
    }

    private void SplitterReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_dragging)
            return;
        FinishDrag(e.Pointer, sender as Control);
        e.Handled = true;
    }

    private void SplitterCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (!_dragging)
            return;
        FinishDrag(null, sender as Control);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_watched is not null)
            _watched.PropertyChanged -= ModelChanged;
        _watched = DataContext as ViewerModel;
        if (_watched is null)
            return;
        _watched.PropertyChanged += ModelChanged;
        SelectCurrent(_watched);
    }

    protected override void OnClosed(EventArgs e)
    {
        DataContext = null;
        if (_watched is not null)
            _watched.PropertyChanged -= ModelChanged;
        base.OnClosed(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.F && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            MainWindow.RequestFind(this);
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Escape)
            return;
        if (DataContext is ViewerModel model && (model.SearchRunning || model.ListingResults))
        {
            model.CancelFind();
            e.Handled = true;
            return;
        }

        if (ResultsList.SelectedItem is not null)
            ResultsList.SelectedItem = null;
        else if (DataContext is ViewerModel clear)
            clear.ClearMark();
        e.Handled = true;
    }

    private void ModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewerModel.CurrentResult) && sender is ViewerModel model)
            SelectCurrent(model);
    }

    private void FinishDrag(IPointer? pointer, Control? thumb)
    {
        _dragging = false;
        if (pointer is not null && thumb is not null)
            pointer.Capture(null);
        if (DataContext is ViewerModel model)
            model.ApplyFoundColumnWidth();
    }

    private void MoveOffGroup(ViewerModel model)
    {
        var index = ResultsList.SelectedIndex;
        var step = _anchor < 0 || index >= _anchor ? 1 : -1;
        var next = index + step;
        while (next >= 0 && next < ResultsList.ItemCount && ResultsList.Items[next] is not ResultHitLine)
            next += step;
        _syncingSelection = true;
        try
        {
            ResultsList.SelectedIndex = next >= 0 && next < ResultsList.ItemCount ? next : -1;
        }
        finally
        {
            _syncingSelection = false;
        }

        if (ResultsList.SelectedItem is ResultHitLine hit)
        {
            _anchor = ResultsList.SelectedIndex;
            model.ChooseResult(hit.Result);
        }
    }

    private void SelectCurrent(ViewerModel model)
    {
        if (_syncingSelection)
            return;
        var hitIndex = model.CurrentHitIndex;
        var listIndex = hitIndex >= 0 && model.VisibleResults.Count > 1 ? hitIndex + 1 : -1;
        if (listIndex >= ResultsList.ItemCount)
            listIndex = -1;

        _syncingSelection = true;
        try
        {
            if (ResultsList.SelectedIndex != listIndex)
                ResultsList.SelectedIndex = listIndex;
            if (listIndex >= 0)
                ResultsList.ScrollIntoView(listIndex);
        }
        finally
        {
            _syncingSelection = false;
        }
    }
}
