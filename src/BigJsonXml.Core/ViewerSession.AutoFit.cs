using BigJsonXml.Xml;

namespace BigJsonXml;

// Every table that is drawn gets its columns fitted once, from a small sample, after
// the scan has finished and off the drawing path (ADR 0057).
public sealed partial class ViewerSession
{
    // Cells one automatic fit reads, all columns together. The entry count follows from it.
    public const int AutoFitCells = 16_384;
    public const int AutoFitMaxEntries = 512;
    public const int AutoFitMinEntries = 16;

    private readonly HashSet<DocumentPath> _autoSeen = [];
    private readonly Queue<DocumentPath> _autoQueue = new();
    private readonly HashSet<(DocumentPath List, string Column)> _setByHand = [];
    private AutoRun? _auto;

    // Off unless the viewer asks for it, so a session read by code keeps its widths.
    public bool AutoFitColumns { get; set; }

    public bool AutoFitPending => AutoFitColumns && (_auto is not null || _autoQueue.Count > 0);

    // Entries the last automatic fit read.
    internal int LastAutoFitReads { get; private set; }

    // Runs queued fits for up to the slice. True when a width changed.
    public bool AdvanceAutoFit(int sliceMilliseconds)
    {
        if (!AutoFitPending || Progress == ScanProgress.Scanning)
            return false;
        var start = Environment.TickCount64;
        var applied = false;
        do
        {
            if (_auto is null)
            {
                if (_autoQueue.Count == 0)
                    break;
                _auto = PrepareAuto(_autoQueue.Dequeue());
                continue;
            }

            if (StepAuto(_auto))
                continue;
            applied |= ApplyAuto(_auto);
            _auto = null;
        }
        while (Environment.TickCount64 - start < sliceMilliseconds);

        return applied;
    }

    private void NoteDrawnList(DocumentPath listPath)
    {
        if (AutoFitColumns && _autoSeen.Add(listPath))
            _autoQueue.Enqueue(listPath);
    }

    private AutoRun? PrepareAuto(DocumentPath listPath)
    {
        Frame parent;
        Slot slot;
        try
        {
            (parent, slot) = Locate(listPath);
        }
        catch (InvalidOperationException)
        {
            return null;
        }

        if (slot.Kind != SlotKind.List || slot.Columns.Count == 0)
            return null;
        var columns = slot.Columns.ToArray();
        var offsets = EnsureOffsets(listPath, parent, slot);
        var count = offsets.Offsets.Count;
        var entries = Math.Clamp(AutoFitCells / columns.Length, AutoFitMinEntries, AutoFitMaxEntries);
        var run = new AutoRun(listPath, slot, columns, offsets, Math.Min(count, entries), count);
        return run;
    }

    // One entry. False when the sample is done.
    private bool StepAuto(AutoRun run)
    {
        if (run.Next >= run.Sample)
            return false;
        var index = AutoIndex(run.Next, run.Sample, run.Count);
        run.Next++;
        var entryPath = run.List.WithIndex(index + 1);
        Frame frame;
        try
        {
            frame = ReadFrame(run.Offsets.Offsets[index], run.Slot.ParentScope, entryPath);
        }
        catch (InvalidOperationException)
        {
            return true;
        }

        for (var i = 0; i < run.Columns.Length; i++)
        {
            if (frame.FindKey(run.Columns[i].Key) is Slot cell)
                MeasureCell(run.Cells[i], cell, entryPath.Field(run.Columns[i].Name));
        }

        return true;
    }

    // The sample is read in runs of neighbouring entries spread over the list, the first
    // run at the top, so a run shares one read-ahead window.
    private static int AutoIndex(int step, int sample, int count)
    {
        if (sample >= count)
            return step;
        const int Run = 32;
        var runs = (sample + Run - 1) / Run;
        var at = step / Run;
        var start = runs <= 1 ? 0 : (int)((long)at * (count - Run) / (runs - 1));
        return Math.Min(count - 1, start + (step % Run));
    }

    private bool ApplyAuto(AutoRun run)
    {
        LastAutoFitReads = run.Next;
        var widths = new List<(string Name, double Width)>();
        for (var i = 0; i < run.Columns.Length; i++)
        {
            var name = run.Columns[i].Name;
            if (_setByHand.Contains((run.List, name)))
                continue;
            var header = Math.Min(FitColumnWidth, CharsWidth(name.Length, gutter: false));
            widths.Add((name, ListWidth(header, run.Cells[i])));
        }

        return widths.Count > 0 && (ApplyAllAtOnce(run.List, widths) ?? ApplyOneByOne(run.List, widths));
    }

    // A table with nothing open inside it takes every width in one step, so a table of a
    // thousand columns does not re-measure its hosts a thousand times. Null when the
    // table holds an open cell or a host would have to shrink past another open table.
    private bool? ApplyAllAtOnce(DocumentPath list, List<(string Name, double Width)> widths)
    {
        if (HasInner(list) || !TryList(list, out _))
            return null;
        double delta = 0;
        foreach (var (name, width) in widths)
            delta += Math.Max(MinWidth, width) - OwnColumn(list, name);
        if (delta < 0 && Math.Abs(ClampDelta(list, delta, -delta) - delta) > 0.01)
            return null;
        var shots = Snapshot(list);
        var changed = false;
        foreach (var (name, width) in widths)
        {
            var before = OwnColumn(list, name);
            Adopt(new Host(list, record: false, column: name), Math.Max(MinWidth, width));
            changed |= Math.Abs(OwnColumn(list, name) - before) > 0.01;
        }

        ApplySnapshots(shots, delta, list);
        return changed;
    }

    private bool ApplyOneByOne(DocumentPath list, List<(string Name, double Width)> widths)
    {
        var changed = false;
        foreach (var (name, width) in widths)
        {
            var before = OwnColumn(list, name);
            ApplyColumnWidth(list, name, width);
            changed |= Math.Abs(OwnColumn(list, name) - before) > 0.01;
        }

        return changed;
    }

    private sealed class AutoRun(DocumentPath list, Slot slot, Column[] columns, OffsetIndex offsets, int sample, int count)
    {
        public DocumentPath List { get; } = list;
        public Slot Slot { get; } = slot;
        public Column[] Columns { get; } = columns;
        public OffsetIndex Offsets { get; } = offsets;
        public int Sample { get; } = sample;
        public int Count { get; } = count;
        public int Next { get; set; }
        public List<double>[] Cells { get; } = [.. columns.Select(_ => new List<double>())];
    }
}
