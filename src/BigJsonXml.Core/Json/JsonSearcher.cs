using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using BigJsonXml.Xml;

namespace BigJsonXml.Json;

/// <summary>
/// Text search over a JSON document. Values are the shown literals. Field names use the
/// element-name switch. Attribute names and parent attributes match nothing.
/// </summary>
internal sealed class JsonSearcher : IScanSearch, IJsonItem
{
    private readonly XmlDocument _document;
    private readonly JsonParser _parser;
    private readonly TextSearch _search;
    private readonly List<Hit> _hits;
    private readonly List<int> _matches = [];
    private readonly Stack<Node> _stack = new();
    private readonly Stack<Node> _spare = new();
    private readonly bool _bulk;
    private readonly byte[] _needleBytes;
    private string _pending = "";
    private Node? _visitArray;

    public JsonSearcher(XmlDocument document, TextSearch search, List<Hit> hits)
    {
        _document = document;
        _parser = JsonParser.Document(document);
        _search = search;
        _hits = hits;
        _bulk = CanBulk(document, search);
        _needleBytes = _bulk ? Encoding.ASCII.GetBytes(search.FindWhat) : [];
    }

    public bool Done { get; private set; }

    public SearchStep Step()
    {
        if (Done)
            return SearchStep.Idle;
        var ev = _parser.Read();
        switch (ev.Kind)
        {
            case JsonEventKind.NeedMore:
                return SearchStep.NeedMore;
            case JsonEventKind.Continue:
                return SearchStep.Work;
            case JsonEventKind.Error:
                DropUncommitted();
                Done = true;
                return SearchStep.Idle;
            case JsonEventKind.Refuse:
                _hits.Clear();
                Done = true;
                return SearchStep.Idle;
            case JsonEventKind.Done:
                Done = true;
                return SearchStep.Idle;
            case JsonEventKind.ObjectStart:
                OnObject();
                return Done ? SearchStep.Idle : SearchStep.Work;
            case JsonEventKind.ObjectEnd:
            case JsonEventKind.ArrayEnd:
                if (_stack.Count > 0)
                    Recycle(_stack.Pop());
                if (!Done)
                    VisitBulk();
                return Done ? SearchStep.Idle : SearchStep.Work;
            case JsonEventKind.ArrayStart:
                OnArray();
                return SearchStep.Work;
            case JsonEventKind.Key:
                OnKey(ev);
                return Done ? SearchStep.Idle : SearchStep.Work;
            case JsonEventKind.Scalar:
                OnScalar(ev);
                if (!Done)
                    VisitBulk();
                return Done ? SearchStep.Idle : SearchStep.Work;
            default:
                return SearchStep.Work;
        }
    }

    private void OnObject()
    {
        if (_stack.Count == 0)
        {
            var root = Rent();
            root.Path = DocumentPath.Root;
            _stack.Push(root);
            return;
        }

        var parent = _stack.Peek();
        if (parent.Array)
        {
            parent.Count++;
            _stack.Push(OpenEntry(parent, parent.Path.WithIndex(parent.Count)));
            return;
        }

        var path = parent.Path.Field(_pending);
        SearchName(path, _pending, parent);
        var child = Rent();
        child.Name = _pending;
        child.Parent = parent;
        child.Path = path;
        _stack.Push(child);
    }

    private void OnArray()
    {
        if (_stack.Count == 0)
        {
            var path = DocumentPath.Root.Field("#value");
            SearchName(path, "#value", null);
            var root = Rent();
            root.Name = "#value";
            root.Array = true;
            root.Path = path;
            _stack.Push(root);
            return;
        }

        var parent = _stack.Peek();
        if (parent.Array)
        {
            parent.Count++;
            var path = parent.Path.WithIndex(parent.Count).Field("#value");
            var entry = OpenEntry(parent, path);
            entry.Name = "#value";
            entry.Array = true;
            _stack.Push(entry);
            SearchName(path, "#value", parent);
            return;
        }

        var field = parent.Path.Field(_pending);
        SearchName(field, _pending, parent);
        var child = Rent();
        child.Name = _pending;
        child.Array = true;
        child.Parent = parent;
        child.Path = field;
        _stack.Push(child);
    }

    private void OnKey(JsonEvent ev)
    {
        var container = _stack.Peek();
        container.Names ??= new HashSet<string>(StringComparer.Ordinal);
        if (!container.Names.Add(ev.Name))
        {
            DropUncommitted();
            Done = true;
            return;
        }

        _pending = ev.Name;
    }

    private void OnScalar(JsonEvent ev)
    {
        if (_stack.Count == 0)
        {
            var path = DocumentPath.Root.Field("#value");
            SearchName(path, "#value", null);
            SearchLiteral(path, "#value", ev, null);
            return;
        }

        var parent = _stack.Peek();
        if (parent.Array)
        {
            parent.Count++;
            var path = parent.Path.WithIndex(parent.Count).Field("#value");
            SearchName(path, "#value", parent);
            SearchLiteral(path, "#value", ev, parent);
            return;
        }

        var field = parent.Path.Field(_pending);
        SearchName(field, _pending, parent);
        SearchLiteral(field, _pending, ev, parent);
    }

    private Node OpenEntry(Node parent, DocumentPath path)
    {
        var node = Rent();
        node.Parent = parent;
        node.Path = path;
        node.Provisional = true;
        node.HitOrigin = _hits.Count;
        return node;
    }

    private void DropUncommitted()
    {
        var origin = _hits.Count;
        foreach (var node in _stack)
        {
            if (node.Provisional && node.HitOrigin >= 0 && node.HitOrigin < origin)
                origin = node.HitOrigin;
        }

        if (origin < _hits.Count)
            _hits.RemoveRange(origin, _hits.Count - origin);
    }

    private void SearchName(DocumentPath path, string name, Node? container)
    {
        if (!_search.ElementName || !Allows(name) || !Passes(container))
            return;
        var needle = _search.FindWhat;
        if (needle.Length == 0 || name.Length == 0)
            return;
        var comparison = _search.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        if (_search.EntireCell)
        {
            if (name.Equals(needle, comparison))
                AddName(path, 0, needle.Length, name);
            return;
        }

        if (_search.StartsWith)
        {
            if (name.StartsWith(needle, comparison))
                AddName(path, 0, needle.Length, name);
            return;
        }

        var from = 0;
        while (from <= name.Length - needle.Length)
        {
            var found = name.IndexOf(needle, from, comparison);
            if (found < 0)
                return;
            AddName(path, found, needle.Length, name);
            from = found + 1;
        }
    }

    private void SearchLiteral(DocumentPath path, string name, JsonEvent ev, Node? container)
    {
        if (!_search.Value || _search.FindWhat.Length == 0 || !Allows(name) || !Passes(container))
            return;
        var piece = TextPiece.JsonLiteral(ev.ByteStart, ev.ByteEnd);
        var length = _search.FindWhat.Length;
        ExpandedText.Match(_document, MemoryMarshal.CreateReadOnlySpan(ref piece, 1), _search, _matches);
        foreach (var offset in _matches)
            Add(path, offset, length);
    }

    private bool Passes(Node? container)
    {
        var parent = Named(container);
        var grand = parent is null ? null : Named(parent.Parent);
        if (!Matches(_search.ParentElementName, parent?.Name))
            return false;
        if (!Matches(_search.GrandfatherElementName, grand?.Name))
            return false;
        return _search.ParentAttributeName.Length == 0;
    }

    private Node? Named(Node? node)
    {
        while (node is not null && node.Name is null)
            node = node.Parent;
        return node;
    }

    private bool Matches(string required, string? actual)
    {
        if (required.Length == 0)
            return true;
        if (actual is null)
            return false;
        var comparison = _search.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return actual.Equals(required, comparison);
    }

    private bool Allows(string name)
    {
        var required = _search.FieldName;
        if (required.Length == 0)
            return true;
        var comparison = _search.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return name.Equals(required, comparison);
    }

    private void AddName(DocumentPath path, int offset, int matchLength, string name)
    {
        var hit = new Hit(offset, matchLength) { OnName = true, NameText = name };
        foreach (var step in path.Steps)
            hit.Steps.Add(step);
        _hits.Add(hit);
    }

    private Node Rent()
    {
        if (!_spare.TryPop(out var node))
            return new Node();
        node.Name = null;
        node.Parent = null;
        node.Path = DocumentPath.Root;
        node.Array = false;
        node.Count = 0;
        node.Names?.Clear();
        node.Provisional = false;
        node.HitOrigin = 0;
        return node;
    }

    private void Recycle(Node node)
    {
        if (_spare.Count < 64)
            _spare.Push(node);
    }

    private void Add(DocumentPath path, int offset, int matchLength)
    {
        var hit = new Hit(offset, matchLength);
        foreach (var step in path.Steps)
            hit.Steps.Add(step);
        _hits.Add(hit);
    }

    private void VisitBulk()
    {
        if (!_bulk || _stack.Count == 0)
            return;
        var parent = _stack.Peek();
        if (!parent.Array)
            return;
        var deadline = Stopwatch.GetTimestamp() + Stopwatch.Frequency / 125;
        _visitArray = parent;
        while (Stopwatch.GetTimestamp() < deadline)
        {
            var origin = _hits.Count;
            var take = _parser.TryVisitItem(deadline, this, _search.FindWhat, _needleBytes);
            if (take is JsonParser.ItemTake.Defer or JsonParser.ItemTake.Repeated)
            {
                if (_hits.Count > origin)
                    _hits.RemoveRange(origin, _hits.Count - origin);
                if (take == JsonParser.ItemTake.Repeated)
                    Done = true;
                return;
            }

            if (take == JsonParser.ItemTake.None)
                return;
            parent.Count++;
        }
    }

    void IJsonItem.ValueHit(ReadOnlySpan<DocumentPath.Step> trail, int offset, int length) =>
        Add(ItemPath(trail), offset, length);

    void IJsonItem.NameHit(ReadOnlySpan<DocumentPath.Step> trail, int offset, int length, string name) =>
        AddName(ItemPath(trail), offset, length, name);

    private DocumentPath ItemPath(ReadOnlySpan<DocumentPath.Step> trail)
    {
        var path = _visitArray!.Path.WithIndex(_visitArray.Count + 1);
        for (var i = 0; i < trail.Length; i++)
            path = path.Field(trail[i].Name, trail[i].OneBasedIndex);
        return path;
    }

    private static bool CanBulk(XmlDocument document, TextSearch search)
    {
        if (!document.Source.IsComplete || !search.MatchCase || search.StartsWith || search.EntireCell)
            return false;
        if (!search.ElementName || !search.Value)
            return false;
        if (search.FieldName.Length != 0 || search.ParentElementName.Length != 0
            || search.GrandfatherElementName.Length != 0 || search.ParentAttributeName.Length != 0)
            return false;
        if (search.FindWhat.Length == 0)
            return false;
        foreach (var unit in search.FindWhat)
        {
            if (unit > 127)
                return false;
        }

        return true;
    }

    private sealed class Node
    {
        public string? Name;
        public Node? Parent;
        public DocumentPath Path = DocumentPath.Root;
        public bool Array;
        public int Count;
        public HashSet<string>? Names;
        public bool Provisional;
        public int HitOrigin;
    }
}
