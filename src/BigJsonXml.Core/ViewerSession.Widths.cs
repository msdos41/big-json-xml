using BigJsonXml.Xml;

namespace BigJsonXml;

public sealed partial class ViewerSession
{
    private sealed class WidthState
    {
        public double Stored = DefaultWidth;
        public double? Pinned;
    }

    private readonly Dictionary<DocumentPath, WidthState> _nameWidths = new();
    private readonly Dictionary<DocumentPath, WidthState> _valueWidths = new();
    private readonly Dictionary<DocumentPath, Dictionary<string, WidthState>> _columnWidths = new();

    public static double IndexTrackWidth(int entryCount)
    {
        var digits = Math.Max(1, entryCount).ToString(System.Globalization.CultureInfo.InvariantCulture).Length;
        return digits * Advance;
    }

    public void SetRecordNameWidth(DocumentPath recordPath, double width)
    {
        var state = Width(_nameWidths, recordPath);
        state.Stored = Math.Max(MinWidth, width);
        state.Pinned = null;
    }

    public void SetRecordValueWidth(DocumentPath recordPath, double width)
    {
        Assign(Width(_valueWidths, recordPath), Math.Max(MinWidth, width), RecordHasEmbedded(recordPath));
    }

    public void SetColumnWidth(DocumentPath listPath, string column, double width)
    {
        ArgumentException.ThrowIfNullOrEmpty(column);
        Assign(ColumnState(listPath, column), Math.Max(HeaderFloor(column), width), ColumnHasEmbedded(listPath, column));
    }

    private void ReleaseUnusedPins()
    {
        foreach (var (path, state) in _valueWidths)
        {
            if (state.Pinned is not null && !RecordHasEmbedded(path))
                state.Pinned = null;
        }

        foreach (var (list, columns) in _columnWidths)
        {
            foreach (var (column, state) in columns)
            {
                if (state.Pinned is not null && !ColumnHasEmbedded(list, column))
                    state.Pinned = null;
            }
        }
    }

    private double ShownName(DocumentPath recordPath)
    {
        var state = Width(_nameWidths, recordPath);
        return state.Pinned ?? state.Stored;
    }

    private double ShownValue(DocumentPath recordPath)
    {
        var state = Width(_valueWidths, recordPath);
        if (state.Pinned is double pinned)
            return pinned;
        var width = state.Stored;
        foreach (var path in _expanded)
        {
            if (IsDirectField(recordPath, path))
                width = Math.Max(width, EmbeddedOuter(path));
        }

        return width;
    }

    private double ShownColumn(DocumentPath listPath, string column)
    {
        var state = ColumnState(listPath, column);
        if (state.Pinned is double pinned)
            return pinned;
        var width = state.Stored;
        foreach (var path in _expanded)
        {
            if (IsColumnCell(listPath, column, path))
                width = Math.Max(width, EmbeddedOuter(path));
        }

        return width;
    }

    private double EmbeddedOuter(DocumentPath path)
    {
        var (_, slot) = Locate(path);
        if (slot.Kind == SlotKind.List)
            return ListOuter(path, slot);
        if (slot.Kind == SlotKind.Record)
            return ShownName(path) + ShownValue(path);
        return 0;
    }

    private double ListOuter(DocumentPath listPath, Slot slot)
    {
        var width = IndexTrackWidth(slot.Count);
        foreach (var column in slot.Columns)
            width += ShownColumn(listPath, column.Name);
        return width;
    }

    private double ScalarWidth(DocumentPath path)
    {
        if (path.Steps.Count >= 2 && path.Steps[^2].OneBasedIndex is int)
            return ShownColumn(ListPath(path), path.Steps[^1].Name);
        return ShownValue(WithoutLast(path));
    }

    private bool RecordHasEmbedded(DocumentPath recordPath)
    {
        foreach (var path in _expanded)
        {
            if (IsDirectField(recordPath, path))
                return true;
        }

        return false;
    }

    private bool ColumnHasEmbedded(DocumentPath listPath, string column)
    {
        foreach (var path in _expanded)
        {
            if (IsColumnCell(listPath, column, path))
                return true;
        }

        return false;
    }

    private static bool IsDirectField(DocumentPath recordPath, DocumentPath field)
    {
        if (field.Steps.Count != recordPath.Steps.Count + 1)
            return false;
        for (var i = 0; i < recordPath.Steps.Count; i++)
        {
            if (field.Steps[i] != recordPath.Steps[i])
                return false;
        }

        return field.Steps[^1].OneBasedIndex is null;
    }

    private static bool IsColumnCell(DocumentPath listPath, string column, DocumentPath field)
    {
        if (field.Steps.Count != listPath.Steps.Count + 1)
            return false;
        for (var i = 0; i < listPath.Steps.Count; i++)
        {
            var expected = listPath.Steps[i];
            var actual = field.Steps[i];
            if (actual.Name != expected.Name)
                return false;
            if (i < listPath.Steps.Count - 1 && actual.OneBasedIndex != expected.OneBasedIndex)
                return false;
        }

        if (field.Steps[listPath.Steps.Count - 1].OneBasedIndex is null)
            return false;
        var last = field.Steps[^1];
        return last.OneBasedIndex is null && last.Name == column;
    }

    private static DocumentPath WithoutLast(DocumentPath path)
    {
        var steps = new DocumentPath.Step[path.Steps.Count - 1];
        for (var i = 0; i < steps.Length; i++)
            steps[i] = path.Steps[i];
        return DocumentPath.From(steps);
    }

    private static DocumentPath ListPath(DocumentPath scalar)
    {
        var entry = WithoutLast(scalar);
        var steps = new DocumentPath.Step[entry.Steps.Count];
        for (var i = 0; i < steps.Length; i++)
            steps[i] = entry.Steps[i];
        var last = steps[^1];
        steps[^1] = new DocumentPath.Step(last.Name, null);
        return DocumentPath.From(steps);
    }

    private static double HeaderFloor(string header)
    {
        var natural = Math.Max(1, header.Length) * Advance;
        return Math.Min(MinWidth, natural);
    }

    private static int Capacity(double width) => Math.Max(1, (int)(width / Advance));

    private static int Place(int matchStart, int matchLength, int length, int capacity)
    {
        var match = Math.Max(1, matchLength);
        var last = Math.Max(0, length - capacity);
        if (match >= capacity)
            return Math.Clamp(matchStart, 0, last);

        var start = matchStart - ((capacity - match) / 2);
        if (start < 0)
            start = 0;
        if (start > last)
            start = last;
        if (matchStart + match > start + capacity)
            start = matchStart + match - capacity;
        if (start < 0)
            start = 0;
        if (start > matchStart)
            start = matchStart;
        return start;
    }

    private static void Assign(WidthState state, double width, bool open)
    {
        if (open)
            state.Pinned = width;
        else
        {
            state.Stored = width;
            state.Pinned = null;
        }
    }

    private static WidthState Width(Dictionary<DocumentPath, WidthState> table, DocumentPath path)
    {
        if (!table.TryGetValue(path, out var state))
        {
            state = new WidthState();
            table[path] = state;
        }

        return state;
    }

    private WidthState ColumnState(DocumentPath listPath, string column)
    {
        if (!_columnWidths.TryGetValue(listPath, out var columns))
        {
            columns = new Dictionary<string, WidthState>(StringComparer.Ordinal);
            _columnWidths[listPath] = columns;
        }

        if (!columns.TryGetValue(column, out var state))
        {
            state = new WidthState();
            columns[column] = state;
        }

        return state;
    }
}
