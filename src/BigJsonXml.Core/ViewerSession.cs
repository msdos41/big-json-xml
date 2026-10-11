using BigJsonXml.Json;
using BigJsonXml.Xml;

namespace BigJsonXml;

/// <summary>
/// Read-only session over one document. Callers read a record, a slice of list entries,
/// a scalar window, or a slice of a full value. The document is never returned as a tree or one string.
/// </summary>
public sealed partial class ViewerSession : IDisposable
{
    public const double Advance = 8;
    public const double DefaultWidth = 200;
    public const double MinWidth = 32;
    public const int FitCharacterCap = 500;
    public const double RowHeight = 24;
    public const double ExpandButtonInset = 10;
    public const double ExpandButtonSize = 12;
    public const double ExpandGutter = ExpandButtonInset + ExpandButtonSize + 2;
    public const int ScrollSurfaces = 1;

    private enum Grammar
    {
        Unknown,
        Xml,
        Json,
        Refused,
    }

    private readonly XmlDocument _document;
    private readonly DocumentTree _tree;
    private readonly IDisposable? _owned;
    private readonly HashSet<DocumentPath> _expanded = [];
    private readonly Dictionary<OffsetKey, OffsetIndex> _offsets = [];
    private readonly Dictionary<DocumentPath, Chrome> _chrome = [];
    private readonly Dictionary<long, LinkedListNode<LoadedFrame>> _loaded = [];
    private readonly LinkedList<LoadedFrame> _loadedOrder = [];
    private readonly List<Hit> _hits = [];
    private XmlParser? _xml;
    private JsonParser? _json;
    private JsonTree? _jsonTree;
    private Grammar _grammar;
    private IScanSearch? _search;

    private ViewerSession(IByteSource source, IDisposable? owned)
    {
        _document = new XmlDocument(source);
        _tree = new DocumentTree(_document, documentElement: true);
        _owned = owned;
    }

    public ScanProgress Progress { get; private set; } = ScanProgress.Scanning;

    public ScanStop? Error { get; private set; }

    public long BytesReached
    {
        get
        {
            var reached = _grammar switch
            {
                Grammar.Xml => _xml!.FileByte,
                Grammar.Json => _json!.FileByte,
                _ => 0,
            };
            var length = _document.Source.Length;
            if (reached < 0)
                return 0;
            return reached > length ? length : reached;
        }
    }

    public long DocumentLength => _document.Source.Length;

    public bool LengthIsFixed => _document.Source.IsComplete;

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
        var start = Environment.TickCount64;
        var any = false;
        for (var i = 0; i < budget; i++)
        {
            if (any && Environment.TickCount64 - start >= 12)
                break;
            var worked = false;
            if (Progress == ScanProgress.Scanning)
            {
                if (_grammar == Grammar.Unknown && Detect() && _grammar == Grammar.Refused)
                    worked = true;
                if (Progress == ScanProgress.Scanning && _grammar == Grammar.Xml)
                {
                    var ev = _xml!.Read();
                    if (ev.Kind != XmlEventKind.NeedMore)
                    {
                        worked = true;
                        Apply(ev);
                    }
                }
                else if (Progress == ScanProgress.Scanning && _grammar == Grammar.Json)
                {
                    var ev = _json!.Read();
                    if (ev.Kind != JsonEventKind.NeedMore)
                    {
                        worked = true;
                        if (ev.Kind != JsonEventKind.Continue)
                            ApplyJson(ev);
                    }
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

    public int ScalarLength(DocumentPath path) => LengthOf(path, Pieces(path));

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
        var chrome = new List<DocumentPath>();
        foreach (var key in _chrome.Keys)
        {
            if (key.Equals(path) || IsUnder(key, path))
                chrome.Add(key);
        }

        foreach (var key in chrome)
            _chrome.Remove(key);
        ReleaseUnusedPins();
    }

    public void ExpandFullValue(DocumentPath path)
    {
        var pieces = Pieces(path);
        var length = ExpandedText.Count(_document, pieces);
        if (length <= Capacity(ScalarWidth(path)))
            return;
        _chrome.TryGetValue(path, out var existing);
        _chrome[path] = new Chrome
        {
            FullValue = true,
            Length = length,
            WindowStart = existing?.WindowStart ?? 0,
            HitOffset = existing?.HitOffset,
            MatchLength = existing?.MatchLength ?? 0,
        };
    }

    public void CollapseFullValue(DocumentPath path)
    {
        if (!_chrome.TryGetValue(path, out var chrome))
            return;
        chrome.FullValue = false;
        if (Marked is null || !Marked.Equals(path))
            chrome.WindowStart = 0;
    }

    public void Search(TextSearch search)
    {
        ArgumentNullException.ThrowIfNull(search);
        ClearMark();
        _hits.Clear();
        if (search.FindWhat.Length == 0)
        {
            _search = null;
            return;
        }

        if (_grammar == Grammar.Unknown)
            Detect();
        _search = _grammar switch
        {
            Grammar.Json => new JsonSearcher(_document, search, _hits),
            Grammar.Xml => new Searcher(_document, search, _hits),
            _ => null,
        };
    }

    public string FormatPath(DocumentPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (_grammar == Grammar.Json)
            return FormatJsonPath(path);
        var builder = new System.Text.StringBuilder();
        builder.Append('/');
        builder.Append(_tree.Root?.Name ?? "");
        foreach (var step in path.Steps)
        {
            builder.Append('/');
            builder.Append(step.Name);
            if (step.OneBasedIndex is int index)
            {
                builder.Append('[');
                builder.Append(index.ToString(System.Globalization.CultureInfo.InvariantCulture));
                builder.Append(']');
            }
        }

        return builder.ToString();
    }

    public HitText ReadHitText(Hit hit, int characterWidth)
    {
        ArgumentNullException.ThrowIfNull(hit);
        if (characterWidth < 1)
            characterWidth = 1;
        if (hit.OnName)
            return NameHitText(hit.NameText, hit.CharacterOffset, hit.MatchLength, characterWidth);
        var pieces = Pieces(hit.Path);
        var length = ExpandedText.Count(_document, pieces);
        if (length <= characterWidth)
        {
            var whole = ExpandedText.Slice(_document, pieces, 0, length, out _);
            return ClipMark(whole, hit.CharacterOffset, hit.MatchLength);
        }

        var start = Place(hit.CharacterOffset, hit.MatchLength, length, characterWidth);
        var text = ExpandedText.Slice(_document, pieces, start, characterWidth, out _);
        return ClipMark(text, hit.CharacterOffset - start, hit.MatchLength);
    }

    private static HitText ClipMark(string text, int mark, int length)
    {
        if (mark < 0)
        {
            length += mark;
            mark = 0;
        }

        if (mark > text.Length)
            mark = text.Length;
        if (length < 0)
            length = 0;
        if (mark + length > text.Length)
            length = text.Length - mark;
        return new HitText(text, mark, length);
    }

    private static HitText NameHitText(string name, int offset, int matchLength, int characterWidth)
    {
        if (name.Length <= characterWidth)
            return ClipMark(name, offset, matchLength);
        var start = Place(offset, matchLength, name.Length, characterWidth);
        var count = Math.Min(characterWidth, name.Length - start);
        return ClipMark(name.Substring(start, count), offset - start, matchLength);
    }

    public void ClearMark()
    {
        if (Marked is DocumentPath path && _chrome.TryGetValue(path, out var chrome))
        {
            chrome.WindowStart = 0;
            chrome.HitOffset = null;
        }

        Marked = null;
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

    private bool Detect()
    {
        var look = JsonSniff.Look(_document.Source);
        switch (look.Kind)
        {
            case JsonSniff.Kind.NeedMore:
                return false;
            case JsonSniff.Kind.Xml:
                _grammar = Grammar.Xml;
                _xml = XmlParser.Create(_document);
                return true;
            case JsonSniff.Kind.Json:
                _grammar = Grammar.Json;
                _json = JsonParser.Document(_document);
                _jsonTree = new JsonTree(DocumentPath.Root, _tree);
                return true;
            default:
                _grammar = Grammar.Refused;
                Error = new ScanStop("The document has no root element.", 0, look.Line, look.Column);
                Progress = ScanProgress.Stopped;
                return true;
        }
    }

    private void ApplyJson(JsonEvent ev)
    {
        if (ev.Kind == JsonEventKind.Error)
        {
            Error = new ScanStop(ev.Message, ev.ErrorByte, ev.Line, ev.Column);
            Progress = ScanProgress.Stopped;
            return;
        }

        if (ev.Kind == JsonEventKind.Refuse)
        {
            _tree.ClearRoot();
            _offsets.Clear();
            _expanded.Clear();
            Error = new ScanStop(ev.Message, ev.ErrorByte, ev.Line, ev.Column);
            Progress = ScanProgress.Stopped;
            return;
        }

        if (ev.Kind == JsonEventKind.Done)
        {
            if (Progress == ScanProgress.Scanning)
                Progress = ScanProgress.Completed;
            return;
        }

        var note = _jsonTree!.Apply(ev);
        if (note.Stop)
        {
            Error = new ScanStop(note.Message, note.Byte, note.Line, note.Column);
            Progress = ScanProgress.Stopped;
            return;
        }

        if (note.HasEntry && note.Path is DocumentPath path && note.Slot is Slot slot)
        {
            RememberEntry(path, note.EntryStart);
            DrainJson(slot, path);
        }
    }

    private void RememberEntry(DocumentPath path, long start)
    {
        if (_offsets.TryGetValue(new OffsetKey(0, path), out var index) && start >= index.Watermark)
        {
            index.Offsets.Add(start);
            index.Watermark = start + 1;
        }
    }

    private void DrainJson(Slot slot, DocumentPath path)
    {
        var stamp = System.Diagnostics.Stopwatch.GetTimestamp();
        var limit = System.Diagnostics.Stopwatch.Frequency / 80;
        var deadline = stamp + limit;
        while (System.Diagnostics.Stopwatch.GetTimestamp() - stamp < limit)
        {
            var take = _json!.TryTakeArrayItem(deadline, out var start, out var stopByte, out var stopLine, out var stopColumn);
            if (take is JsonParser.ItemTake.None or JsonParser.ItemTake.Defer)
                return;
            if (take == JsonParser.ItemTake.Repeated)
            {
                Error = new ScanStop("A repeated key stops the scan.", stopByte, stopLine, stopColumn);
                Progress = ScanProgress.Stopped;
                return;
            }

            if (_json.FlatSameShape)
                _jsonTree!.AddSame(slot, start);
            else
                _jsonTree!.AddDrained(slot, start, _json.DirectKeys, take == JsonParser.ItemTake.Value);
            RememberEntry(path, start);
        }
    }

    private static string FormatJsonPath(DocumentPath path)
    {
        var builder = new System.Text.StringBuilder();
        builder.Append('/');
        for (var i = 0; i < path.Steps.Count; i++)
        {
            if (i > 0)
                builder.Append('/');
            var step = path.Steps[i];
            builder.Append(step.Name);
            if (step.OneBasedIndex is int index)
            {
                builder.Append('[');
                builder.Append(index.ToString(System.Globalization.CultureInfo.InvariantCulture));
                builder.Append(']');
            }
        }

        return builder.ToString();
    }

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
        var path = folded.Slot.Path ?? folded.Parent.Path.Field(folded.Slot.Name);
        var key = OffsetKey.For(folded.Parent, folded.Slot, path);
        if (folded.Promoted)
        {
            _expanded.Remove(path);
            _xml!.PreferFlat(folded.Slot.Name);
            _offsets[key] = Seed(folded.Slot, folded.ChildStart, folded.ChildEnd);
            return;
        }

        if (folded.Slot.Kind == SlotKind.List
            && folded.ChildStart >= 0
            && _offsets.TryGetValue(key, out var index)
            && folded.ChildStart >= index.Watermark)
        {
            index.Offsets.Add(folded.ChildStart);
            // The end tag can still be ahead. A later close of this same start must not be stored again.
            index.Watermark = folded.ChildEnd >= folded.ChildStart ? folded.ChildEnd : folded.ChildStart + 1;
            if (ev.Kind == XmlEventKind.Flat)
                DrainFlat(folded.Slot, index);
        }
    }

    private void DrainFlat(Slot slot, OffsetIndex index)
    {
        var stamp = System.Diagnostics.Stopwatch.GetTimestamp();
        var limit = System.Diagnostics.Stopwatch.Frequency / 80;
        while (_xml!.TryNextFlat(slot.Name, out var start, out var end))
        {
            if (!_xml.FlatSameShape && !_tree.SameFlat(slot, _xml.FlatFields))
                _tree.MergeFlat(slot, _xml.FlatFields);
            slot.Count++;
            slot.EndByte = end;
            if (start >= index.Watermark)
            {
                index.Offsets.Add(start);
                index.Watermark = end;
            }

            if (System.Diagnostics.Stopwatch.GetTimestamp() - stamp >= limit)
                break;
        }
    }

    // The first sibling is already closed when the second promotes the field.
    // The second sibling's start is known even while its end tag is still ahead.
    private static OffsetIndex Seed(Slot slot, long childStart, long childEnd)
    {
        var index = new OffsetIndex();
        index.Offsets.Add(slot.StartByte);
        if (childStart >= 0 && childStart != slot.StartByte)
        {
            index.Offsets.Add(childStart);
            index.Watermark = childEnd >= childStart ? childEnd : childStart + 1;
            return index;
        }

        index.Watermark = slot.EndByte >= 0 && childEnd < 0 ? slot.EndByte : slot.StartByte;
        return index;
    }

    private Frame ResolveRecord(DocumentPath path)
    {
        if (path.Steps.Count == 0)
            return RootFrame();
        var (parent, slot) = Locate(path);
        if (slot.Kind == SlotKind.Scalar)
            throw new InvalidOperationException($"{path} is a scalar.");
        return Opened(slot, path);
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
            frame = Opened(record, recordPath);
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
        if (_loaded.TryGetValue(offset, out var cached))
        {
            _loadedOrder.Remove(cached);
            _loadedOrder.AddFirst(cached);
            return cached.Value.Frame;
        }

        var frame = ReadFrame(offset, scope, path);
        if (frame.EndByte >= 0)
            StoreLoaded(offset, frame);
        return frame;
    }

    // A fit walks every entry once. Those frames stay out of the view cache.
    private Frame ReadFrame(long offset, NsScope scope, DocumentPath path)
    {
        if (_loaded.TryGetValue(offset, out var cached))
            return cached.Value.Frame;

        var frame = _grammar == Grammar.Json
            ? JsonLoader.Load(_document, offset, path)
            : new DocumentTree(_document, documentElement: false).Load(XmlParser.Fragment(_document, scope, offset, long.MaxValue), path);
        Normalize(frame);
        return frame;
    }

    private void StoreLoaded(long offset, Frame frame)
    {
        var node = _loadedOrder.AddFirst(new LoadedFrame(offset, frame));
        _loaded[offset] = node;
        while (_loaded.Count > 128 && _loadedOrder.Last is LinkedListNode<LoadedFrame> oldest)
        {
            _loaded.Remove(oldest.Value.Offset);
            _loadedOrder.RemoveLast();
        }
    }

    private OffsetIndex EnsureOffsets(DocumentPath path, Frame parent, Slot slot)
    {
        var key = OffsetKey.For(parent, slot, path);
        if (_offsets.TryGetValue(key, out var existing))
            return existing;
        if (slot.EntryStarts is not null)
        {
            var copied = new OffsetIndex();
            foreach (var start in slot.EntryStarts)
                copied.Offsets.Add(start);
            copied.Watermark = copied.Offsets.Count == 0 ? 0 : copied.Offsets[^1] + 1;
            _offsets[key] = copied;
            return copied;
        }
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
        _offsets[key] = index;
        return index;
    }

    // A list inside one repeated entry must not share offsets with the same names in the next entry.
    // JSON lists already keep their own entry starts, so those indexes stay on the path alone.
    private readonly record struct OffsetKey(long ParentStart, DocumentPath Path)
    {
        public static OffsetKey For(Frame parent, Slot slot, DocumentPath path) =>
            new(slot.EntryStarts is null ? parent.StartByte : 0, path);
    }

    private List<TextPiece> Pieces(DocumentPath path)
    {
        var (_, slot) = Locate(path);
        if (slot.Kind != SlotKind.Scalar)
            throw new InvalidOperationException($"{path} is not a scalar.");
        return slot.Text;
    }

    private bool TryScalar(DocumentPath path, out List<TextPiece> pieces)
    {
        pieces = [];
        if (path.Steps.Count == 0)
            return false;
        var (_, slot) = Locate(path);
        if (slot.Kind != SlotKind.Scalar)
            return false;
        pieces = slot.Text;
        return true;
    }

    private void Mark(DocumentPath path, Hit hit)
    {
        if (Marked is DocumentPath previous && !previous.Equals(path) && _chrome.TryGetValue(previous, out var old))
        {
            old.WindowStart = 0;
            old.HitOffset = null;
        }

        Marked = path;
        if (hit.OnName || !TryScalar(path, out var pieces))
        {
            if (_chrome.TryGetValue(path, out var named))
            {
                named.WindowStart = 0;
                named.HitOffset = null;
            }

            return;
        }

        var length = ExpandedText.Count(_document, pieces);
        var capacity = Capacity(ScalarWidth(path));
        var matchLength = Math.Max(1, hit.MatchLength);
        var start = length > capacity ? Place(hit.CharacterOffset, matchLength, length, capacity) : 0;
        _chrome.TryGetValue(path, out var existing);
        _chrome[path] = new Chrome
        {
            WindowStart = start,
            HitOffset = hit.CharacterOffset,
            MatchLength = matchLength,
            Length = length,
            FullValue = existing?.FullValue ?? false,
        };
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
                slot.Name, FieldKind.Record, slot.Closed, true, expanded,
                null, null, false, false, false, 0, marked);
        }

        return Scalar(slot.Name, slot.Text, path);
    }

    private List<FieldView> Cells(Frame entry, List<Column> columns, DocumentPath entryPath)
    {
        var cells = new List<FieldView>(columns.Count);
        foreach (var column in columns)
        {
            if (entry.FindKey(column.Key) is not Slot slot)
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
        var text = WindowText(path, pieces, ScalarWidth(path), out _, out var canGrow, out var start);
        var marked = Marked is not null && Marked.Equals(path);
        return new FieldView(
            name, FieldKind.Scalar, text, false, false, null, null,
            canGrow, chrome?.FullValue ?? false, canGrow, start, marked);
    }

    private static void Normalize(Frame frame)
    {
        if (frame.Text.Count == 0 || frame.FindKey(FieldKey.Text) is not null)
            return;
        var slot = new Slot
        {
            Key = FieldKey.Text,
            Name = "#text",
            Kind = SlotKind.Scalar,
            Text = frame.Text,
        };
        frame.Slots.Add(slot);
        if (frame.ByKey.Count > 0)
        {
            frame.ByKey[slot.Key] = slot;
            frame.ByName[slot.Name] = slot;
        }
    }

    private Frame RootFrame() =>
        _tree.Root ?? throw new InvalidOperationException("The document element is not available yet.");

    private Frame Opened(Slot slot, DocumentPath path) =>
        FindLive(slot.StartByte) ?? slot.Detail ?? LoadElement(slot.StartByte, slot.ParentScope, path);

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
        if (frame.FindName(name) is Slot slot)
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

    private sealed class LoadedFrame(long offset, Frame frame)
    {
        public long Offset { get; } = offset;
        public Frame Frame { get; } = frame;
    }

    private sealed class Chrome
    {
        public int WindowStart { get; set; }
        public int? HitOffset { get; set; }
        public int MatchLength { get; set; }
        public bool FullValue { get; set; }
        public int? Length { get; set; }
    }
}
