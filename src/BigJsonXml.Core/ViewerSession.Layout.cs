using System.Globalization;
using BigJsonXml.Xml;

namespace BigJsonXml;

public sealed partial class ViewerSession
{
    private sealed class Seek(DocumentPath path)
    {
        public DocumentPath Path { get; } = path;
        public bool Found { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
    }

    public SurfaceFrame ReadSurface()
    {
        ReleaseUnusedPins();
        var rows = new List<SurfaceRow>();
        double height = 0;
        double width = 0;
        if (_tree.Root is not null)
        {
            height = LayoutRecord(_tree.Root, DocumentPath.Root, 0, 0, double.PositiveInfinity, rows, null);
            width = ShownName(DocumentPath.Root) + ShownValue(DocumentPath.Root);
        }

        return new SurfaceFrame(width, height, ViewportX, ViewportY, rows, Error, ScrollSurfaces);
    }

    private void ScrollIntoView(DocumentPath path)
    {
        if (_tree.Root is null)
            return;
        var seek = new Seek(path);
        _ = LayoutRecord(_tree.Root, DocumentPath.Root, 0, 0, double.PositiveInfinity, null, seek);
        if (!seek.Found)
            return;
        if (seek.Y < ViewportY || seek.Y + RowHeight > ViewportY + ViewportHeight)
            ViewportY = Math.Max(0, seek.Y - RowHeight);
        if (seek.X < ViewportX || seek.X + Math.Min(seek.Width, ViewportWidth) > ViewportX + ViewportWidth)
            ViewportX = Math.Max(0, seek.X);
    }

    private double LayoutRecord(
        Frame frame,
        DocumentPath path,
        double x,
        double y,
        double clipRight,
        List<SurfaceRow>? emit,
        Seek? seek)
    {
        var nameWidth = ShownName(path);
        var valueWidth = ShownValue(path);
        var count = frame.Slots.Count;
        var variable = seek is not null || RecordVaries(path);
        var first = 0;
        var last = count;
        if (!variable && count > 0)
        {
            first = (int)Math.Max(0, Math.Floor((ViewportY - y) / RowHeight) - 1);
            last = (int)Math.Min(count, Math.Ceiling((ViewportY + ViewportHeight - y) / RowHeight) + 2);
            if (first > count)
                first = count;
            if (last < first)
                last = first;
        }

        var cursor = y + (first * RowHeight);
        for (var index = first; index < last; index++)
        {
            var slot = frame.Slots[index];
            var field = path.Field(slot.Name);
            var rowHeight = FieldBodyHeight(frame, slot, field, valueWidth);
            var expanded = _expanded.Contains(field);
            var full = slot.Kind == SlotKind.Scalar && Full(field);
            var canExpand = slot.Kind != SlotKind.Scalar;
            string? text = null;
            var cutOff = false;
            var canGrow = false;
            var start = 0;
            var soleList = false;
            Frame soleFrame = null!;
            Slot sole = null!;
            if (slot.Kind == SlotKind.List)
                text = ClipLabel($"{slot.Name}[{slot.Count}]", ExpandLabelWidth(valueWidth), out cutOff);
            else if (TrySoleList(slot, field, out soleFrame, out sole))
            {
                soleList = true;
                text = ClipLabel($"{sole.Name}[{sole.Count}]", ExpandLabelWidth(valueWidth), out cutOff);
            }
            else if (slot.Kind == SlotKind.Record)
                text = ClipLabel(slot.Closed ?? slot.Name, ExpandLabelWidth(valueWidth), out cutOff);
            else if (!full)
                text = WindowText(field, slot.Text, valueWidth, out cutOff, out canGrow, out start);
            else
                canGrow = true;

            var label = ClipLabel(slot.Name, nameWidth, out var nameCut);
            Emit(
                emit, seek, x, cursor, nameWidth, RowHeight, clipRight,
                slot.Name, label, canExpand, expanded, false, field, SurfaceKind.FieldName,
                canGrow, nameCut, WidthAxis.RecordName, path, null);

            var valueRight = Math.Min(clipRight, x + nameWidth + valueWidth);
            if (full)
            {
                EmitWrapped(emit, seek, field, slot.Name, slot.Text, x + nameWidth, cursor, valueWidth, valueRight, SurfaceKind.Field, WidthAxis.RecordValue, path, null);
            }
            else
            {
                Emit(
                    emit, seek, x + nameWidth, cursor, valueWidth, rowHeight, clipRight,
                    slot.Name, text, canExpand, expanded, Marks(field), field, SurfaceKind.Field,
                    canGrow, cutOff, WidthAxis.RecordValue, path, null,
                    start, slot.Kind == SlotKind.Scalar);
                var nest = expanded && canExpand ? RowHeight : 0;
                var inner = x + nameWidth + (nest > 0 ? ExpandGutter : 0);
                if (slot.Kind == SlotKind.List && expanded)
                    LayoutList(frame, slot, field, inner, cursor + nest, valueRight, emit, seek);
                else if (soleList && expanded)
                    LayoutList(soleFrame, sole, field.Field(sole.Name), inner, cursor + nest, valueRight, emit, seek);
                else if (slot.Kind == SlotKind.Record && expanded)
                    LayoutRecord(Opened(slot, field), field, inner, cursor + nest, valueRight, emit, seek);
            }

            cursor += rowHeight;
            if (seek is { Found: true })
                break;
        }

        if (!variable)
            return count * RowHeight;
        return cursor - y;
    }

    private bool RecordVaries(DocumentPath path)
    {
        foreach (var expanded in _expanded)
        {
            if (IsUnder(expanded, path))
                return true;
        }

        foreach (var (key, chrome) in _chrome)
        {
            if (chrome.FullValue && IsUnder(key, path))
                return true;
        }

        return false;
    }

    private double LayoutList(
        Frame parent,
        Slot slot,
        DocumentPath listPath,
        double x,
        double y,
        double clipRight,
        List<SurfaceRow>? emit,
        Seek? seek)
    {
        var indexWidth = IndexTrackWidth(slot.Count);
        var count = slot.Columns.Count;
        var widths = new double[count];
        var starts = new double[count];
        var cursor = x + indexWidth;
        for (var i = 0; i < count; i++)
        {
            widths[i] = ShownColumn(listPath, slot.Columns[i].Name);
            starts[i] = cursor;
            cursor += widths[i];
        }

        if (emit is not null)
        {
            Emit(emit, null, x, y, indexWidth, RowHeight, clipRight, "", null, false, false, false, listPath, SurfaceKind.EntryIndex, false, false, WidthAxis.None, null, null);
            for (var i = 0; i < count; i++)
            {
                var name = slot.Columns[i].Name;
                var label = ClipLabel(name, widths[i], out var cut);
                Emit(
                    emit, null, starts[i], y, widths[i], RowHeight, clipRight,
                    name, label, false, false, false, listPath, SurfaceKind.ColumnHeader,
                    false, cut, WidthAxis.ListColumn, listPath, name);
            }
        }

        var body = y + RowHeight;
        var entries = slot.Count;
        var extras = Extras(parent, slot, listPath);
        double extra = 0;
        foreach (var value in extras.Values)
            extra += value;
        var height = RowHeight + (entries * RowHeight) + extra;

        double Top(int zeroBased)
        {
            var top = body + (zeroBased * RowHeight);
            foreach (var (entryIndex, size) in extras)
            {
                if (entryIndex - 1 < zeroBased)
                    top += size;
            }

            return top;
        }

        if (entries <= 0)
            return height;

        var index = EnsureOffsets(listPath, parent, slot);
        var limit = Math.Min(entries, index.Offsets.Count);
        if (limit <= 0)
            return height;

        if (seek is not null && TryEntryIndex(listPath, seek.Path, out var wanted) && wanted >= 1 && wanted <= limit)
        {
            LayoutEntry(slot, index, wanted - 1, Top(wanted - 1), x, indexWidth, starts, widths, clipRight, null, seek, listPath);
            return height;
        }

        if (emit is null)
            return height;

        int first;
        int last;
        if (extras.Count == 0)
        {
            first = (int)Math.Max(0, Math.Floor((ViewportY - body) / RowHeight) - 1);
            last = (int)Math.Min(limit, Math.Ceiling((ViewportY + ViewportHeight - body) / RowHeight) + 1);
        }
        else
        {
            first = Lower(Top, extras, limit, ViewportY);
            last = Math.Min(limit, Lower(Top, extras, limit, ViewportY + ViewportHeight) + 1);
        }

        for (var i = Math.Max(0, first); i < last; i++)
            LayoutEntry(slot, index, i, Top(i), x, indexWidth, starts, widths, clipRight, emit, null, listPath);
        return height;
    }

    private void LayoutEntry(
        Slot slot,
        OffsetIndex index,
        int zeroBased,
        double top,
        double x,
        double indexWidth,
        double[] starts,
        double[] widths,
        double clipRight,
        List<SurfaceRow>? emit,
        Seek? seek,
        DocumentPath listPath)
    {
        var entryPath = listPath.WithIndex(zeroBased + 1);
        var entry = LoadEntry(slot, index, zeroBased + 1, entryPath);
        var count = slot.Columns.Count;
        var entryHeight = RowHeight;
        for (var i = 0; i < count; i++)
        {
            var column = slot.Columns[i];
            if (entry.FindKey(column.Key) is not Slot cell)
                continue;
            var height = FieldBodyHeight(entry, cell, entryPath.Field(column.Name), widths[i]);
            if (height > entryHeight)
                entryHeight = height;
        }

        Emit(
            emit, seek, x, top, indexWidth, entryHeight, clipRight,
            (zeroBased + 1).ToString(CultureInfo.InvariantCulture), null,
            false, false, Marks(entryPath), entryPath, SurfaceKind.EntryIndex,
            false, false, WidthAxis.None, null, null);

        for (var i = 0; i < count; i++)
        {
            var column = slot.Columns[i];
            var cellPath = entryPath.Field(column.Name);
            var cellRight = Math.Min(clipRight, starts[i] + widths[i]);
            if (entry.FindKey(column.Key) is not Slot cell)
            {
                Emit(emit, seek, starts[i], top, widths[i], entryHeight, clipRight, column.Name, "", false, false, false, cellPath, SurfaceKind.Entry, false, false, WidthAxis.ListColumn, listPath, column.Name);
                continue;
            }

            var expanded = _expanded.Contains(cellPath);
            var full = cell.Kind == SlotKind.Scalar && Full(cellPath);
            if (full)
            {
                EmitWrapped(emit, seek, cellPath, cell.Name, cell.Text, starts[i], top, widths[i], cellRight, SurfaceKind.Entry, WidthAxis.ListColumn, listPath, column.Name);
                continue;
            }

            string? text;
            var cutOff = false;
            var canGrow = false;
            var start = 0;
            var canExpand = cell.Kind != SlotKind.Scalar;
            var soleList = false;
            Frame soleFrame = null!;
            Slot sole = null!;
            if (cell.Kind == SlotKind.List)
                text = ClipLabel($"{cell.Name}[{cell.Count}]", ExpandLabelWidth(widths[i]), out cutOff);
            else if (TrySoleList(cell, cellPath, out soleFrame, out sole))
            {
                soleList = true;
                text = ClipLabel($"{sole.Name}[{sole.Count}]", ExpandLabelWidth(widths[i]), out cutOff);
            }
            else if (cell.Kind == SlotKind.Record)
                text = ClipLabel(cell.Closed ?? cell.Name, ExpandLabelWidth(widths[i]), out cutOff);
            else
                text = WindowText(cellPath, cell.Text, widths[i], out cutOff, out canGrow, out start);

            Emit(
                emit, seek, starts[i], top, widths[i], entryHeight, clipRight,
                cell.Name, text, canExpand, expanded, Marks(cellPath), cellPath, SurfaceKind.Entry,
                canGrow, cutOff, WidthAxis.ListColumn, listPath, column.Name,
                start, cell.Kind == SlotKind.Scalar);
            var nest = expanded && canExpand ? RowHeight : 0;
            var inner = starts[i] + (nest > 0 ? ExpandGutter : 0);
            if (cell.Kind == SlotKind.List && expanded)
                LayoutList(entry, cell, cellPath, inner, top + nest, cellRight, emit, seek);
            else if (soleList && expanded)
                LayoutList(soleFrame, sole, cellPath.Field(sole.Name), inner, top + nest, cellRight, emit, seek);
            else if (cell.Kind == SlotKind.Record && expanded)
                LayoutRecord(Opened(cell, cellPath), cellPath, inner, top + nest, cellRight, emit, seek);
        }
    }

    private double FieldBodyHeight(Frame parent, Slot slot, DocumentPath path, double valueWidth)
    {
        if (slot.Kind == SlotKind.List && _expanded.Contains(path))
            return RowHeight + ListHeight(parent, slot, path);
        if (slot.Kind == SlotKind.Record && _expanded.Contains(path))
        {
            if (TrySoleList(slot, path, out var frame, out var list))
                return RowHeight + ListHeight(frame, list, path.Field(list.Name));
            return RowHeight + RecordHeight(Opened(slot, path), path);
        }
        if (slot.Kind == SlotKind.Scalar && Full(path))
            return WrappedHeight(path, slot.Text, valueWidth);
        return RowHeight;
    }

    private double RecordHeight(Frame frame, DocumentPath path)
    {
        var valueWidth = ShownValue(path);
        var height = 0d;
        foreach (var slot in frame.Slots)
            height += FieldBodyHeight(frame, slot, path.Field(slot.Name), valueWidth);
        return height;
    }

    private double ListHeight(Frame parent, Slot slot, DocumentPath listPath)
    {
        double extra = 0;
        foreach (var value in Extras(parent, slot, listPath).Values)
            extra += value;
        return RowHeight + (slot.Count * RowHeight) + extra;
    }

    private Dictionary<int, double> Extras(Frame parent, Slot slot, DocumentPath listPath)
    {
        var extras = new Dictionary<int, double>();
        if (!HasInner(listPath))
            return extras;
        var index = EnsureOffsets(listPath, parent, slot);
        foreach (var entry in InnerEntries(listPath))
        {
            if (entry < 1 || entry > index.Offsets.Count)
                continue;
            var entryPath = listPath.WithIndex(entry);
            var frame = LoadEntry(slot, index, entry, entryPath);
            var extra = EntryHeight(frame, entryPath, slot, listPath) - RowHeight;
            if (extra > 0)
                extras[entry] = extra;
        }

        return extras;
    }

    private double EntryHeight(Frame entry, DocumentPath entryPath, Slot list, DocumentPath listPath)
    {
        var height = RowHeight;
        foreach (var column in list.Columns)
        {
            if (entry.FindKey(column.Key) is not Slot cell)
                continue;
            var path = entryPath.Field(cell.Name);
            height = Math.Max(height, FieldBodyHeight(entry, cell, path, ShownColumn(listPath, column.Name)));
        }

        return height;
    }

    private double WrappedHeight(DocumentPath path, List<TextPiece> pieces, double width)
    {
        var length = LengthOf(path, pieces);
        var lines = Math.Max(1, (length + Capacity(width) - 1) / Capacity(width));
        return lines * RowHeight;
    }

    private void EmitWrapped(
        List<SurfaceRow>? emit,
        Seek? seek,
        DocumentPath path,
        string name,
        List<TextPiece> pieces,
        double x,
        double y,
        double width,
        double clipRight,
        SurfaceKind kind,
        WidthAxis axis,
        DocumentPath table,
        string? column)
    {
        var length = LengthOf(path, pieces);
        var cols = Capacity(width);
        var lines = Math.Max(1, (length + cols - 1) / cols);
        Note(seek, path, kind, x, y, width);
        if (emit is null || lines == 0)
            return;
        var first = (int)Math.Max(0, Math.Floor((ViewportY - y) / RowHeight));
        var last = (int)Math.Min(lines, Math.Ceiling((ViewportY + ViewportHeight - y) / RowHeight));
        for (var line = first; line < last; line++)
        {
            var text = ExpandedText.Slice(_document, pieces, line * cols, cols, out _);
            Emit(
                emit, null, x, y + (line * RowHeight), width, RowHeight, clipRight,
                name, text, false, line == 0, Marks(path), path, line == 0 ? kind : SurfaceKind.ValueLine,
                true, false, axis, table, column, line * cols, true);
        }
    }

    private Frame LoadEntry(Slot slot, OffsetIndex index, int oneBased, DocumentPath entryPath)
    {
        var frame = LoadElement(index.Offsets[oneBased - 1], slot.ParentScope, entryPath);
        Normalize(frame);
        return frame;
    }

    private string WindowText(DocumentPath path, List<TextPiece> pieces, double width, out bool cutOff, out bool canGrow, out int start)
    {
        var capacity = Capacity(width);
        if (Marked is not null && Marked.Equals(path)
            && _chrome.TryGetValue(path, out var marked)
            && marked.HitOffset is int hit)
        {
            var length = LengthOf(path, pieces);
            start = length > capacity ? Place(hit, marked.MatchLength, length, capacity) : 0;
            marked.WindowStart = start;
            var text = ExpandedText.Slice(_document, pieces, start, capacity, out _);
            canGrow = length > capacity;
            cutOff = canGrow;
            return text;
        }

        start = _chrome.TryGetValue(path, out var chrome) ? chrome.WindowStart : 0;
        var window = ExpandedText.Slice(_document, pieces, start, capacity, out var more);
        canGrow = more || start > 0;
        cutOff = canGrow;
        return window;
    }

    // A record whose only field is a list is that list. The extra name row is not drawn.
    private bool TrySoleList(Slot slot, DocumentPath path, out Frame frame, out Slot list)
    {
        frame = null!;
        list = null!;
        if (slot.Kind != SlotKind.Record || slot.Closed is not null)
            return false;
        if (slot.OutlineCount == 0 || slot.OutlineCount > 1 || (slot.OutlineCount == 1 && !slot.OutlineSoleList))
            return false;
        frame = Opened(slot, path);
        if (frame.Slots.Count != 1)
            return false;
        var only = frame.Slots[0];
        if (only.Kind != SlotKind.List)
            return false;
        list = only;
        return true;
    }

    private static double ExpandLabelWidth(double width) => Math.Max(Advance, width - ExpandGutter);

    private static string ClipLabel(string text, double width, out bool cutOff)
    {
        var capacity = Capacity(width);
        if (text.Length <= capacity)
        {
            cutOff = false;
            return text;
        }

        cutOff = true;
        return text[..capacity];
    }

    private void Emit(
        List<SurfaceRow>? rows,
        Seek? seek,
        double x,
        double y,
        double width,
        double height,
        double clipRight,
        string name,
        string? value,
        bool canExpand,
        bool expanded,
        bool marked,
        DocumentPath path,
        SurfaceKind kind,
        bool canGrow,
        bool cutOff,
        WidthAxis axis,
        DocumentPath? table,
        string? column,
        int textStart = 0,
        bool scalarText = false)
    {
        Note(seek, path, kind, x, y, width);
        if (rows is null)
            return;
        if (x >= clipRight)
            return;
        var drawn = width;
        if (x + drawn > clipRight)
            drawn = clipRight - x;
        if (drawn <= 0 || !OnScreen(x, y, drawn, height))
            return;
        rows.Add(new SurfaceRow(
            x, y, drawn, height,
            name, value,
            canExpand, expanded, marked, path, kind,
            canGrow, null, null, cutOff, axis, table, column, textStart, scalarText));
    }

    private static void Note(Seek? seek, DocumentPath path, SurfaceKind kind, double x, double y, double width)
    {
        if (seek is null || seek.Found || !seek.Path.Equals(path))
            return;
        if (kind is not (SurfaceKind.Field or SurfaceKind.Entry or SurfaceKind.ValueLine or SurfaceKind.EntryIndex))
            return;
        seek.Found = true;
        seek.X = x;
        seek.Y = y;
        seek.Width = width;
    }

    private bool OnScreen(double x, double y, double width, double height) =>
        y < ViewportY + ViewportHeight && y + height > ViewportY
        && x < ViewportX + ViewportWidth && x + width > ViewportX;

    private int LengthOf(DocumentPath path, List<TextPiece> pieces)
    {
        if (_chrome.TryGetValue(path, out var chrome) && chrome.Length is int known)
            return known;
        var length = ExpandedText.Count(_document, pieces);
        _chrome.TryGetValue(path, out var existing);
        _chrome[path] = new Chrome
        {
            Length = length,
            FullValue = existing?.FullValue ?? false,
            WindowStart = existing?.WindowStart ?? 0,
            HitOffset = existing?.HitOffset,
            MatchLength = existing?.MatchLength ?? 0,
        };
        return length;
    }

    private bool Full(DocumentPath path) => _chrome.TryGetValue(path, out var chrome) && chrome.FullValue;

    private bool Marks(DocumentPath path) => Marked is not null && Marked.Equals(path);

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
