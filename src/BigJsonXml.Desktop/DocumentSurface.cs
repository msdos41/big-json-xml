using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace BigJsonXml.Desktop;

public sealed class DocumentSurface : Control
{
    private static readonly IBrush Mark = new SolidColorBrush(Color.Parse("#FFF3C4"));
    private static readonly IBrush Ink = Brushes.Black;
    private static readonly Typeface Face = new("Segoe UI");

    public DocumentSurface()
    {
        ClipToBounds = true;
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
        var y = point.Y + model.ScrollY;
        var row = model.Rows.FirstOrDefault(item => y >= item.Y && y < item.Y + item.Height);
        if (row is not null && (row.CanExpand || row.CanGrowValue || row.Kind == SurfaceKind.ValueLine))
            model.Toggle(row);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (DataContext is not ViewerModel model)
            return;
        context.FillRectangle(Brushes.White, new Rect(Bounds.Size));
        foreach (var row in model.Rows)
        {
            var top = row.Y - model.ScrollY;
            var left = row.X - model.ScrollX;
            if (row.IsMarked)
                context.FillRectangle(Mark, new Rect(0, top, Bounds.Width, row.Height));
            var mark = row.CanExpand || row.CanGrowValue;
            var label = mark ? ((row.IsExpanded || row.Kind == SurfaceKind.ValueLine) ? "▾ " : "▸ ") + row.Name : row.Name;
            Draw(context, label, left + 4, top + 4);
            if (!string.IsNullOrEmpty(row.Value))
                Draw(context, row.Value, left + ViewerSession.NameWidth, top + 4);
        }
    }

    private static void Draw(DrawingContext context, string text, double x, double y)
    {
        var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Face, 13, Ink);
        context.DrawText(formatted, new Point(x, y));
    }
}
