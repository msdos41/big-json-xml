using BigJsonXml.Xml;

namespace BigJsonXml;

public sealed partial class ViewerSession
{
    public SurfaceFrame ReadSurface()
    {
        var rows = new List<SurfaceRow>();
        double width = RowSpan;
        var height = 0d;
        if (_tree.Root is not null)
            Walk(_tree.Root, 0, ref height, ref width, rows, find: null);
        return new SurfaceFrame(width, height, ViewportX, ViewportY, rows, Error, ScrollSurfaces);
    }

    private void ScrollIntoView(DocumentPath path)
    {
        double width = 0;
        var y = 0d;
        if (_tree.Root is null || !Walk(_tree.Root, 0, ref y, ref width, emit: null, find: path))
            return;
        if (y < ViewportY || y + RowHeight > ViewportY + ViewportHeight)
            ViewportY = Math.Max(0, y - RowHeight);
    }

    private bool Walk(Frame frame, int indent, ref double y, ref double width, List<SurfaceRow>? emit, DocumentPath? find)
    {
        var found = false;
        foreach (var slot in frame.Slots)
        {
            var path = frame.Path.Field(slot.Name);
            var x = indent * 16d;
            width = Math.Max(width, x + RowSpan);
            if (find is not null && find.Equals(path))
                return true;
            if (slot.Kind == SlotKind.Scalar && Full(path))
            {
                if (EmitFull(slot, path, x, ref y, emit))
                    found = true;
                if (found && emit is null)
                    return true;
                continue;
            }

            var rowHeight = RowHeight;
            if (Visible(y, rowHeight) && emit is not null)
                emit.Add(Row(slot, path, x, y));
            y += rowHeight;
            if (slot.Kind == SlotKind.List && _expanded.Contains(path))
            {
                if (WalkList(frame, slot, path, indent + 1, ref y, ref width, emit, find))
                    return true;
            }
            else if (slot.Kind == SlotKind.Record && _expanded.Contains(path))
            {
                var child = Opened(slot, path);
                if (Walk(child, indent + 1, ref y, ref width, emit, find))
                    return true;
            }
        }

        return found;
    }

    private bool WalkList(
        Frame parent,
        Slot slot,
        DocumentPath path,
        int indent,
        ref double y,
        ref double width,
        List<SurfaceRow>? emit,
        DocumentPath? find)
    {
        var x = indent * 16d;
        width = Math.Max(width, x + RowSpan);
        if (find is not null && find.Equals(path))
            return true;
        if (Visible(y, RowHeight) && emit is not null)
        {
            emit.Add(new SurfaceRow(
                x, y, RowSpan, RowHeight,
                "", string.Join("  ", ColumnNames(slot)),
                false, false, false, path, SurfaceKind.ColumnHeader));
        }

        y += RowHeight;
        var index = EnsureOffsets(path, parent, slot);
        var count = slot.Count;
        var body = y;
        if (HasInner(path))
            return WalkVariable(slot, path, index, count, body, x, indent, ref y, ref width, emit, find);

        if (find is not null && TryEntryIndex(path, find, out var entryIndex))
        {
            y = body + (entryIndex - 1) * RowHeight;
            return true;
        }

        if (emit is not null && count > 0)
        {
            var viewTop = ViewportY;
            var viewBottom = ViewportY + ViewportHeight;
            var first = 0;
            var last = 0;
            if (body < viewBottom && body + (count * RowHeight) > viewTop)
            {
                first = (int)Math.Max(0, Math.Floor((viewTop - body) / RowHeight) - 1);
                last = (int)Math.Min(count, Math.Ceiling((viewBottom - body) / RowHeight) + 1);
            }

            for (var i = first; i < last; i++)
            {
                var top = body + (i * RowHeight);
                if (!Visible(top, RowHeight) || i >= index.Offsets.Count)
                    continue;
                EmitEntry(slot, path, index, i, top, x, emit);
            }
        }

        y = body + (count * RowHeight);
        return false;
    }

    private bool WalkVariable(
        Slot slot,
        DocumentPath path,
        OffsetIndex index,
        int count,
        double body,
        double x,
        int indent,
        ref double y,
        ref double width,
        List<SurfaceRow>? emit,
        DocumentPath? find)
    {
        var extras = new Dictionary<int, double>();
        foreach (var entryIndex in InnerEntries(path))
        {
            if (entryIndex < 1 || entryIndex > index.Offsets.Count)
                continue;
            var entryPath = path.WithIndex(entryIndex);
            var entry = LoadElement(index.Offsets[entryIndex - 1], slot.ParentScope, entryPath);
            Normalize(entry);
            double height = 0;
            double wide = width;
            WalkEmbedded(entry, entryPath, indent + 1, ref height, ref wide, null, null);
            extras[entryIndex] = height;
            width = Math.Max(width, wide);
        }

        double Top(int zeroBased)
        {
            var top = body + (zeroBased * RowHeight);
            foreach (var (entryIndex, extra) in extras)
            {
                if (entryIndex - 1 < zeroBased)
                    top += extra;
            }

            return top;
        }

        if (find is not null && TryEntryIndex(path, find, out var wanted) && wanted >= 1 && wanted <= index.Offsets.Count)
        {
            var entryPath = path.WithIndex(wanted);
            var direct = find.Steps.Count <= entryPath.Steps.Count
                || !_expanded.Contains(entryPath.Field(find.Steps[entryPath.Steps.Count].Name));
            if (direct)
            {
                y = Top(wanted - 1);
                return true;
            }

            y = Top(wanted - 1) + RowHeight;
            var entry = LoadElement(index.Offsets[wanted - 1], slot.ParentScope, entryPath);
            Normalize(entry);
            return WalkEmbedded(entry, entryPath, indent + 1, ref y, ref width, emit, find);
        }

        if (emit is not null && count > 0 && index.Offsets.Count > 0)
        {
            var limit = Math.Min(count, index.Offsets.Count);
            var first = Lower(Top, extras, limit, ViewportY);
            var last = Math.Min(limit, Lower(Top, extras, limit, ViewportY + ViewportHeight) + 1);
            for (var i = first; i < last; i++)
            {
                var top = Top(i);
                var extra = extras.GetValueOrDefault(i + 1);
                if (Visible(top, RowHeight))
                    EmitEntry(slot, path, index, i, top, x, emit);
                if (extra > 0)
                {
                    var entryPath = path.WithIndex(i + 1);
                    var entry = LoadElement(index.Offsets[i], slot.ParentScope, entryPath);
                    Normalize(entry);
                    double inner = top + RowHeight;
                    WalkEmbedded(entry, entryPath, indent + 1, ref inner, ref width, emit, null);
                }
            }
        }

        double totalExtra = 0;
        foreach (var extra in extras.Values)
            totalExtra += extra;
        y = body + (count * RowHeight) + totalExtra;
        return false;
    }

    private void EmitEntry(Slot slot, DocumentPath path, OffsetIndex index, int zeroBased, double top, double x, List<SurfaceRow> emit)
    {
        var entryPath = path.WithIndex(zeroBased + 1);
        var entry = LoadElement(index.Offsets[zeroBased], slot.ParentScope, entryPath);
        Normalize(entry);
        DocumentPath? expandPath = null;
        DocumentPath? growPath = null;
        foreach (var column in slot.Columns)
        {
            if (!entry.ByKey.TryGetValue(column.Key, out var cell))
                continue;
            if (expandPath is null && cell.Kind is SlotKind.Record or SlotKind.List)
                expandPath = entryPath.Field(cell.Name);
            if (growPath is null && cell.Kind == SlotKind.Scalar)
            {
                var cellPath = entryPath.Field(cell.Name);
                var start = _chrome.TryGetValue(cellPath, out var chrome) ? chrome.WindowStart : 0;
                _ = ExpandedText.Slice(_document, cell.Text, start, ScalarWindowChars, out var more);
                if (!Full(cellPath) && (start > 0 || more))
                    growPath = cellPath;
            }
        }

        emit.Add(new SurfaceRow(
            x, top, RowSpan, RowHeight,
            (zeroBased + 1).ToString(), Join(entry, slot.Columns, entryPath),
            expandPath is not null, expandPath is not null && _expanded.Contains(expandPath),
            Marks(entryPath), entryPath, SurfaceKind.Entry,
            growPath is not null, expandPath, growPath));
    }

    private bool WalkEmbedded(
        Frame entry,
        DocumentPath entryPath,
        int indent,
        ref double y,
        ref double width,
        List<SurfaceRow>? emit,
        DocumentPath? find)
    {
        foreach (var field in entry.Slots)
        {
            var cell = entryPath.Field(field.Name);
            if (field.Kind == SlotKind.Record && _expanded.Contains(cell))
            {
                var child = Opened(field, cell);
                if (Walk(child, indent, ref y, ref width, emit, find))
                    return true;
            }
            else if (field.Kind == SlotKind.List && _expanded.Contains(cell))
            {
                if (WalkList(entry, field, cell, indent, ref y, ref width, emit, find))
                    return true;
            }
            else if (field.Kind == SlotKind.Scalar && Full(cell))
            {
                EmitFull(field, cell, indent * 16d, ref y, emit);
            }
        }

        return false;
    }

    private bool HasInner(DocumentPath listPath)
    {
        foreach (var path in _expanded)
        {
            if (path.Steps.Count > listPath.Steps.Count && TryEntryIndex(listPath, path, out _))
                return true;
        }

        foreach (var (path, chrome) in _chrome)
        {
            if (chrome.FullValue && path.Steps.Count > listPath.Steps.Count && TryEntryIndex(listPath, path, out _))
                return true;
        }

        return false;
    }

    private List<int> InnerEntries(DocumentPath listPath)
    {
        var indexes = new List<int>();
        void Add(DocumentPath path)
        {
            if (path.Steps.Count > listPath.Steps.Count && TryEntryIndex(listPath, path, out var index) && !indexes.Contains(index))
                indexes.Add(index);
        }

        foreach (var path in _expanded)
            Add(path);
        foreach (var (path, chrome) in _chrome)
        {
            if (chrome.FullValue)
                Add(path);
        }

        return indexes;
    }

    private static int Lower(Func<int, double> top, Dictionary<int, double> extras, int count, double probe)
    {
        var lo = 0;
        var hi = count;
        while (lo < hi)
        {
            var mid = (lo + hi) / 2;
            var bottom = top(mid) + RowHeight + extras.GetValueOrDefault(mid + 1);
            if (bottom <= probe)
                lo = mid + 1;
            else
                hi = mid;
        }

        return lo;
    }

    private bool EmitFull(Slot slot, DocumentPath path, double x, ref double y, List<SurfaceRow>? emit)
    {
        var length = LengthOf(path, slot.Text);
        var lines = Math.Max(1, (length + FullValueColumns - 1) / FullValueColumns);
        for (var line = 0; line < lines; line++)
        {
            var top = y + (line * RowHeight);
            if (emit is not null && Visible(top, RowHeight))
            {
                var text = ExpandedText.Slice(_document, slot.Text, line * FullValueColumns, FullValueColumns, out _);
                emit.Add(new SurfaceRow(
                    x, top, RowSpan, RowHeight,
                    line == 0 ? slot.Name : "", text,
                    false, false, Marked is not null && Marked.Equals(path), path, SurfaceKind.ValueLine));
            }
        }

        y += lines * RowHeight;
        return false;
    }

    private SurfaceRow Row(Slot slot, DocumentPath path, double x, double y)
    {
        string? value = null;
        var canGrow = false;
        if (slot.Kind == SlotKind.List)
            value = $"{slot.Name}[{slot.Count}]";
        else if (slot.Kind == SlotKind.Scalar)
        {
            var windowStart = _chrome.TryGetValue(path, out var chrome) ? chrome.WindowStart : 0;
            value = ExpandedText.Slice(_document, slot.Text, windowStart, ScalarWindowChars, out var more);
            canGrow = !Full(path) && (windowStart > 0 || more);
        }

        var marked = Marked is not null && (Marked.Equals(path) || (slot.Kind == SlotKind.List && Marks(path)));
        return new SurfaceRow(
            x, y, RowSpan, RowHeight,
            slot.Name, value,
            slot.Kind != SlotKind.Scalar, _expanded.Contains(path), marked, path,
            SurfaceKind.Field, canGrow);
    }

    private string Join(Frame entry, List<Column> columns, DocumentPath entryPath)
    {
        var parts = new List<string>(columns.Count);
        foreach (var column in columns)
        {
            if (!entry.ByKey.TryGetValue(column.Key, out var slot))
            {
                parts.Add("");
                continue;
            }

            if (slot.Kind == SlotKind.List)
                parts.Add($"{slot.Name}[{slot.Count}]");
            else if (slot.Kind == SlotKind.Record)
                parts.Add(slot.Name);
            else
            {
                var cell = entryPath.Field(slot.Name);
                var start = _chrome.TryGetValue(cell, out var chrome) ? chrome.WindowStart : 0;
                parts.Add(ExpandedText.Slice(_document, slot.Text, start, ScalarWindowChars, out _));
            }
        }

        return string.Join(" | ", parts);
    }

    private int LengthOf(DocumentPath path, List<TextPiece> pieces)
    {
        if (_chrome.TryGetValue(path, out var chrome) && chrome.Length is int known)
            return known;
        var length = ExpandedText.Count(_document, pieces);
        _chrome[path] = new Chrome { Length = length, FullValue = _chrome.TryGetValue(path, out var existing) && existing.FullValue, WindowStart = existing?.WindowStart ?? 0 };
        return length;
    }

    private bool Full(DocumentPath path) => _chrome.TryGetValue(path, out var chrome) && chrome.FullValue;

    private bool Visible(double y, double height) => y < ViewportY + ViewportHeight && y + height > ViewportY;

    private bool Marks(DocumentPath path)
    {
        if (Marked is null)
            return false;
        if (Marked.Equals(path))
            return true;
        return IsUnder(Marked, path);
    }

    private static bool TryEntryIndex(DocumentPath listPath, DocumentPath target, out int index)
    {
        index = 0;
        if (target.Steps.Count < listPath.Steps.Count)
            return false;
        for (var i = 0; i < listPath.Steps.Count; i++)
        {
            var expected = listPath.Steps[i];
            var actual = target.Steps[i];
            if (actual.Name != expected.Name)
                return false;
            if (i < listPath.Steps.Count - 1 && actual.OneBasedIndex != expected.OneBasedIndex)
                return false;
        }

        if (target.Steps[listPath.Steps.Count - 1].OneBasedIndex is not int found)
            return false;
        index = found;
        return true;
    }
}
