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
    // A fitted list column stops here, header included (ADR 0056).
    public const int FitColumnCharacters = 60;
    // A list column fits from at most this many entries, spread over the list (ADR 0056).
    public const int FitSampleEntries = 4096;
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
    // The search can name an entry the tree has not counted yet. Such a hit waits here.
    private readonly List<Hit> _found = [];
    private readonly HitView _hits;
    private long _retryAt;
    private long _blockedFrom = -1;
    private XmlParser? _xml;
    private JsonParser? _json;
    private JsonTree? _jsonTree;
    private Grammar _grammar;
    private IScanSearch? _search;
    private BackgroundSearch? _background;
    private SearchSkip? _lastSkip;
    private SkipList? _rootEntries;

    // A short document is searched at once, so it needs no thread. One step of a large one can take
    // a whole drain slice, so a large document goes straight to the thread.
    internal long InlineSearchTicks = System.Diagnostics.Stopwatch.Frequency / 500;
    internal long InlineSearchBytes = 4L << 20;
    internal long InlineSearchCompleteBytes = 64L << 10;

    // How long one drain of repeated siblings may run. Tests shorten it to pin a slow machine.
    internal long FlatDrainTicks = System.Diagnostics.Stopwatch.Frequency / 80;

    private ViewerSession(IByteSource source, IDisposable? owned)
    {
        _document = new XmlDocument(source);
        _tree = new DocumentTree(_document, documentElement: true);
        _owned = owned;
        _hits = new HitView(_found);
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
            // Only the search thread is left. Waiting for it does not count towards the limit.
            while (Progress != ScanProgress.Scanning && SearchInBackground)
            {
                WaitForSearch(10);
                Publish();
            }
        }

        throw new InvalidOperationException("The scan did not finish.");
    }

    public bool Pump(int budget)
    {
        var any = PumpSlice(budget);
        var published = Publish();
        return any || published || SearchInBackground;
    }

    /// <summary>True while a search after the scan still runs on its own thread or has hits to hand over.</summary>
    public bool SearchInBackground => _background is { } background && (!background.Finished || background.HasWaiting);

    /// <summary>Blocks up to <paramref name="milliseconds"/> until the search thread finds more or ends.</summary>
    public void WaitForSearch(int milliseconds)
    {
        if (_background is { Finished: false } background && !background.HasWaiting)
            background.Wait(milliseconds);
    }

    // Entries the last search jumped over, for tests and measurements.
    internal long SearchSkipped => _lastSkip?.Skipped ?? 0;

    private bool PumpSlice(int budget)
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
                var reached = Progress == ScanProgress.Scanning ? BytesReached : long.MaxValue;
                _search.Front = CountedFront(reached);
                var steps = 0;
                do
                {
                    var step = _search.Step();
                    steps++;
                    if (step == SearchStep.Work)
                    {
                        worked = true;
                    }
                    else if (step == SearchStep.NeedMore)
                    {
                        if (!worked)
                            return any;
                        break;
                    }
                    else
                    {
                        break;
                    }
                }
                while (_search is { Done: false } && _search.FileByte < reached && steps < 512);
            }

            if (!worked)
                break;
            any = true;
        }

        return any;
    }

    // The tree counts a simple element at its end tag. Until then search stops at its start tag,
    // so it cannot turn a published hit's field into a list the outline does not hold yet.
    private long CountedFront(long reached)
    {
        if (_grammar == Grammar.Xml && _tree.Stack.TryPeek(out var open) && !open.Announced)
            return Math.Min(reached, open.StartByte);
        return reached;
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
        StopBackground(join: false);
        ClearMark();
        _found.Clear();
        _hits.Count = 0;
        _blockedFrom = -1;
        _lastSkip = null;
        if (search.FindWhat.Length == 0)
        {
            _search = null;
            return;
        }

        if (_grammar == Grammar.Unknown)
            Detect();
        if (StartBackground(search))
        {
            _search = null;
            return;
        }

        _search = _grammar switch
        {
            Grammar.Json => new JsonSearcher(_document, search, _found),
            Grammar.Xml => new Searcher(_document, search, _found) { Removed = Unpublish },
            _ => null,
        };
    }

    public void CancelSearch()
    {
        _search = null;
        StopBackground(join: false);
    }

    private void StopBackground(bool join)
    {
        _background?.Cancel(join);
        _background = null;
    }

    // A search after a finished scan reads its own copy of the file on another thread. Hits reach the
    // list only once their paths are final, so Find Next and Find All see the same hits in the same order.
    private bool StartBackground(TextSearch search)
    {
        if (Progress != ScanProgress.Completed || _grammar is not (Grammar.Xml or Grammar.Json))
            return false;
        if (_document.Source is not IReaderSource readers || readers.OpenReader() is not { } reader)
            return false;
        var owned = ReferenceEquals(reader, _document.Source) ? null : reader as IDisposable;
        var document = new XmlDocument(reader);
        var hits = new List<Hit>();
        var skip = PlanSkip(search, reader);
        IScanSearch searcher;
        Func<int, int> stable;
        if (_grammar == Grammar.Json)
        {
            var json = new JsonSearcher(document, search, hits) { Skip = skip };
            searcher = json;
            stable = json.StableCount;
        }
        else
        {
            var xml = new Searcher(document, search, hits) { Skip = skip, Outline = ListFields() };
            searcher = xml;
            stable = xml.StableCount;
        }

        Action<Func<bool>>? prepare = null;
        if (skip is not null && _grammar == Grammar.Xml && skip.Lists.FirstOrDefault(list => list.Offsets is null) is { } entries)
            prepare = cancelled => entries.Offsets ??= EntryTagScan.Build(reader, entries, cancelled);
        _lastSkip = skip;
        var background = new BackgroundSearch(searcher, hits, stable, owned, skip, prepare);
        _background = background;
        // A tiny document finishes here whatever the machine's load, so its hits are there at once.
        if (_document.Source.Length <= InlineSearchCompleteBytes && InlineSearchTicks > 0)
            background.RunInline(long.MaxValue / 2);
        else if (_document.Source.Length <= InlineSearchBytes)
            background.RunInline(InlineSearchTicks);
        background.Start();
        Publish();
        return true;
    }

    // Jumping is safe only when every hit's text holds the probe below in its raw bytes,
    // or sits near a byte the finder also stops at (an escape, an entity, a tag that splits text).
    private SearchSkip? PlanSkip(TextSearch search, IByteSource reader)
    {
        var needle = search.FindWhat;
        if (needle.Length == 0 || _tree.Root is not { } root)
            return null;
        // Without match case only ASCII letters fold: no other character equals an ASCII one when case is ignored.
        var fold = !search.MatchCase;
        var comparison = fold ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var json = _grammar == Grammar.Json;
        if (json && search.ElementName && "#value".Contains(needle, comparison))
            return null;
        if (!json && _document.Encoding.CodePage != 65001)
            return null;
        var probe = Rarest(Parts(needle, fold, json), fold, reader);
        if (probe.Length == 0)
            return null;
        var bytes = System.Text.Encoding.UTF8.GetBytes(probe);
        var patterns = new List<CandidatePattern> { new(bytes, Fold: fold) };
        var lists = new List<SkipList>();
        if (json)
        {
            WalkOutline(root, DocumentPath.Root, (frame, path) =>
            {
                foreach (var slot in frame.Slots)
                {
                    if (slot.Kind == SlotKind.List && slot.EntryStarts is { Count: > 1 } starts)
                        lists.Add(new SkipList(slot.StartByte, slot.Name, starts) { Parent = path });
                }
            });
            patterns.Add(new("\\"u8.ToArray()));
        }
        else
        {
            WalkOutline(root, DocumentPath.Root, (frame, path) =>
            {
                if (XmlSkipList(frame, path, ReferenceEquals(frame, root)) is { } list)
                    lists.Add(list);
            });
            patterns.Add(new("&"u8.ToArray()));
            patterns.Add(new("xmlns"u8.ToArray()));
            patterns.Add(new("<![CDATA["u8.ToArray()));
            for (var cut = 1; cut < bytes.Length; cut++)
            {
                var split = new byte[cut + 1];
                bytes.AsSpan(0, cut).CopyTo(split);
                split[cut] = (byte)'<';
                patterns.Add(new(split, '/', fold));
            }
        }

        return lists.Count == 0 ? null : new SearchSkip(new CandidateFinder(reader, patterns), lists);
    }

    // The longest part of the needle that every hit holds byte for byte, apart from letter case.
    // XML turns tabs and line breaks in attribute values into spaces, and shows names with @ and #
    // that the bytes do not hold, so those characters split the needle. Ignoring case keeps only
    // ASCII, because only ASCII letters fold to ASCII.
    internal static string Probe(string needle, bool fold, bool json) =>
        Parts(needle, fold, json).OrderByDescending(part => part.Length).FirstOrDefault() ?? "";

    // The parts of the needle that every hit holds byte for byte, apart from letter case.
    internal static List<string> Parts(string needle, bool fold, bool json)
    {
        var parts = new List<string>();
        var start = 0;
        for (var i = 0; i <= needle.Length; i++)
        {
            if (i < needle.Length && Keeps(needle[i], fold, json))
                continue;
            if (i > start)
                parts.Add(needle[start..i]);
            start = i + 1;
        }

        return parts;
    }

    // Any part will do, so the one seen least in a sample from the middle of the file is looked for.
    // A common word would stop the search at nearly every entry.
    private static string Rarest(List<string> parts, bool fold, IByteSource reader)
    {
        if (parts.Count <= 1)
            return parts.Count == 1 ? parts[0] : "";
        var sample = new byte[(int)Math.Min(1 << 20, reader.Length)];
        var at = Math.Max(0, reader.Length / 2 - sample.Length / 2);
        var got = 0;
        while (got < sample.Length)
        {
            var n = reader.Read(at + got, sample.AsSpan(got));
            if (n <= 0)
                break;
            got += n;
        }

        var hay = sample.AsSpan(0, got);
        var best = parts[0];
        var fewest = int.MaxValue;
        foreach (var part in parts)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(fold ? part.ToLowerInvariant() : part);
            var count = 0;
            for (var i = 0; i < hay.Length && count <= fewest;)
            {
                var found = fold ? CandidateFinder.IndexOfFolded(hay[i..], bytes) : hay[i..].IndexOf(bytes);
                if (found < 0)
                    break;
                count++;
                i += found + 1;
            }

            if (count < fewest || (count == fewest && part.Length > best.Length))
            {
                fewest = count;
                best = part;
            }
        }

        return best;
    }

    private static bool Keeps(char unit, bool fold, bool json)
    {
        if (fold && (unit < 0x20 || unit > 0x7e))
            return false;
        if (json)
            return true;
        return unit is not (' ' or '\t' or '\n' or '\r' or '@' or '#');
    }

    // Record frames the finished outline holds outside list entries, with their paths.
    private static void WalkOutline(Frame frame, DocumentPath path, Action<Frame, DocumentPath> visit)
    {
        visit(frame, path);
        foreach (var slot in frame.Slots)
        {
            if (slot.Kind == SlotKind.Record && slot.Detail is { } detail && !ReferenceEquals(detail, frame))
                WalkOutline(detail, slot.Path ?? path.Field(slot.Name), visit);
        }
    }

    // A list may be jumped through when it is its parent's only child element and the parent has no text,
    // so the bytes between two entries hold nothing else that counts.
    private SkipList? XmlSkipList(Frame frame, DocumentPath path, bool isRoot)
    {
        if (!ReferenceEquals(frame.Scope, NsScope.Empty))
            return null;
        Slot? list = null;
        foreach (var slot in frame.Slots)
        {
            if (slot.Key.Kind == NameKind.Attribute)
                continue;
            if (list is not null)
                return null;
            list = slot;
        }

        if (list is null || list.Kind != SlotKind.List || list.Key.Kind != NameKind.Element || list.Key.Ns.Length != 0 || list.Count < 3)
            return null;
        var listPath = list.Path ?? path.Field(list.Name);
        if (_offsets.TryGetValue(OffsetKey.For(frame, list, listPath), out var index) && index.Offsets.Count == list.Count)
            return new SkipList(-1, list.Key.Local, index.Offsets) { Parent = path };
        if (!isRoot || frame.ContentEnd < 0)
            return null;
        _rootEntries = _rootEntries is { } known && known.Name == list.Key.Local && known.Expected == list.Count
            ? known
            : new SkipList(-1, list.Key.Local, null) { Parent = path, ContentStart = frame.ContentByte, ContentEnd = frame.ContentEnd, Expected = list.Count };
        _rootEntries.Misses = 0;
        _rootEntries.Pause = 0;
        _rootEntries.PauseUntil = 0;
        return _rootEntries;
    }

    // For each record frame outside list entries, the fields the finished outline holds as lists.
    private Dictionary<DocumentPath, HashSet<string>> ListFields()
    {
        var outline = new Dictionary<DocumentPath, HashSet<string>>();
        if (_tree.Root is { } root)
        {
            WalkOutline(root, DocumentPath.Root, (frame, path) =>
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var slot in frame.Slots)
                {
                    if (slot.Kind == SlotKind.List)
                        names.Add(slot.Name);
                }

                outline[path] = names;
            });
        }

        return outline;
    }

    // Hits are published in document order. One that cannot be read yet holds back the hits after it.
    private bool Publish()
    {
        var before = _hits.Count;
        if (_background is { } background)
        {
            background.Drain(_found);
            if (background.Finished && !background.HasWaiting)
                _background = null;
        }

        if (Progress != ScanProgress.Scanning)
        {
            _hits.Count = _found.Count;
            _blockedFrom = -1;
            return _hits.Count != before;
        }

        while (_hits.Count < _found.Count)
        {
            var reached = BytesReached;
            if (_blockedFrom >= 0 && reached < _retryAt)
                return _hits.Count != before;
            if (!Readable(_found[_hits.Count]))
            {
                // Reading an open entry again costs its length, so the wait doubles.
                if (_blockedFrom < 0)
                    _blockedFrom = reached;
                _retryAt = reached + Math.Max(4096, reached - _blockedFrom);
                return _hits.Count != before;
            }

            _blockedFrom = -1;
            _hits.Count++;
        }

        return _hits.Count != before;
    }

    private void Unpublish(int index)
    {
        if (index < _hits.Count)
            _hits.Count--;
    }

    private bool Readable(Hit hit)
    {
        if (!hit.OnName)
            return TryHitPieces(hit, out _);
        if (hit.Path.Steps.Count == 0)
            return true;
        try
        {
            Locate(hit.Path);
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentOutOfRangeException)
        {
            return false;
        }
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
        if (!TryHitPieces(hit, out var pieces))
            throw new InvalidOperationException($"{hit.Path} is not a scalar.");
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

    public void Dispose()
    {
        StopBackground(join: true);
        _xml?.Dispose();
        _owned?.Dispose();
    }

    internal long XmlStartCount => _xml?.StartCount ?? 0;
    internal int XmlShapeCacheCount => _xml?.ShapeCacheCount ?? 0;

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

        if (note.HasEntry && note.Slot is Slot slot)
            DrainJson(slot);
    }

    // A JSON list's offset index is the slot's own entry starts, so a drained entry is stored once.
    private void DrainJson(Slot slot)
    {
        var stamp = System.Diagnostics.Stopwatch.GetTimestamp();
        var limit = System.Diagnostics.Stopwatch.Frequency / 80;
        var deadline = stamp + limit;
        while (System.Diagnostics.Stopwatch.GetTimestamp() - stamp < limit)
        {
            var run = _json!.TakeScalarRun(slot.EntryStarts!, deadline);
            if (run > 0)
            {
                JsonTree.AddValues(slot, run);
                continue;
            }

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

        if (!folded.Promoted && folded.Slot.Kind != SlotKind.List)
            return;

        var path = folded.Slot.Path ?? folded.Parent.Path.Field(folded.Slot.Name);
        var key = OffsetKey.For(folded.Parent, folded.Slot, path);
        if (folded.Promoted)
        {
            _expanded.Remove(path);
            _xml!.PreferFlat(folded.Slot.Name);
            var seededIndex = Seed(folded.Slot, folded.ChildStart, folded.ChildEnd);
            _offsets[key] = seededIndex;
            if (ev.Kind == XmlEventKind.Flat)
                DrainFlat(folded.Slot, seededIndex);
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
        var limit = FlatDrainTicks;
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
            var shared = new OffsetIndex(slot.EntryStarts);
            _offsets[key] = shared;
            return shared;
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
        if (TryScalar(path, out var pieces))
            return pieces;
        throw new InvalidOperationException($"{path} is not a scalar.");
    }

    // Search can still hold the first simple match on the field path after the
    // tree has already promoted that field to a list.
    private bool TryHitPieces(Hit hit, out List<TextPiece> pieces)
    {
        pieces = [];
        try
        {
            if (TryScalar(hit.Path, out pieces))
                return true;
            if (!hit.SimpleText || hit.Path.Steps.Count == 0 || hit.Path.Steps[^1].OneBasedIndex is not null)
                return false;
            var (_, slot) = Locate(hit.Path);
            if (slot.Kind != SlotKind.List)
                return false;
            return TryScalar(hit.Path.WithIndex(1).Field("#text"), out pieces);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentOutOfRangeException)
        {
            return false;
        }
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
