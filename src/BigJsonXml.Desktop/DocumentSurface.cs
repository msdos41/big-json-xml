using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace BigJsonXml.Desktop;

public readonly record struct SurfacePointer(SurfaceRow? Row, bool Resize);

public sealed class DocumentSurface : Control
{
    public const double ResizeEdge = 6;

    private static readonly IBrush Mark = new SolidColorBrush(Color.Parse("#FFF3C4"));
    private static readonly IBrush Ink = Brushes.Black;
    private static readonly IPen Line = new Pen(new SolidColorBrush(Color.Parse("#E1E4E8")), 1);
    private static readonly IPen ButtonPen = new Pen(new SolidColorBrush(Color.Parse("#4B5563")), 1);
    private static readonly IBrush ButtonFill = new SolidColorBrush(Color.Parse("#F3F4F6"));
    private static readonly Typeface Face = new("Segoe UI");
    private SurfaceRow? _drag;

    public DocumentSurface()
    {
        ClipToBounds = true;
    }

    public static bool HasExpandButton(SurfaceRow row) =>
        row.CanExpand && row.Kind is SurfaceKind.Field or SurfaceKind.Entry;

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

    public static StandardCursorType CursorFor(SurfacePointer hit) => StandardCursorType.Arrow;

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
        var hit = HitTest(model.Rows, point.X + model.ScrollX, point.Y + model.ScrollY);
        if (hit.Resize && hit.Row is not null)
        {
            _drag = hit.Row;
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        if (hit.Row is { } row && (row.CanExpand || row.CanGrowValue || row.Kind == SurfaceKind.ValueLine))
            model.Toggle(row);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_drag is null || DataContext is not ViewerModel model)
            return;
        var point = e.GetPosition(this);
        model.DragWidth(_drag, point.X + model.ScrollX - _drag.X);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_drag is null)
            return;
        _drag = null;
        e.Pointer.Capture(null);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (DataContext is not ViewerModel model)
            return;
        context.FillRectangle(Brushes.White, new Rect(Bounds.Size));
        foreach (var row in model.Rows)
        {
            var rect = new Rect(row.X - model.ScrollX, row.Y - model.ScrollY, row.Width, row.Height);
            using (context.PushClip(rect))
            {
                if (row.IsMarked)
                    context.FillRectangle(Mark, rect);
                if (row.Kind != SurfaceKind.ValueLine)
                    context.DrawRectangle(null, Line, rect);
                if (HasExpandButton(row))
                    DrawExpandButton(context, rect, row.IsExpanded);
                var text = row.Value ?? row.Name;
                var textY = rect.Y + TextTop(row);
                if (text.Length > 0)
                    Draw(context, text, rect.X + TextOrigin(row), textY);
                if (row.IsCutOff)
                    Draw(context, "…", rect.Right - 14, textY);
            }
        }
    }

    private static void DrawExpandButton(DrawingContext context, Rect cell, bool expanded)
    {
        const double size = 12;
        if (cell.Width < size || cell.Height < 8)
            return;
        var box = new Rect(cell.X + 2, cell.Y + ((ViewerSession.RowHeight - size) / 2), size, size);
        context.DrawRectangle(ButtonFill, ButtonPen, box, 2, 2);
        var midY = box.Y + (size / 2);
        context.DrawLine(ButtonPen, new Point(box.X + 3, midY), new Point(box.Right - 3, midY));
        if (!expanded)
        {
            var midX = box.X + (size / 2);
            context.DrawLine(ButtonPen, new Point(midX, box.Y + 3), new Point(midX, box.Bottom - 3));
        }
    }

    private static void Draw(DrawingContext context, string text, double x, double y)
    {
        var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Face, 13, Ink);
        context.DrawText(formatted, new Point(x, y));
    }
}
