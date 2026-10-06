using BigJsonXml.Xml;

namespace BigJsonXml.Json;

/// <summary>
/// Text search over a JSON document. Values are the shown literals. Field names use the
/// element-name switch. Attribute names and parent attributes match nothing.
/// </summary>
internal sealed class JsonSearcher : IScanSearch
{
    private readonly XmlDocument _document;
    private readonly JsonParser _parser;
    private readonly TextSearch _search;
    private readonly List<Hit> _hits;
    private readonly Stack<Node> _stack = new();
    private string _pending = "";

    public JsonSearcher(XmlDocument document, TextSearch search, List<Hit> hits)
    {
        _document = document;
        _parser = JsonParser.Document(document);
        _search = search;
        _hits = hits;
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
            case JsonEventKind.Done:
            case JsonEventKind.Refuse:
                Done = true;
                return SearchStep.Idle;
            case JsonEventKind.ObjectStart:
                OnObject();
                return SearchStep.Work;
            case JsonEventKind.ObjectEnd:
            case JsonEventKind.ArrayEnd:
                if (_stack.Count > 0)
                    _stack.Pop();
                return SearchStep.Work;
            case JsonEventKind.ArrayStart:
                OnArray();
                return SearchStep.Work;
            case JsonEventKind.Key:
                OnKey(ev);
                return SearchStep.Work;
            case JsonEventKind.Scalar:
                OnScalar(ev);
                return SearchStep.Work;
            default:
                return SearchStep.Work;
        }
    }

    private void OnObject()
    {
        if (_stack.Count == 0)
        {
            _stack.Push(new Node { Path = DocumentPath.Root });
            return;
        }

        var parent = _stack.Peek();
        if (parent.Array)
        {
            parent.Count++;
            _stack.Push(new Node
            {
                Parent = parent,
                Path = parent.Path.WithIndex(parent.Count),
            });
            return;
        }

        _stack.Push(new Node
        {
            Name = _pending,
            Parent = parent,
            Path = parent.Path.Field(_pending),
        });
    }

    private void OnArray()
    {
        if (_stack.Count == 0)
        {
            var path = DocumentPath.Root.Field("#value");
            SearchName(path, "#value", null);
            _stack.Push(new Node { Name = "#value", Array = true, Path = path });
            return;
        }

        var parent = _stack.Peek();
        if (parent.Array)
        {
            parent.Count++;
            var path = parent.Path.WithIndex(parent.Count).Field("#value");
            SearchName(path, "#value", parent);
            _stack.Push(new Node { Name = "#value", Array = true, Parent = parent, Path = path });
            return;
        }

        _stack.Push(new Node
        {
            Name = _pending,
            Array = true,
            Parent = parent,
            Path = parent.Path.Field(_pending),
        });
    }

    private void OnKey(JsonEvent ev)
    {
        _pending = ev.Name;
        var container = _stack.Peek();
        SearchName(container.Path.Field(ev.Name), ev.Name, container);
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

        SearchLiteral(parent.Path.Field(_pending), _pending, ev, parent);
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
        ExpandedText.Match(_document, [piece], _search, offset => Add(path, offset, length));
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

    private void Add(DocumentPath path, int offset, int matchLength)
    {
        var hit = new Hit(offset, matchLength);
        foreach (var step in path.Steps)
            hit.Steps.Add(step);
        _hits.Add(hit);
    }

    private sealed class Node
    {
        public string? Name;
        public Node? Parent;
        public DocumentPath Path = DocumentPath.Root;
        public bool Array;
        public int Count;
    }
}
