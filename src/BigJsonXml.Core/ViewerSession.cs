using BigJsonXml.Xml;

namespace BigJsonXml;

/// <summary>
/// Read-only session over one XML document. Callers read a record, a slice of list entries,
/// a scalar window, or a slice of a full value. The document is never returned as a tree or one string.
/// </summary>
public sealed partial class ViewerSession : IDisposable
{
    public const int ScalarWindowChars = 200;
    public const double RowHeight = 24;
    public const double NameWidth = 220;
    public const double ValueWidth = 720;
    public const int FullValueColumns = 100;
    public const int ScrollSurfaces = 1;

    private readonly XmlDocument _document;
    private readonly XmlParser _parser;
    private readonly DocumentTree _tree;
    private readonly IDisposable? _owned;
    private readonly HashSet<DocumentPath> _expanded = [];
    private readonly Dictionary<DocumentPath, OffsetIndex> _offsets = [];
    private readonly Dictionary<DocumentPath, Chrome> _chrome = [];
    private readonly List<Hit> _hits = [];
    private Searcher? _search;

    private ViewerSession(IByteSource source, IDisposable? owned)
    {
        _document = new XmlDocument(source);
        _parser = XmlParser.Create(_document);
        _tree = new DocumentTree(_document, documentElement: true);
        _owned = owned;
    }

    public ScanProgress Progress { get; private set; } = ScanProgress.Scanning;

    public ScanStop? Error { get; private set; }

    public DocumentPath? Marked { get; private set; }

    public IReadOnlyList<Hit> Hits => _hits;

    public double ViewportX { get; private set; }

    public double ViewportY { get; private set; }

    public double ViewportWidth { get; private set; } = 800;

    public double ViewportHeight { get; private set; } = 600;

    public static ViewerSession Open(IByteSource source) => new(source, source as IDisposable);

    public static ViewerSession Open(Stream stream) => new(new StreamByteSource(stream), stream);

    public void SetViewport(double x, double y, double width, double height)
    {
        ViewportX = Math.Max(0, x);
        ViewportY = Math.Max(0, y);
        ViewportWidth = Math.Max(1, width);
        ViewportHeight = Math.Max(1, height);
    }

    public void PumpAll()
    {
        for (var i = 0; i < 1_000_000; i++)
        {
            if (!Pump(2048))
                return;
        }

        throw new InvalidOperationException("The scan did not finish.");
    }

    public bool Pump(int budget)
    {
        var any = false;
        for (var i = 0; i < budget; i++)
        {
            var worked = false;
            if (Progress == ScanProgress.Scanning)
            {
                var ev = _parser.Read();
                if (ev.Kind != XmlEventKind.NeedMore)
                {
                    worked = true;
                    Apply(ev);
                }
            }

            if (_search is { Done: false })
            {
                var step = _search.Step();
                if (step == SearchStep.Work)
                    worked = true;
                else if (step == SearchStep.NeedMore && !worked)
                    break;
            }

            if (!worked)
                break;
            any = true;
        }

        return any;
    }

    public RecordView ReadRecord(DocumentPath path)
    {
        var frame = ResolveRecord(path);
        var fields = new List<FieldView>(frame.Slots.Count);
        foreach (var slot in frame.Slots)
            fields.Add(View(slot, frame.Path));
        return new RecordView(fields);
    }

    public ListView ReadList(DocumentPath path, int start, int count)
    {
        if (start < 0 || count < 0)
            throw new ArgumentOutOfRangeException(nameof(start));
        if (!_expanded.Contains(path))
            throw new InvalidOperationException("Expand the list before reading a slice.");
        var (parent, slot) = Locate(path);
        if (slot.Kind != SlotKind.List)
            throw new InvalidOperationException($"{path} is not a list.");
        var index = EnsureOffsets(path, parent, slot);
        var end = Math.Min(index.Offsets.Count, start + count);
        var entries = new List<ListEntryView>(Math.Max(0, end - start));
        var columns = ColumnNames(slot);
        for (var i = start; i < end; i++)
        {
            var entry = Enter(parent, slot, i + 1, path);
            entries.Add(new ListEntryView(i + 1, Cells(entry, slot.Columns, path.WithIndex(i + 1))));
        }

        return new ListView(columns, slot.Count, start, entries);
    }

    public FieldView ReadScalar(DocumentPath path)
    {
        var pieces = Pieces(path);
        return Scalar(path.Steps[^1].Name, pieces, path);
    }

    public string ReadFullValueSlice(DocumentPath path, int characterOffset, int characterCount)
    {
        if (characterOffset < 0 || characterCount < 0)
            throw new ArgumentOutOfRangeException(nameof(characterOffset));
        return ExpandedText.Slice(_document, Pieces(path), characterOffset, characterCount, out _);
    }

    public void Expand(DocumentPath path)
    {
        var (parent, slot) = Locate(path);
        if (slot.Kind == SlotKind.Scalar)
            throw new InvalidOperationException($"{path} has no embedded table.");
        _expanded.Add(path);
        if (slot.Kind == SlotKind.List)
            EnsureOffsets(path, parent, slot);
    }

    public void Collapse(DocumentPath path)
    {
        RemoveUnder(_expanded, path);
        RemoveUnderOffsets(path);
        var chrome = new List<DocumentPath>();
        foreach (var key in _chrome.Keys)
        {
            if (key.Equals(path) || IsUnder(key, path))
                chrome.Add(key);
        }

        foreach (var key in chrome)
            _chrome.Remove(key);
    }

    public void ExpandFullValue(DocumentPath path)
    {
        var pieces = Pieces(path);
        var length = ExpandedText.Count(_document, pieces);
        if (length <= ScalarWindowChars)
            return;
        _chrome[path] = new Chrome { FullValue = true, Length = length };
    }

    public void CollapseFullValue(DocumentPath path)
    {
        if (_chrome.TryGetValue(path, out var chrome))
        {
            chrome.FullValue = false;
            chrome.WindowStart = 0;
        }
    }

    public void Search(string query)
    {
        ArgumentNullException.ThrowIfNull(query);
        _hits.Clear();
        Marked = null;
        _search = new Searcher(_document, query, _hits);
    }

    public void Reveal(Hit hit)
    {
        ArgumentNullException.ThrowIfNull(hit);
        var steps = hit.Path.Steps;
        var walk = DocumentPath.Root;
        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            var last = i == steps.Count - 1;
            if (step.OneBasedIndex is int)
            {
                var listPath = walk.Field(step.Name);
                Expand(listPath);
                walk = listPath.WithIndex(step.OneBasedIndex.Value);
                if (last)
                    Mark(walk, hit);
                continue;
            }

            var field = walk.Field(step.Name);
            if (!last)
            {
                Expand(field);
                walk = field;
                continue;
            }

            Mark(field, hit);
        }

        ScrollIntoView(hit.Path);
    }

    public void Dispose() => _owned?.Dispose();

    private void Apply(XmlEvent ev)
    {
        if (ev.Kind == XmlEventKind.Error)
        {
            Error = new ScanStop(ev.Message, ev.ErrorByte, ev.Line, ev.Column);
            Progress = ScanProgress.Stopped;
            return;
        }

        if (ev.Kind == XmlEventKind.Done)
        {
            if (Progress == ScanProgress.Scanning)
                Progress = ScanProgress.Completed;
            return;
        }

        if (!_tree.Apply(ev, out var folded))
            return;
        var path = folded.Parent.Path.Field(folded.Slot.Name);
        if (folded.Promoted)
        {
            _expanded.Remove(path);
            _offsets.Remove(path);
            return;
        }

        if (folded.Slot.Kind == SlotKind.List
            && _offsets.TryGetValue(path, out var index)
            && folded.Child.StartByte >= index.Watermark)
        {
            index.Offsets.Add(folded.Child.StartByte);
            index.Watermark = folded.Child.EndByte;
        }
    }

    private Frame ResolveRecord(DocumentPath path)
    {
        if (path.Steps.Count == 0)
            return RootFrame();
        var (parent, slot) = Locate(path);
        if (slot.Kind == SlotKind.Scalar)
            throw new InvalidOperationException($"{path} is a scalar.");
        return FindLive(slot.StartByte) ?? LoadElement(slot.StartByte, slot.ParentScope, path);
    }

    private (Frame Parent, Slot Slot) Locate(DocumentPath path)
    {
        var frame = RootFrame();
        for (var i = 0; i < path.Steps.Count; i++)
        {
            var step = path.Steps[i];
            var last = i == path.Steps.Count - 1;
            if (!last && step.OneBasedIndex is int index)
            {
                var list = Require(frame, step.Name);
                var listPath = frame.Path.Field(list.Name);
                frame = Enter(frame, list, index, listPath);
                continue;
            }

            if (last)
                return (frame, Require(frame, step.Name));

            var record = Require(frame, step.Name);
            var recordPath = frame.Path.Field(record.Name);
            frame = FindLive(record.StartByte) ?? LoadElement(record.StartByte, record.ParentScope, recordPath);
        }

        throw new InvalidOperationException("The path does not name a field.");
    }

    private Frame Enter(Frame parent, Slot list, int index, DocumentPath listPath)
    {
        var offsets = EnsureOffsets(listPath, parent, list);
        if (index < 1 || index > offsets.Offsets.Count)
            throw new ArgumentOutOfRangeException(nameof(index), "That list entry is not available.");
        var frame = LoadElement(offsets.Offsets[index - 1], list.ParentScope, listPath.WithIndex(index));
        Normalize(frame);
        return frame;
    }

    private Frame LoadElement(long offset, NsScope scope, DocumentPath path)
    {
        var tree = new DocumentTree(_document, documentElement: false);
        return tree.Load(XmlParser.Fragment(_document, scope, offset, long.MaxValue), path);
    }

    private OffsetIndex EnsureOffsets(DocumentPath path, Frame parent, Slot slot)
    {
        if (_offsets.TryGetValue(path, out var existing))
            return existing;
        var index = new OffsetIndex();
        var limit = parent.ContentEnd >= 0 ? parent.ContentEnd : parent.StableByte;
        if (limit > parent.ContentByte)
        {
            var parser = XmlParser.Fragment(_document, parent.Scope, parent.ContentByte, limit);
            var depth = 0;
            while (true)
            {
                var ev = parser.Read();
                if (ev.Kind is XmlEventKind.Done or XmlEventKind.NeedMore or XmlEventKind.Error)
                    break;
                if (ev.Kind == XmlEventKind.Start)
                {
                    if (depth == 0 && ev.Local == slot.Key.Local && ev.Ns == slot.Key.Ns)
                        index.Offsets.Add(ev.ByteStart);
                    if (!ev.Empty)
                        depth++;
                }
                else if (ev.Kind == XmlEventKind.End && depth > 0)
                {
                    depth--;
                }
            }
        }

        index.Watermark = limit;
        _offsets[path] = index;
        return index;
    }

    private List<TextPiece> Pieces(DocumentPath path)
    {
        var (_, slot) = Locate(path);
        if (slot.Kind != SlotKind.Scalar)
            throw new InvalidOperationException($"{path} is not a scalar.");
        return slot.Text;
    }

    private void Mark(DocumentPath path, Hit hit)
    {
        Marked = path;
        var pieces = Pieces(path);
        var length = ExpandedText.Count(_document, pieces);
        var start = 0;
        if (length > ScalarWindowChars)
        {
            start = hit.CharacterOffset - (ScalarWindowChars / 2);
            if (start < 0)
                start = 0;
            var last = length - ScalarWindowChars;
            if (start > last)
                start = Math.Max(0, last);
        }

        _chrome[path] = new Chrome { WindowStart = start, Length = length };
    }

    private FieldView View(Slot slot, DocumentPath owner)
    {
        var path = owner.Field(slot.Name);
        var marked = Marked is not null && Marked.Equals(path);
        var expanded = _expanded.Contains(path);
        if (slot.Kind == SlotKind.List)
        {
            return new FieldView(
                slot.Name, FieldKind.List, null, true, expanded,
                $"{slot.Name}[{slot.Count}]", slot.Count,
                false, false, false, 0, marked);
        }

        if (slot.Kind == SlotKind.Record)
        {
            return new FieldView(
                slot.Name, FieldKind.Record, null, true, expanded,
                null, null, false, false, false, 0, marked);
        }

        return Scalar(slot.Name, slot.Text, path);
    }

    private List<FieldView> Cells(Frame entry, List<Column> columns, DocumentPath entryPath)
    {
        var cells = new List<FieldView>(columns.Count);
        foreach (var column in columns)
        {
            if (!entry.ByKey.TryGetValue(column.Key, out var slot))
            {
                cells.Add(new FieldView(column.Name, FieldKind.Scalar, "", false, false, null, null, false, false, false, 0, false));
                continue;
            }

            cells.Add(View(slot, entryPath));
        }

        return cells;
    }

    private FieldView Scalar(string name, List<TextPiece> pieces, DocumentPath path)
    {
        _chrome.TryGetValue(path, out var chrome);
        var start = chrome?.WindowStart ?? 0;
        var full = chrome?.FullValue ?? false;
        var window = ExpandedText.Slice(_document, pieces, start, ScalarWindowChars, out var more);
        var canGrow = start > 0 || more;
        var marked = Marked is not null && Marked.Equals(path);
        return new FieldView(
            name, FieldKind.Scalar, window, false, false, null, null,
            canGrow, full, canGrow, start, marked);
    }

    private static void Normalize(Frame frame)
    {
        if (frame.Text.Count == 0 || frame.ByKey.ContainsKey(FieldKey.Text))
            return;
        var slot = new Slot
        {
            Key = FieldKey.Text,
            Name = "#text",
            Kind = SlotKind.Scalar,
            Text = frame.Text,
        };
        frame.Slots.Add(slot);
        frame.ByKey[slot.Key] = slot;
        frame.ByName.TryAdd(slot.Name, slot);
    }

    private Frame RootFrame() =>
        _tree.Root ?? throw new InvalidOperationException("The document element is not available yet.");

    private Frame? FindLive(long startByte)
    {
        foreach (var frame in _tree.Stack)
        {
            if (frame.StartByte == startByte)
                return frame;
        }

        return null;
    }

    private static Slot Require(Frame frame, string name)
    {
        if (frame.ByName.TryGetValue(name, out var slot))
            return slot;
        throw new InvalidOperationException($"Field '{name}' was not found.");
    }

    private static List<string> ColumnNames(Slot slot)
    {
        var names = new List<string>(slot.Columns.Count);
        foreach (var column in slot.Columns)
            names.Add(column.Name);
        return names;
    }

    private void RemoveUnderOffsets(DocumentPath path)
    {
        var doomed = new List<DocumentPath>();
        foreach (var key in _offsets.Keys)
        {
            if (key.Equals(path) || IsUnder(key, path))
                doomed.Add(key);
        }

        foreach (var key in doomed)
            _offsets.Remove(key);
    }

    private static void RemoveUnder(HashSet<DocumentPath> set, DocumentPath path)
    {
        var doomed = new List<DocumentPath>();
        foreach (var key in set)
        {
            if (key.Equals(path) || IsUnder(key, path))
                doomed.Add(key);
        }

        foreach (var key in doomed)
            set.Remove(key);
    }

    private static bool IsUnder(DocumentPath path, DocumentPath ancestor)
    {
        if (path.Steps.Count <= ancestor.Steps.Count)
            return false;
        for (var i = 0; i < ancestor.Steps.Count; i++)
        {
            if (path.Steps[i] != ancestor.Steps[i])
                return false;
        }

        return true;
    }

    private sealed class Chrome
    {
        public int WindowStart { get; set; }
        public bool FullValue { get; set; }
        public int? Length { get; set; }
    }
}
