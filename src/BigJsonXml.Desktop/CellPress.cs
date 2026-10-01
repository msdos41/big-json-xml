using Avalonia;

namespace BigJsonXml.Desktop;

public enum CellGesture
{
    None,
    Toggle,
    Select,
}

/// <summary>
/// A span of one cell's text. Offsets are into the scalar value, or into <see cref="Label"/> when the cell is not a scalar.
/// </summary>
public sealed record CellSelection(
    DocumentPath Path,
    int Start,
    int End,
    bool Scalar,
    string? Label,
    SurfaceKind Kind)
{
    public int Length => Math.Max(0, End - Start);

    public bool Covers(SurfaceRow row) =>
        Path.Equals(row.Path)
        && Scalar == row.ScalarText
        && (Scalar || Kind == row.Kind)
        && (Kind != SurfaceKind.ColumnHeader || Label == row.Name);

    public bool Contains(SurfaceRow row, int index) =>
        Covers(row) && Length > 0 && index >= Start && index < End;
}

/// <summary>
/// Decides whether a press toggles the cell or selects its text.
/// A drag selects. A click expands a collapsed scalar or an embedded table. A click on text that is already fully shown selects that text.
/// </summary>
public sealed class CellPress
{
    public const double DragSlop = 4;

    private Point _origin;
    private bool _button;
    private int _anchor;
    private string? _label;
    private bool _drag;

    public SurfaceRow? Row { get; private set; }

    public CellSelection? Selection { get; private set; }

    public bool Active { get; private set; }

    public void Begin(SurfaceRow? row, bool button, Point origin, int index, string? label)
    {
        Row = row;
        _button = button;
        _origin = origin;
        _anchor = index;
        _label = label;
        _drag = false;
        Active = row is not null;
        Selection = null;
    }

    public void Move(Point now, SurfaceRow? under, int index)
    {
        if (!Active || Row is null || _button)
            return;
        if (!_drag && Math.Abs(now.X - _origin.X) < DragSlop && Math.Abs(now.Y - _origin.Y) < DragSlop)
            return;
        _drag = true;
        if (under is null || !SameText(Row, under))
            return;
        Selection = Between(Row, _anchor, index, _label);
    }

    public CellGesture End(int scalarLength, string? label)
    {
        if (!Active || Row is null)
            return CellGesture.None;
        if (_button)
            return _drag ? CellGesture.None : CellGesture.Toggle;
        if (_drag)
            return Selection is { Length: > 0 } ? CellGesture.Select : CellGesture.None;
        if (Row.CanExpand)
            return CellGesture.Toggle;
        if (Row.CanGrowValue && !Row.IsExpanded && Row.Kind != SurfaceKind.ValueLine)
            return CellGesture.Toggle;
        if (!HasSelectableText(Row) && !Row.ScalarText)
            return CellGesture.None;
        Selection = Shown(Row, scalarLength, label ?? _label);
        return CellGesture.Select;
    }

    public void Cancel()
    {
        Active = false;
        Row = null;
        Selection = null;
        _drag = false;
    }

    public static bool HasSelectableText(SurfaceRow row)
    {
        var text = row.Value ?? row.Name;
        return text.Length > 0;
    }

    public static string CopySource(SurfaceRow row)
    {
        if (row.Kind is SurfaceKind.FieldName or SurfaceKind.ColumnHeader or SurfaceKind.EntryIndex)
            return row.Name;
        return row.Value ?? row.Name;
    }

    public static bool SameText(SurfaceRow left, SurfaceRow right) =>
        left.Path.Equals(right.Path)
        && left.ScalarText == right.ScalarText
        && (left.ScalarText || left.Kind == right.Kind)
        && (left.Kind != SurfaceKind.ColumnHeader || left.Name == right.Name);

    public static CellSelection Shown(SurfaceRow row, int scalarLength, string? label)
    {
        if (row.ScalarText && (row.IsExpanded || row.Kind == SurfaceKind.ValueLine))
            return new CellSelection(row.Path, 0, Math.Max(0, scalarLength), true, null, row.Kind);
        if (row.ScalarText)
        {
            var length = row.Value?.Length ?? 0;
            return new CellSelection(row.Path, row.TextStart, row.TextStart + length, true, null, row.Kind);
        }

        var text = label ?? "";
        return new CellSelection(row.Path, 0, text.Length, false, text, row.Kind);
    }

    private static CellSelection Between(SurfaceRow row, int anchor, int focus, string? label)
    {
        var start = Math.Min(anchor, focus);
        var end = Math.Max(anchor, focus);
        return new CellSelection(row.Path, start, end, row.ScalarText, row.ScalarText ? null : label, row.Kind);
    }
}
