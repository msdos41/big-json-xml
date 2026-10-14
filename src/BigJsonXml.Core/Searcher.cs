using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using BigJsonXml.Xml;

namespace BigJsonXml;

internal enum SearchStep
{
    Work,
    NeedMore,
    Idle,
}

internal interface IScanSearch
{
    bool Done { get; }

    SearchStep Step();
}

internal sealed class Searcher : IScanSearch
{
    private readonly XmlDocument _document;
    private readonly XmlParser _parser;
    private readonly TextSearch _search;
    private readonly List<Hit> _hits;
    private readonly List<int> _matches = [];
    private readonly Stack<SFrame> _stack = new();
    private readonly Stack<SFrame> _spare = new();
    private readonly TextPiece[] _one = new TextPiece[1];
    private readonly bool _flat;
    private readonly byte[] _needle;
    private XmlParser _active;

    public Searcher(XmlDocument document, TextSearch search, List<Hit> hits)
    {
        _document = document;
        _parser = XmlParser.Create(document);
        _active = _parser;
        _search = search;
        _hits = hits;
        _flat = CanFlat(document, search);
        _needle = _flat ? Encoding.ASCII.GetBytes(search.FindWhat) : [];
        if (_flat)
        {
            _parser.OmitFlatShape = true;
            _parser.UseSearchNeedle(_needle);
        }
    }

    public bool Done { get; private set; }

    public SearchStep Step()
    {
        if (Done)
            return SearchStep.Idle;
        return Apply(_parser.Read());
    }

    private SearchStep Apply(XmlEvent ev)
    {
        switch (ev.Kind)
        {
            case XmlEventKind.NeedMore:
                return SearchStep.NeedMore;
            case XmlEventKind.Error:
            case XmlEventKind.Done:
                Done = true;
                return SearchStep.Idle;
            case XmlEventKind.Start:
                OnStart(ev);
                return SearchStep.Work;
            case XmlEventKind.End:
                OnEnd();
                return SearchStep.Work;
            case XmlEventKind.Text:
                OnText(ev);
                return SearchStep.Work;
            case XmlEventKind.Flat:
                OnFlat(ev);
                return SearchStep.Work;
            default:
                return SearchStep.Work;
        }
    }

    // A repeated sibling the flat scan already accepted. A miss stays a count. A hit is read again as events.
    private void OnFlat(XmlEvent ev)
    {
        if (_stack.Count == 0)
            return;
        var parser = _active;
        var name = ev.Local;
        var start = ev.ByteStart;
        var end = ev.ByteEnd;
        var deadline = Stopwatch.GetTimestamp() + Stopwatch.Frequency / 80;
        var key = FieldKey.Elem("", name);
        while (true)
        {
            TakeFlat(name, start, end);
            if (Stopwatch.GetTimestamp() >= deadline)
                return;
            var missed = parser.CountFlatMisses(name, _needle);
            if (missed > 0)
            {
                var parent = _stack.Peek();
                parent.SetCount(key, parent.CountOf(key) + missed);
                parent.Names ??= new Dictionary<FieldKey, string>();
                parent.Names.TryAdd(key, name);
                if (Stopwatch.GetTimestamp() >= deadline)
                    return;
            }

            if (!parser.TryNextFlat(name, out start, out end))
                return;
        }
    }

    private void TakeFlat(string name, long start, long end)
    {
        if (!HoldsNeedle(start, end))
        {
            var parent = _stack.Peek();
            var key = FieldKey.Elem("", name);
            parent.SetCount(key, parent.CountOf(key) + 1);
            parent.Names ??= new Dictionary<FieldKey, string>();
            parent.Names.TryAdd(key, name);
            return;
        }

        var fragment = XmlParser.Fragment(_document, _stack.Peek().Scope, start, end);
        fragment.OmitFlatShape = true;
        fragment.UseSearchNeedle(_needle);
        var saved = _active;
        _active = fragment;
        try
        {
            while (true)
            {
                var inner = fragment.Read();
                if (inner.Kind is XmlEventKind.Done or XmlEventKind.NeedMore or XmlEventKind.Error)
                    break;
                Apply(inner);
            }
        }
        finally
        {
            _active = saved;
        }
    }

    private bool HoldsNeedle(long start, long end)
    {
        var length = (int)(end - start);
        if (length < _needle.Length)
            return false;
        if (_active.TryWindow(start, end, out var owned))
            return owned.IndexOf(_needle) >= 0;
        var rented = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            var buf = rented.AsSpan(0, length);
            var got = 0;
            while (got < length)
            {
                var n = _document.Source.Read(start + got, buf[got..]);
                if (n <= 0)
                    return true;
                got += n;
            }

            return buf.IndexOf(_needle) >= 0;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private static bool CanFlat(XmlDocument document, TextSearch search)
    {
        if (!document.Source.IsComplete || document.Encoding.CodePage != 65001)
            return false;
        if (!search.MatchCase || search.StartsWith || search.EntireCell)
            return false;
        if (!search.ElementName || !search.AttributeName || !search.Value)
            return false;
        if (search.FieldName.Length != 0 || search.ParentElementName.Length != 0
            || search.GrandfatherElementName.Length != 0 || search.ParentAttributeName.Length != 0)
            return false;
        if (search.FindWhat.Length == 0)
            return false;
        foreach (var unit in search.FindWhat)
        {
            if (unit < ' ' || unit > '~' || unit == '@')
                return false;
        }

        return true;
    }

    private void OnStart(XmlEvent ev)
    {
        if (_stack.Count == 0)
        {
            var root = Rent();
            root.Path = DocumentPath.Root;
            root.Scope = ev.Scope;
            root.Name = ev.Name;
            root.Ns = ev.Ns;
            root.Local = ev.Local;
            root.HitOrigin = _hits.Count;
            if (ev.Attributes.Count > 0 || ev.MissAttribute)
                root.SawAttribute = true;
            _stack.Push(root);
            if (!ev.SearchMiss)
                SearchDisplayedName(root.Path, ev.Name, element: true, container: null);
            SearchAttributes(root, ev);
            if (ev.Empty)
                OnEnd();
            return;
        }

        var parent = _stack.Peek();
        var key = FieldKey.Elem(ev.Ns, ev.Local);
        var count = parent.CountOf(key) + 1;
        parent.SetCount(key, count);
        parent.Names ??= new Dictionary<FieldKey, string>();
        if (!parent.Names.ContainsKey(key))
            parent.Names[key] = ev.Name;
        var display = parent.Names[key];
        if (count == 2)
            Promote(parent, key, display);
        var index = count >= 2 ? count : (int?)null;
        var frame = Rent();
        frame.Path = parent.Path.Field(display, index);
        frame.Scope = ev.Scope;
        frame.Name = display;
        frame.Ns = ev.Ns;
        frame.Local = ev.Local;
        frame.Index = index;
        frame.SawAttribute = ev.Attributes.Count > 0 || ev.MissAttribute;
        frame.Parent = parent;
        frame.HitOrigin = _hits.Count;
        parent.SawElement = true;
        _stack.Push(frame);
        if (!ev.SearchMiss)
            SearchDisplayedName(frame.Path, display, element: true, container: parent);
        SearchAttributes(frame, ev);
        if (ev.Empty)
            OnEnd();
    }

    private void OnText(XmlEvent ev)
    {
        if (_stack.Count == 0 || !ev.HasText)
            return;
        if (!ev.TrustedText)
        {
            _one[0] = ev.Text;
            if (ExpandedText.IsWhitespaceOnly(_document, _one))
                return;
        }

        _stack.Peek().AddText(ev.Text);
    }

    private void OnEnd()
    {
        if (_stack.Count == 0)
            return;
        var frame = _stack.Pop();
        if (_flat && frame.Index == 2)
            _active.PreferFlat(frame.Local);
        if (_stack.Count == 0 || !frame.IsSimple || frame.Index is not null)
            SearchOwnText(frame);
        else
            SearchSimple(frame);
        Recycle(frame);
    }

    private void SearchAttributes(SFrame frame, XmlEvent ev)
    {
        foreach (var attr in ev.Attributes)
        {
            var shown = "@" + attr.Name;
            (frame.AttributeNames ??= []).Add(shown);
            var path = frame.Path.Field(shown);
            SearchDisplayedName(path, shown, element: false, container: frame);
            var piece = attr.Value;
            SearchValue(path, shown, attribute: true, attr.Ns, attr.Local, frame.Scope, MemoryMarshal.CreateReadOnlySpan(ref piece, 1), simple: false, container: frame);
        }
    }

    private void SearchOwnText(SFrame frame)
    {
        if (!frame.HasText)
            return;
        var path = frame.Path.Field("#text");
        SearchFrame(frame, path, "#text", attribute: false, "", "#text", simple: false, container: frame);
    }

    private void SearchSimple(SFrame frame)
    {
        if (!frame.HasText)
            return;
        if (frame.Index is null && _stack.Count > 0)
            Remember(frame);
        SearchFrame(frame, frame.Path, frame.Name, attribute: false, frame.Ns, frame.Local, simple: true, container: frame.Parent);
    }

    private void Remember(SFrame frame)
    {
        var parent = _stack.Peek();
        var pending = new PendingSimple { Scope = frame.Scope };
        if (frame.MoreText is { Count: > 0 } more)
            pending.More = [..more];
        else
        {
            pending.One = frame.FirstText;
            pending.HasOne = true;
        }

        (parent.Pending ??= new Dictionary<FieldKey, PendingSimple>())[FieldKey.Elem(frame.Ns, frame.Local)] = pending;
    }

    private void SearchFrame(
        SFrame frame,
        DocumentPath path,
        string name,
        bool attribute,
        string ns,
        string local,
        bool simple,
        SFrame? container)
    {
        if (frame.MoreText is { Count: > 0 } more)
        {
            SearchValue(path, name, attribute, ns, local, frame.Scope, CollectionsMarshal.AsSpan(more), simple, container);
            return;
        }

        var one = frame.FirstText;
        SearchValue(path, name, attribute, ns, local, frame.Scope, MemoryMarshal.CreateReadOnlySpan(ref one, 1), simple, container);
    }

    private void SearchValue(
        DocumentPath path,
        string name,
        bool attribute,
        string ns,
        string local,
        NsScope scope,
        ReadOnlySpan<TextPiece> pieces,
        bool simple,
        SFrame? container)
    {
        if (!_search.Value || _search.FindWhat.Length == 0 || !Allows(name) || !Passes(container))
            return;
        var length = _search.FindWhat.Length;
        var dropOnPromote = simple && _search.FieldName.Length > 0;
        ExpandedText.Match(_document, pieces, _search, _matches);
        foreach (var offset in _matches)
            Add(path, offset, length, simpleText: simple, fieldOnSimple: dropOnPromote);
    }

    private void SearchDisplayedName(DocumentPath path, string displayed, bool element, SFrame? container)
    {
        if (element)
        {
            if (!_search.ElementName)
                return;
        }
        else if (!_search.AttributeName)
        {
            return;
        }

        var needle = _search.FindWhat;
        if (needle.Length == 0 || displayed.Length == 0 || !Allows(displayed) || !Passes(container))
            return;
        var comparison = _search.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        if (_search.EntireCell)
        {
            if (displayed.Equals(needle, comparison))
                AddName(path, 0, needle.Length, displayed);
            return;
        }

        if (_search.StartsWith)
        {
            if (displayed.StartsWith(needle, comparison))
                AddName(path, 0, needle.Length, displayed);
            return;
        }

        var from = 0;
        while (from <= displayed.Length - needle.Length)
        {
            var found = displayed.IndexOf(needle, from, comparison);
            if (found < 0)
                return;
            AddName(path, found, needle.Length, displayed);
            from = found + 1;
        }
    }

    private void AddName(DocumentPath path, int offset, int matchLength, string name)
    {
        var hit = new Hit(offset, matchLength)
        {
            OnName = true,
            NameText = name,
        };
        foreach (var step in path.Steps)
            hit.Steps.Add(step);
        _hits.Add(hit);
    }

    private void Add(DocumentPath path, int offset, int matchLength, bool simpleText, bool fieldOnSimple)
    {
        var hit = new Hit(offset, matchLength)
        {
            SimpleText = simpleText,
            FieldOnSimple = fieldOnSimple,
        };
        foreach (var step in path.Steps)
            hit.Steps.Add(step);
        _hits.Add(hit);
    }

    private void Promote(SFrame parent, FieldKey key, string name)
    {
        var depth = parent.Path.Steps.Count;
        // A hit recorded before this parent opened belongs to an earlier entry.
        for (var i = _hits.Count - 1; i >= parent.HitOrigin; i--)
        {
            var hit = _hits[i];
            if (hit.Steps.Count <= depth)
                continue;
            var step = hit.Steps[depth];
            if (step.Name != name || step.OneBasedIndex is not null)
                continue;
            if ((hit.FieldOnSimple || hit.SimpleText) && hit.Steps.Count == depth + 1)
            {
                _hits.RemoveAt(i);
                continue;
            }

            hit.Steps[depth] = step with { OneBasedIndex = 1 };
        }

        if (parent.Pending is null || !parent.Pending.Remove(key, out var pending))
            return;
        var textPath = parent.Path.Field(name, 1).Field("#text");
        var entry = Rent();
        entry.Name = name;
        entry.Parent = parent;
        if (pending.More is { Count: > 0 } more)
            SearchValue(textPath, "#text", attribute: false, "", "#text", pending.Scope, CollectionsMarshal.AsSpan(more), simple: false, container: entry);
        else if (pending.HasOne)
        {
            var one = pending.One;
            SearchValue(textPath, "#text", attribute: false, "", "#text", pending.Scope, MemoryMarshal.CreateReadOnlySpan(ref one, 1), simple: false, container: entry);
        }

        Recycle(entry);
    }

    private bool Passes(SFrame? container)
    {
        if (!MatchesName(_search.ParentElementName, container?.Name))
            return false;
        if (!MatchesName(_search.GrandfatherElementName, container?.Parent?.Name))
            return false;
        return MatchesAttribute(_search.ParentAttributeName, container);
    }

    private bool MatchesName(string required, string? actual)
    {
        if (required.Length == 0)
            return true;
        if (actual is null)
            return false;
        var comparison = _search.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return actual.Equals(required, comparison);
    }

    private bool MatchesAttribute(string required, SFrame? container)
    {
        if (required.Length == 0)
            return true;
        if (container?.AttributeNames is null)
            return false;
        var comparison = _search.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        foreach (var name in container.AttributeNames)
        {
            if (name.Equals(required, comparison))
                return true;
        }

        return false;
    }

    private bool Allows(string displayedName)
    {
        var required = _search.FieldName;
        if (required.Length == 0)
            return true;
        var comparison = _search.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return displayedName.Equals(required, comparison);
    }

    private SFrame Rent()
    {
        if (!_spare.TryPop(out var frame))
            return new SFrame();
        frame.Reset();
        return frame;
    }

    private void Recycle(SFrame frame)
    {
        if (_spare.Count < 64)
            _spare.Push(frame);
    }

    private sealed class SFrame
    {
        public DocumentPath Path = DocumentPath.Root;
        public NsScope Scope = NsScope.Empty;
        public string Name = "";
        public string Ns = "";
        public string Local = "";
        public int? Index;
        public int HitOrigin;
        public SFrame? Parent;
        public bool SawAttribute;
        public bool SawElement;
        public bool IsSimple => !SawAttribute && !SawElement;
        public TextPiece FirstText;
        public bool HasText;
        public List<TextPiece>? MoreText;
        public List<string>? AttributeNames;
        public Dictionary<FieldKey, int>? Counts;
        public Dictionary<FieldKey, string>? Names;
        public Dictionary<FieldKey, PendingSimple>? Pending;

        public int CountOf(FieldKey key) => Counts?.GetValueOrDefault(key) ?? 0;

        public void SetCount(FieldKey key, int count)
        {
            Counts ??= new Dictionary<FieldKey, int>();
            Counts[key] = count;
        }

        public void AddText(TextPiece piece)
        {
            if (!HasText)
            {
                FirstText = piece;
                HasText = true;
                return;
            }

            MoreText ??= [];
            if (MoreText.Count == 0)
                MoreText.Add(FirstText);
            MoreText.Add(piece);
        }

        public void Reset()
        {
            Path = DocumentPath.Root;
            Scope = NsScope.Empty;
            Name = "";
            Ns = "";
            Local = "";
            Index = null;
            HitOrigin = 0;
            Parent = null;
            SawAttribute = false;
            SawElement = false;
            HasText = false;
            MoreText?.Clear();
            AttributeNames?.Clear();
            Counts?.Clear();
            Names?.Clear();
            Pending?.Clear();
        }
    }

    private sealed class PendingSimple
    {
        public TextPiece One;
        public bool HasOne;
        public List<TextPiece>? More;
        public NsScope Scope = NsScope.Empty;
    }
}
