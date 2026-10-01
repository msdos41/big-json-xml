using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace BigJsonXml.Desktop;

public readonly record struct SurfacePointer(SurfaceRow? Row, bool Resize);

public sealed class DocumentSurface : Control
{
    public const double ResizeEdge = 6;
    private const double ButtonSize = ViewerSession.ExpandButtonSize;

    private static readonly IBrush Mark = new SolidColorBrush(Color.Parse("#FFF3C4"));
    private static readonly IBrush SelectionFill = new SolidColorBrush(Color.FromArgb(96, 51, 153, 255));
    private static readonly IBrush Ink = Brushes.Black;
    private static readonly IPen Line = new Pen(new SolidColorBrush(Color.Parse("#E1E4E8")), 1);
    private static readonly IPen ButtonPen = new Pen(new SolidColorBrush(Color.Parse("#4B5563")), 1);
    private static readonly IBrush ButtonFill = new SolidColorBrush(Color.Parse("#F3F4F6"));
    private static readonly Typeface Face = CellText.Face;
    private static readonly Cursor ArrowCursor = new(StandardCursorType.Arrow);
    private static readonly Cursor ResizeCursor = new(StandardCursorType.SizeWestEast);
    private static readonly Cursor IbeamCursor = new(StandardCursorType.Ibeam);
    private SurfaceRow? _drag;
    private readonly CellPress _press = new();

    public DocumentSurface()
    {
        ClipToBounds = true;
        Focusable = true;
        var copy = new MenuItem { Header = "Copy" };
        copy.Click += async (_, _) => await CopySelection();
        ContextMenu = new ContextMenu { Items = { copy } };
    }

    public static bool HasExpandButton(SurfaceRow row) =>
        row.CanExpand && row.Kind is SurfaceKind.Field or SurfaceKind.Entry;

    public static bool HasCollapseButton(SurfaceRow row) =>
        row.Kind is SurfaceKind.Field or SurfaceKind.Entry
        && row.CanGrowValue
        && row.IsExpanded
        && !row.CanExpand;

    public static bool TryButton(SurfaceRow row, out Rect box)
    {
        var y = (ViewerSession.RowHeight - ButtonSize) / 2;
        if (HasExpandButton(row))
        {
            box = new Rect(ViewerSession.ExpandButtonInset, y, ButtonSize, ButtonSize);
            return true;
        }

        if (HasCollapseButton(row))
        {
            var x = Math.Max(2, row.Width - ButtonSize - 2);
            box = new Rect(x, y, ButtonSize, ButtonSize);
            return true;
        }

        box = default;
        return false;
    }

    public static bool HitsExpandButton(SurfaceRow row, double localX, double localY)
    {
        if (!TryButton(row, out var box))
            return false;
        return localX >= box.X && localX < box.Right && localY >= box.Y && localY < box.Bottom;
    }

    public static double TextTop(SurfaceRow row)
    {
        if (HasExpandButton(row) || row.Kind is not (SurfaceKind.Entry or SurfaceKind.EntryIndex))
            return 4;
        return (row.Height - (ViewerSession.RowHeight - 8)) / 2;
    }

    public static double TextOrigin(SurfaceRow row)
    {
        if (HasExpandButton(row))
            return Math.Min(ViewerSession.ExpandGutter, Math.Max(0, row.Width - ViewerSession.Advance));
        return Math.Min(4, Math.Max(0, row.Width - ViewerSession.Advance));
    }

    public static string PaintedText(SurfaceRow row)
    {
        var text = row.Value ?? row.Name;
        if (text.Length == 0)
            return "";
        return row.IsCutOff ? text + "…" : text;
    }

    public static StandardCursorType CursorFor(SurfacePointer hit) =>
        hit.Resize ? StandardCursorType.SizeWestEast : StandardCursorType.Arrow;

    public static SurfacePointer HitTest(IReadOnlyList<SurfaceRow> rows, double x, double y)
    {
        SurfaceRow? hit = null;
        SurfaceRow? resize = null;
        foreach (var row in rows)
        {
            if (y < row.Y || y >= row.Y + row.Height)
                continue;
            if (x >= row.X && x < row.X + row.Width)
                hit = row;
            if (row.Axis != WidthAxis.None && Math.Abs(row.X + row.Width - x) <= ResizeEdge)
                resize = row;
        }

        return new SurfacePointer(resize ?? hit, resize is not null);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is ViewerModel model)
            model.PropertyChanged += (_, _) => InvalidateVisual();
        InvalidateVisual();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (DataContext is ViewerModel model)
            model.SetViewportSize(e.NewSize.Width, e.NewSize.Height);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (DataContext is not ViewerModel model)
            return;
        model.ScrollY -= e.Delta.Y * ViewerSession.RowHeight * 3;
        e.Handled = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (DataContext is not ViewerModel model)
            return;
        var point = e.GetPosition(this);
        var docX = point.X + model.ScrollX;
        var docY = point.Y + model.ScrollY;
        var hit = HitTest(model.Rows, docX, docY);
        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
        {
            Focus();
            SelectForCopy(model, hit, docX);
            return;
        }

        if (hit.Resize && hit.Row is not null)
        {
            _press.Cancel();
            _drag = hit.Row;
            Cursor = ResizeCursor;
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        Focus();
        var row = hit.Row;
        var onButton = row is not null && HitsExpandButton(row, docX - row.X, docY - row.Y);
        var index = row is null ? 0 : IndexOf(row, docX);
        _press.Begin(row, onButton, point, index, row is null ? null : CellPress.CopySource(row));
        if (row is null)
            return;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (DataContext is not ViewerModel model)
            return;
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control))
            return;
        if (e.Key == Key.C)
        {
            _ = CopySelection();
            e.Handled = true;
        }
        else if (e.Key == Key.A)
        {
            model.SelectAll();
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        if (_drag is not null && DataContext is ViewerModel model)
        {
            Cursor = ResizeCursor;
            model.DragWidth(_drag, point.X + model.ScrollX - _drag.X);
            e.Handled = true;
            return;
        }

        if (_press.Active && DataContext is ViewerModel selecting)
        {
            var docX = point.X + selecting.ScrollX;
            var docY = point.Y + selecting.ScrollY;
            var hit = HitTest(selecting.Rows, docX, docY);
            var under = hit.Resize ? null : hit.Row;
            var index = under is null ? 0 : IndexOf(under, docX);
            var before = _press.Selection;
            _press.Move(point, under, index);
            if (_press.Selection != before)
                selecting.SetSelection(_press.Selection);
            Cursor = IbeamCursor;
            e.Handled = true;
            return;
        }

        ApplyCursor(point);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_drag is not null)
        {
            _drag = null;
            e.Pointer.Capture(null);
            ApplyCursor(e.GetPosition(this));
            return;
        }

        if (!_press.Active || DataContext is not ViewerModel model)
        {
            _press.Cancel();
            return;
        }

        var row = _press.Row;
        var length = row is { ScalarText: true } ? model.ScalarLength(row.Path) : 0;
        var label = row is null ? null : CellPress.CopySource(row);
        var gesture = _press.End(length, label);
        if (gesture == CellGesture.Toggle && row is not null)
        {
            model.SetSelection(null);
            model.Toggle(row);
        }
        else if (gesture == CellGesture.Select)
        {
            model.SetSelection(_press.Selection);
        }

        _press.Cancel();
        e.Pointer.Capture(null);
        ApplyCursor(e.GetPosition(this));
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_drag is null && !_press.Active)
            Cursor = ArrowCursor;
    }

    private void ApplyCursor(Point point)
    {
        if (_drag is not null)
        {
            Cursor = ResizeCursor;
            return;
        }

        if (DataContext is not ViewerModel model)
        {
            Cursor = ArrowCursor;
            return;
        }

        var docX = point.X + model.ScrollX;
        var docY = point.Y + model.ScrollY;
        var hit = HitTest(model.Rows, docX, docY);
        if (hit.Resize)
        {
            Cursor = ResizeCursor;
            return;
        }

        var row = hit.Row;
        var onButton = row is not null && HitsExpandButton(row, docX - row.X, docY - row.Y);
        Cursor = row is not null && !onButton && CellPress.HasSelectableText(row) ? IbeamCursor : ArrowCursor;
    }

    private void SelectForCopy(ViewerModel model, SurfacePointer hit, double docX)
    {
        if (hit.Resize || hit.Row is not SurfaceRow row || !CellPress.HasSelectableText(row))
            return;
        var index = IndexOf(row, docX);
        if (model.Selection is { } current && current.Contains(row, index))
            return;
        var length = row.ScalarText ? model.ScalarLength(row.Path) : 0;
        model.SetSelection(CellPress.Shown(row, length, CellPress.CopySource(row)));
    }

    private static int IndexOf(SurfaceRow row, double docX)
    {
        var painted = row.Value ?? row.Name;
        var localX = docX - row.X - TextOrigin(row);
        return row.TextStart + CellText.IndexAt(painted, localX);
    }

    private async Task CopySelection()
    {
        if (DataContext is not ViewerModel model)
            return;
        var text = model.SelectedText();
        if (string.IsNullOrEmpty(text))
            return;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
            await clipboard.SetTextAsync(text);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (DataContext is not ViewerModel model)
            return;
        var view = new Rect(Bounds.Size);
        if (view.Width <= 0 || view.Height <= 0)
            return;
        context.FillRectangle(Brushes.White, view);
        // A cell around a long list is taller than a GPU surface can hold.
        var limit = new Rect(view.X - 2, view.Y - 2, view.Width + 4, view.Height + 4);
        foreach (var row in model.Rows)
        {
            var rect = new Rect(row.X - model.ScrollX, row.Y - model.ScrollY, row.Width, row.Height);
            var visible = rect.Intersect(view);
            if (visible.Width <= 0 || visible.Height <= 0)
                continue;
            using (context.PushClip(visible))
            {
                if (row.IsMarked)
                    context.FillRectangle(Mark, visible);
                if (row.Kind != SurfaceKind.ValueLine)
                {
                    var border = ClampTo(rect, limit);
                    if (border.Width > 0 && border.Height > 0)
                        context.DrawRectangle(null, Line, border);
                }

                if (TryButton(row, out var button) && ButtonInView(rect, button, visible))
                    DrawExpandButton(context, rect, button, row.IsExpanded);
                var text = row.Value ?? row.Name;
                var textY = rect.Y + TextTop(row);
                var textX = rect.X + TextOrigin(row);
                if (model.Selection is { } selection && selection.Covers(row))
                    DrawSelection(context, selection, row, text, textX, textY);
                if (TextInView(text, textX, textY, visible))
                    Draw(context, text, textX, textY);
                if (row.IsCutOff && TextInView("…", rect.Right - 14, textY, visible))
                    Draw(context, "…", rect.Right - 14, textY);
            }
        }
    }

    private static Rect ClampTo(Rect rect, Rect limit)
    {
        var left = Math.Max(rect.Left, limit.Left);
        var top = Math.Max(rect.Top, limit.Top);
        var right = Math.Min(rect.Right, limit.Right);
        var bottom = Math.Min(rect.Bottom, limit.Bottom);
        if (right <= left || bottom <= top)
            return default;
        return new Rect(left, top, right - left, bottom - top);
    }

    private static bool ButtonInView(Rect cell, Rect button, Rect visible)
    {
        var top = cell.Y + button.Y;
        var left = cell.X + button.X;
        return top < visible.Bottom && top + button.Height > visible.Top
            && left < visible.Right && left + button.Width > visible.Left;
    }

    private static void DrawSelection(DrawingContext context, CellSelection selection, SurfaceRow row, string text, double textX, double textY)
    {
        var from = Math.Max(selection.Start, row.TextStart);
        var to = Math.Min(selection.End, row.TextStart + text.Length);
        if (to <= from)
            return;
        foreach (var glyph in CellText.Rects(text, from - row.TextStart, to - from))
            context.FillRectangle(SelectionFill, new Rect(textX + glyph.X, textY + glyph.Y, glyph.Width, glyph.Height));
    }

    private static bool TextInView(string text, double x, double y, Rect visible)
    {
        if (text.Length == 0 || y >= visible.Bottom || y + ViewerSession.RowHeight <= visible.Top)
            return false;
        var reach = Math.Max(ViewerSession.Advance, text.Length * ViewerSession.Advance);
        return x < visible.Right && x + reach > visible.Left;
    }

    private static void DrawExpandButton(DrawingContext context, Rect cell, Rect button, bool expanded)
    {
        if (cell.Width < ButtonSize || cell.Height < 8)
            return;
        var box = new Rect(cell.X + button.X, cell.Y + button.Y, button.Width, button.Height);
        context.DrawRectangle(ButtonFill, ButtonPen, box, 2, 2);
        var midY = box.Y + (button.Height / 2);
        context.DrawLine(ButtonPen, new Point(box.X + 3, midY), new Point(box.Right - 3, midY));
        if (!expanded)
        {
            var midX = box.X + (button.Width / 2);
            context.DrawLine(ButtonPen, new Point(midX, box.Y + 3), new Point(midX, box.Bottom - 3));
        }
    }

    private static void Draw(DrawingContext context, string text, double x, double y)
    {
        using var layout = new TextLayout(text, Face, CellText.FontSize, Ink);
        layout.Draw(context, new Point(x, y));
    }
}
