using BigJsonXml.Xml;

namespace BigJsonXml;

public sealed partial class ViewerSession
{
    private const int FitSliceMilliseconds = 32;
    private static readonly IReadOnlyList<Nest> NoNests = [];
    private FitRun? _fit;

    public void Fit(WidthAxis axis, DocumentPath table, string? column = null)
    {
        BeginFit(axis, table, column);
        while (AdvanceFit() == FitAdvance.Working)
        {
        }
    }

    public void BeginFit(WidthAxis axis, DocumentPath table, string? column)
    {
        _fit = null;
        if (axis == WidthAxis.None)
            return;
        if (axis == WidthAxis.ListColumn && string.IsNullOrEmpty(column))
            return;
        _fit = new FitRun(axis, table, column);
    }

    public FitAdvance AdvanceFit()
    {
        var run = _fit;
        if (run is null)
            return FitAdvance.Idle;
        var start = Environment.TickCount64;
        do
        {
            StepFit(run);
        }
        while (!run.Finished && Environment.TickCount64 - start < FitSliceMilliseconds);

        if (!run.Finished)
            return FitAdvance.Working;
        _fit = null;
        ApplyFit(run);
        return FitAdvance.Applied;
    }

    public void CancelFit() => _fit = null;

    private void StepFit(FitRun run)
    {
        if (run.Child is not null)
        {
            StepFit(run.Child);
            if (!run.Child.Finished)
                return;
            if (run.Child.Width >= MinWidth)
                run.Width = Math.Max(run.Width, run.Child.Width + run.Child.HostFixed + ExpandGutter);
            run.Child = null;
            run.NestAt++;
        }

        if (run.Nests is { Count: > 0 } && run.NestAt < run.Nests.Count)
        {
            var nest = run.Nests[run.NestAt];
            if (nest.Axis == WidthAxis.None)
            {
                run.Width = Math.Max(run.Width, nest.FixedWidth + ExpandGutter);
                run.NestAt++;
                return;
            }

            run.Child = new FitRun(nest.Axis, nest.Table, nest.Column) { HostFixed = nest.FixedWidth };
            return;
        }

        if (run.Axis != WidthAxis.ListColumn)
        {
            if (!run.Ready)
            {
                run.Width = MeasureRecord(run);
                run.Ready = true;
                if (run.Axis == WidthAxis.RecordValue)
                    run.Nests = NestsUnderRecord(run.Table);
                if (run.Nests is { Count: > 0 })
                    return;
            }

            run.Finished = true;
            return;
        }

        if (!run.Ready)
        {
            run.Ready = true;
            if (!PrepareList(run) || run.Count == 0 || run.Width >= FitCeiling)
                run.Finished = true;
            return;
        }

        if (!run.EntriesDone)
        {
            if (!TryMeasureFast(run, run.Offsets.Offsets[run.Next], run.ColumnSlot.Key))
                MeasureEntry(run, run.Next);
            run.Next++;
            if (run.Next < run.Count && run.Width < FitCeiling)
                return;
            run.EntriesDone = true;
            if (run.Column is not null)
                run.Nests = NestsUnderColumn(run.Table, run.Column);
            if (run.Nests is not { Count: > 0 })
                run.Finished = true;
            return;
        }

        run.Finished = true;
    }

    // An open table ends on this edge. Fitting the edge sizes that table's flexible side.
    private IReadOnlyList<Nest> NestsUnderRecord(DocumentPath record)
    {
        List<Nest>? nests = null;
        foreach (var path in _expanded)
        {
            if (!IsDirectField(record, path))
                continue;
            nests ??= [];
            TryAddNest(nests, path);
        }

        return nests ?? NoNests;
    }

    private IReadOnlyList<Nest> NestsUnderColumn(DocumentPath list, string column)
    {
        List<Nest>? nests = null;
        foreach (var path in _expanded)
        {
            if (!IsColumnCell(list, column, path))
                continue;
            nests ??= [];
            TryAddNest(nests, path);
        }

        return nests ?? NoNests;
    }

    private void TryAddNest(List<Nest> nests, DocumentPath cell)
    {
        try
        {
            AddNest(nests, cell);
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void AddNest(List<Nest> nests, DocumentPath cell)
    {
        var (_, slot) = Locate(cell);
        if (slot.Kind == SlotKind.List)
        {
            AddListNest(nests, cell, slot);
            return;
        }

        if (slot.Kind == SlotKind.Record && TrySoleList(slot, cell, out _, out var sole))
        {
            AddListNest(nests, cell.Field(sole.Name), sole);
            return;
        }

        if (slot.Kind == SlotKind.Record)
            nests.Add(new Nest(WidthAxis.RecordValue, cell, null, OwnName(cell)));
    }

    private void AddListNest(List<Nest> nests, DocumentPath list, Slot slot)
    {
        if (slot.Columns.Count == 0)
        {
            nests.Add(new Nest(WidthAxis.None, list, null, IndexTrackWidth(slot.Count)));
            return;
        }

        var fixedWidth = IndexTrackWidth(slot.Count);
        for (var i = 0; i < slot.Columns.Count - 1; i++)
            fixedWidth += OwnColumn(list, slot.Columns[i].Name);
        nests.Add(new Nest(WidthAxis.ListColumn, list, slot.Columns[^1].Name, fixedWidth));
    }

    private double MeasureRecord(FitRun run)
    {
        Frame frame;
        try
        {
            frame = ResolveRecord(run.Table);
        }
        catch (InvalidOperationException)
        {
            return 0;
        }

        var width = MinWidth;
        foreach (var slot in frame.Slots)
        {
            if (run.Axis == WidthAxis.RecordName)
            {
                width = Math.Max(width, CharsWidth(slot.Name.Length, gutter: false));
                continue;
            }

            var field = frame.Path.Field(slot.Name);
            if (slot.Kind == SlotKind.Scalar)
                width = Math.Max(width, CharsWidth(TextChars(slot.Text), gutter: false));
            else
                width = Math.Max(width, CharsWidth(LabelChars(slot, field), gutter: true));
        }

        return width;
    }

    private bool PrepareList(FitRun run)
    {
        if (run.Column is null)
            return false;
        Frame parent;
        Slot slot;
        try
        {
            (parent, slot) = Locate(run.Table);
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        if (slot.Kind != SlotKind.List)
            return false;
        Column? column = null;
        foreach (var candidate in slot.Columns)
        {
            if (candidate.Name == run.Column)
            {
                column = candidate;
                break;
            }
        }

        if (column is null)
            return false;
        run.List = slot;
        run.ColumnSlot = column;
        run.Offsets = EnsureOffsets(run.Table, parent, slot);
        run.Count = run.Offsets.Offsets.Count;
        run.Width = CharsWidth(column.Name.Length, gutter: false);
        return true;
    }

    private void MeasureEntry(FitRun run, int index)
    {
        var entryPath = run.Table.WithIndex(index + 1);
        Frame frame;
        try
        {
            frame = ReadFrame(run.Offsets.Offsets[index], run.List.ParentScope, entryPath);
        }
        catch (InvalidOperationException)
        {
            return;
        }

        var cell = frame.FindKey(run.ColumnSlot.Key);
        if (cell is null)
            return;
        var path = entryPath.Field(run.ColumnSlot.Name);
        if (cell.Kind == SlotKind.Scalar)
            run.Width = Math.Max(run.Width, CharsWidth(TextChars(cell.Text), gutter: false));
        else
            run.Width = Math.Max(run.Width, CharsWidth(LabelChars(cell, path), gutter: true));
    }

    private int LabelChars(Slot cell, DocumentPath path)
    {
        string label;
        if (cell.Kind == SlotKind.List)
            label = $"{cell.Name}[{cell.Count}]";
        else if (TrySoleListFresh(cell, path, out var sole))
            label = $"{sole.Name}[{sole.Count}]";
        else
            label = cell.Closed ?? cell.Name;
        return label.Length;
    }

    private bool TrySoleListFresh(Slot slot, DocumentPath path, out Slot list)
    {
        list = null!;
        if (slot.Kind != SlotKind.Record || slot.Closed is not null)
            return false;
        if (slot.OutlineCount == 0 || slot.OutlineCount > 1 || (slot.OutlineCount == 1 && !slot.OutlineSoleList))
            return false;
        Frame frame;
        try
        {
            frame = ReadFrame(slot.StartByte, slot.ParentScope, path);
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        if (frame.Slots.Count != 1 || frame.Slots[0].Kind != SlotKind.List)
            return false;
        list = frame.Slots[0];
        return true;
    }

    private int TextChars(IReadOnlyList<TextPiece> pieces)
    {
        var cursor = new ExpandedCursor(_document, pieces);
        var count = 0;
        while (count < FitCharacterCap && cursor.TryRead(out _))
            count++;
        return count;
    }

    private static double CharsWidth(int chars, bool gutter)
    {
        if (chars > FitCharacterCap)
            chars = FitCharacterCap;
        var width = chars * Advance;
        if (gutter)
            width += ExpandGutter;
        return Math.Max(MinWidth, width);
    }

    private void ApplyFit(FitRun run)
    {
        if (run.Width < MinWidth)
            return;
        switch (run.Axis)
        {
            case WidthAxis.RecordName:
                SetRecordNameWidth(run.Table, run.Width);
                break;
            case WidthAxis.RecordValue:
                SetRecordValueWidth(run.Table, run.Width);
                break;
            case WidthAxis.ListColumn when run.Column is not null:
                SetColumnWidth(run.Table, run.Column, run.Width);
                break;
        }
    }

    private static double FitCeiling => FitCharacterCap * Advance + ExpandGutter;

    private sealed class FitRun(WidthAxis axis, DocumentPath table, string? column)
    {
        public WidthAxis Axis { get; } = axis;
        public DocumentPath Table { get; } = table;
        public string? Column { get; } = column;
        public double Width { get; set; }
        public int Next { get; set; }
        public int Count { get; set; }
        public bool Ready { get; set; }
        public bool Finished { get; set; }
        public bool EntriesDone { get; set; }
        public double HostFixed { get; set; }
        public int NestAt { get; set; }
        public FitRun? Child { get; set; }
        public IReadOnlyList<Nest>? Nests { get; set; }
        public Slot List { get; set; } = null!;
        public Column ColumnSlot { get; set; } = null!;
        public OffsetIndex Offsets { get; set; } = null!;
    }

    private readonly struct Nest(WidthAxis axis, DocumentPath table, string? column, double fixedWidth)
    {
        public WidthAxis Axis { get; } = axis;
        public DocumentPath Table { get; } = table;
        public string? Column { get; } = column;
        public double FixedWidth { get; } = fixedWidth;
    }
}
