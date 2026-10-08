using BigJsonXml.Xml;

namespace BigJsonXml;

public sealed partial class ViewerSession
{
    private sealed class WidthState
    {
        public double Stored = DefaultWidth;
        public double? Override;
    }

    private readonly struct Host
    {
        public Host(DocumentPath table, bool record, string? column)
        {
            Table = table;
            Record = record;
            Column = column;
        }

        public DocumentPath Table { get; }
        public bool Record { get; }
        public string? Column { get; }
    }

    private readonly Dictionary<DocumentPath, WidthState> _nameWidths = new();
    private readonly Dictionary<DocumentPath, WidthState> _valueWidths = new();
    private readonly Dictionary<DocumentPath, Dictionary<string, WidthState>> _columnWidths = new();

    public static double IndexTrackWidth(int entryCount)
    {
        var digits = Math.Max(1, entryCount).ToString(System.Globalization.CultureInfo.InvariantCulture).Length;
        // One extra advance holds the text inset and the gap before the right border.
        return (digits + 1) * Advance;
    }

    public void SetRecordNameWidth(DocumentPath recordPath, double width)
    {
        var state = Width(_nameWidths, recordPath);
        var old = state.Stored;
        var desired = Math.Max(MinWidth, width);
        var delta = desired - old;
        if (delta < 0)
            delta = ClampDelta(recordPath, delta, old - MinWidth);
        var shots = Snapshot(recordPath);
        state.Stored = old + delta;
        ApplySnapshots(shots, delta, recordPath);
    }

    public void SetRecordValueWidth(DocumentPath recordPath, double width)
    {
        var host = new Host(recordPath, record: true, column: null);
        var desired = Math.Max(EdgeFloor(host), width);
        if (TryParent(recordPath, out var parent))
        {
            ApplyChain(parent, OwnName(recordPath) + desired + ExpandGutter);
            return;
        }

        var old = HostShown(host);
        var shots = Snapshot(recordPath);
        AssignTop(host, desired);
        ApplySnapshots(shots, desired - old, recordPath);
    }

    public void SetColumnWidth(DocumentPath listPath, string column, double width)
    {
        ArgumentException.ThrowIfNullOrEmpty(column);
        if (!TryList(listPath, out var slot))
        {
            var state = ColumnState(listPath, column);
            state.Stored = Math.Max(MinWidth, width);
            state.Override = null;
            return;
        }

        var host = new Host(listPath, record: false, column: column);
        var desired = Math.Max(EdgeFloor(host), width);
        var old = HostShown(host);
        var delta = desired - old;
        if (delta < 0)
            delta = ClampDelta(listPath, delta, old - EdgeFloor(host));
        desired = old + delta;
        var shots = Snapshot(listPath);
        AssignTop(host, desired);
        ApplySnapshots(shots, delta, listPath);
    }

    private void ApplyChain(Host host, double desired)
    {
        desired = Math.Max(desired, EdgeFloor(host));
        if (IsFlexible(host) && TryParent(host.Table, out var parent))
        {
            ApplyChain(parent, CellWidthFor(host, desired));
            return;
        }

        var old = HostShown(host);
        var shots = Snapshot(host.Table);
        AssignTop(host, desired);
        ApplySnapshots(shots, desired - old, host.Table);
    }

    private void AssignTop(Host host, double width)
    {
        width = Math.Max(width, EdgeFloor(host));
        foreach (var cell in OpenCells(host))
            CommitTable(cell, width - ExpandGutter);
        Adopt(host, width);
    }

    // A width the open tables already produce is not stored again. A width that
    // falls short of the stored width is an override until those tables close.
    private void Adopt(Host host, double width)
    {
        var state = HostState(host);
        if (Same(Natural(host), width))
        {
            state.Override = null;
            return;
        }

        if (HasEmbedded(host))
        {
            state.Override = width;
            return;
        }

        state.Stored = Math.Max(MinWidth, width);
        state.Override = null;
    }

    // An embedded table keeps a wider drag after its own child closes. A drag
    // narrower than the stored width stays an override until that child closes.
    private void Remember(Host host, double width)
    {
        var state = HostState(host);
        if (HasEmbedded(host) && width + 0.01 < state.Stored)
        {
            state.Override = width;
            return;
        }

        state.Stored = Math.Max(MinWidth, width);
        state.Override = null;
    }

    private double Natural(Host host)
    {
        var width = HostState(host).Stored;
        foreach (var cell in OpenCells(host))
            width = Math.Max(width, OwnOuter(cell) + ExpandGutter);
        return width;
    }

    private static bool Same(double left, double right) => Math.Abs(left - right) < 0.01;

    private void CommitTable(DocumentPath path, double outer)
    {
        var (_, slot) = Locate(path);
        if (slot.Kind == SlotKind.List)
        {
            CommitList(path, slot, outer);
            return;
        }

        if (slot.Kind == SlotKind.Record && TrySoleList(slot, path, out _, out var sole))
        {
            CommitList(path.Field(sole.Name), sole, outer);
            return;
        }

        if (slot.Kind != SlotKind.Record)
            return;

        var name = OwnName(path);
        var value = outer - name;
        foreach (var child in OpenFields(path))
            CommitTable(child, value - ExpandGutter);
        Remember(new Host(path, record: true, column: null), value);
    }

    private void CommitList(DocumentPath listPath, Slot slot, double outer)
    {
        if (slot.Columns.Count == 0)
            return;
        var fixedWidth = IndexTrackWidth(slot.Count);
        for (var i = 0; i < slot.Columns.Count - 1; i++)
            fixedWidth += OwnColumn(listPath, slot.Columns[i].Name);
        var last = slot.Columns[^1].Name;
        var flexible = outer - fixedWidth;
        foreach (var cell in OpenColumnCells(listPath, last))
            CommitTable(cell, flexible - ExpandGutter);
        Remember(new Host(listPath, record: false, column: last), flexible);
    }

    private double ClampDelta(DocumentPath table, double delta, double shrinkRoom)
    {
        var cursor = table;
        while (TryParent(cursor, out var host))
        {
            var minDelta = TranslatingFloor(host, cursor, shrinkRoom) - HostShown(host);
            if (delta < minDelta)
                delta = minDelta;
            cursor = host.Table;
        }

        return delta;
    }

    private List<(Host Host, double Shown)> Snapshot(DocumentPath table)
    {
        var shots = new List<(Host, double)>();
        var cursor = table;
        while (TryParent(cursor, out var host))
        {
            shots.Add((host, HostShown(host)));
            cursor = host.Table;
        }

        return shots;
    }

    private void ApplySnapshots(List<(Host Host, double Shown)> shots, double delta, DocumentPath origin)
    {
        if (delta == 0)
            return;
        foreach (var (host, shown) in shots)
        {
            var desired = shown + delta;
            foreach (var cell in OpenCells(host))
            {
                if (Holds(cell, origin))
                    continue;
                CommitTable(cell, desired - ExpandGutter);
            }

            Adopt(host, desired);
        }
    }

    private void ReleaseUnusedPins()
    {
        foreach (var (path, state) in _valueWidths)
        {
            if (state.Override is not null && !RecordHasEmbedded(path))
                state.Override = null;
        }

        foreach (var (list, columns) in _columnWidths)
        {
            foreach (var (column, state) in columns)
            {
                if (state.Override is not null && !ColumnHasEmbedded(list, column))
                    state.Override = null;
            }
        }
    }

    private double ShownName(DocumentPath recordPath) => OwnName(recordPath);

    private double ShownValue(DocumentPath recordPath)
    {
        var own = OwnValue(recordPath);
        var outer = OwnName(recordPath) + own;
        var slot = TableSlot(recordPath);
        return slot > outer ? own + (slot - outer) : own;
    }

    private double ShownColumn(DocumentPath listPath, string column)
    {
        var own = OwnColumn(listPath, column);
        if (!TryList(listPath, out var slot) || !IsLast(slot, column))
            return own;
        var outer = ListOwnOuter(listPath, slot);
        var tableSlot = TableSlot(listPath);
        return tableSlot > outer ? own + (tableSlot - outer) : own;
    }

    private double OwnName(DocumentPath recordPath) => Width(_nameWidths, recordPath).Stored;

    private double OwnValue(DocumentPath recordPath)
    {
        var state = Width(_valueWidths, recordPath);
        var width = state.Override ?? state.Stored;
        foreach (var path in OpenFields(recordPath))
            width = Math.Max(width, OwnOuter(path) + ExpandGutter);
        return width;
    }

    private double OwnColumn(DocumentPath listPath, string column)
    {
        var state = ColumnState(listPath, column);
        var width = state.Override ?? state.Stored;
        foreach (var path in OpenColumnCells(listPath, column))
            width = Math.Max(width, OwnOuter(path) + ExpandGutter);
        return width;
    }

    private double OwnOuter(DocumentPath path)
    {
        var (_, slot) = Locate(path);
        if (slot.Kind == SlotKind.List)
            return ListOwnOuter(path, slot);
        if (slot.Kind == SlotKind.Record)
        {
            if (TrySoleList(slot, path, out _, out var list))
                return ListOwnOuter(path.Field(list.Name), list);
            return OwnName(path) + OwnValue(path);
        }

        return 0;
    }

    private double ListOwnOuter(DocumentPath listPath, Slot slot)
    {
        var width = IndexTrackWidth(slot.Count);
        foreach (var column in slot.Columns)
            width += OwnColumn(listPath, column.Name);
        return width;
    }

    private double ListOuterWith(DocumentPath listPath, Slot slot, string column, double columnWidth)
    {
        var width = IndexTrackWidth(slot.Count);
        foreach (var item in slot.Columns)
            width += item.Name == column ? columnWidth : OwnColumn(listPath, item.Name);
        return width;
    }

    private double TableSlot(DocumentPath table)
    {
        if (table.Steps.Count == 0)
            return OwnName(table) + OwnValue(table);
        if (TrySoleContainer(table, out var cell))
            return HostCellWidth(cell) - ExpandGutter;
        if (_expanded.Contains(table))
            return HostCellWidth(table) - ExpandGutter;
        return OwnOuter(table);
    }

    private double HostCellWidth(DocumentPath cell)
    {
        if (cell.Steps.Count >= 2 && cell.Steps[^2].OneBasedIndex is int)
            return ShownColumn(ListPath(cell), cell.Steps[^1].Name);
        return ShownValue(WithoutLast(cell));
    }

    private double CellWidthFor(Host host, double columnWidth)
    {
        if (host.Record)
            return OwnName(host.Table) + columnWidth + ExpandGutter;
        var (_, slot) = Locate(host.Table);
        return ListOuterWith(host.Table, slot, host.Column!, columnWidth) + ExpandGutter;
    }

    private double HostShown(Host host) =>
        host.Record ? ShownValue(host.Table) : ShownColumn(host.Table, host.Column!);

    private double EdgeFloor(Host host)
    {
        var floor = MinWidth;
        foreach (var cell in OpenCells(host))
            floor = Math.Max(floor, ExpandGutter + MinOuter(cell));
        return floor;
    }

    private double TranslatingFloor(Host host, DocumentPath origin, double shrinkRoom)
    {
        var floor = MinWidth;
        foreach (var cell in OpenCells(host))
        {
            var outer = Holds(cell, origin) ? OwnOuter(cell) - shrinkRoom : MinOuter(cell);
            floor = Math.Max(floor, ExpandGutter + outer);
        }

        return floor;
    }

    private double MinOuter(DocumentPath path)
    {
        var (_, slot) = Locate(path);
        if (slot.Kind == SlotKind.List)
            return ListMinOuter(path, slot);
        if (slot.Kind == SlotKind.Record && TrySoleList(slot, path, out _, out var sole))
            return ListMinOuter(path.Field(sole.Name), sole);
        if (slot.Kind == SlotKind.Record)
            return OwnName(path) + EdgeFloor(new Host(path, record: true, column: null));
        return 0;
    }

    private double ListMinOuter(DocumentPath listPath, Slot slot)
    {
        if (slot.Columns.Count == 0)
            return IndexTrackWidth(slot.Count);
        var width = IndexTrackWidth(slot.Count);
        for (var i = 0; i < slot.Columns.Count - 1; i++)
            width += OwnColumn(listPath, slot.Columns[i].Name);
        width += EdgeFloor(new Host(listPath, record: false, column: slot.Columns[^1].Name));
        return width;
    }

    private bool IsFlexible(Host host)
    {
        if (host.Record)
            return true;
        return TryList(host.Table, out var slot) && IsLast(slot, host.Column!);
    }

    private bool TryParent(DocumentPath table, out Host parent)
    {
        var cell = ContainingCell(table);
        if (cell is null)
        {
            parent = default;
            return false;
        }

        parent = HostOf(cell);
        return true;
    }

    private DocumentPath? ContainingCell(DocumentPath table)
    {
        if (table.Steps.Count == 0)
            return null;
        if (TrySoleContainer(table, out var cell))
            return cell;
        if (_expanded.Contains(table))
            return table;
        return null;
    }

    private bool TrySoleContainer(DocumentPath table, out DocumentPath cell)
    {
        cell = WithoutLast(table);
        if (cell.Steps.Count == 0 || !_expanded.Contains(cell))
            return false;
        var (_, slot) = Locate(cell);
        if (slot.Kind != SlotKind.Record || !TrySoleList(slot, cell, out _, out var list))
            return false;
        return cell.Field(list.Name).Equals(table);
    }

    private static Host HostOf(DocumentPath cell)
    {
        if (cell.Steps.Count >= 2 && cell.Steps[^2].OneBasedIndex is int)
            return new Host(ListPath(cell), record: false, column: cell.Steps[^1].Name);
        return new Host(WithoutLast(cell), record: true, column: null);
    }

    private bool HasEmbedded(Host host) =>
        host.Record ? RecordHasEmbedded(host.Table) : ColumnHasEmbedded(host.Table, host.Column!);

    private WidthState HostState(Host host) =>
        host.Record ? Width(_valueWidths, host.Table) : ColumnState(host.Table, host.Column!);

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

    private List<DocumentPath> OpenFields(DocumentPath recordPath)
    {
        var fields = new List<DocumentPath>();
        foreach (var path in _expanded)
        {
            if (IsDirectField(recordPath, path))
                fields.Add(path);
        }

        return fields;
    }

    private List<DocumentPath> OpenColumnCells(DocumentPath listPath, string column)
    {
        var cells = new List<DocumentPath>();
        foreach (var path in _expanded)
        {
            if (IsColumnCell(listPath, column, path))
                cells.Add(path);
        }

        return cells;
    }

    private List<DocumentPath> OpenCells(Host host) =>
        host.Record ? OpenFields(host.Table) : OpenColumnCells(host.Table, host.Column!);

    private bool TryList(DocumentPath listPath, out Slot slot)
    {
        try
        {
            (_, slot) = Locate(listPath);
        }
        catch (InvalidOperationException)
        {
            slot = null!;
            return false;
        }

        return slot.Kind == SlotKind.List || slot.Columns.Count > 0;
    }

    private static bool IsLast(Slot slot, string column) =>
        slot.Columns.Count > 0 && slot.Columns[^1].Name == column;

    private static bool Holds(DocumentPath cell, DocumentPath table)
    {
        if (cell.Equals(table))
            return true;
        if (table.Steps.Count <= cell.Steps.Count)
            return false;
        for (var i = 0; i < cell.Steps.Count; i++)
        {
            var outer = cell.Steps[i];
            var inner = table.Steps[i];
            if (inner.Name != outer.Name)
                return false;
            // A list path has no entry index, so it holds every entry under it.
            if (outer.OneBasedIndex is int index && index != inner.OneBasedIndex)
                return false;
        }

        return true;
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
