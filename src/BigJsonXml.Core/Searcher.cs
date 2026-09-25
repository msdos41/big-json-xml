using BigJsonXml.Xml;

namespace BigJsonXml;

internal enum SearchStep
{
    Work,
    NeedMore,
    Idle,
}

internal sealed class Searcher
{
    private readonly XmlDocument _document;
    private readonly XmlParser _parser;
    private readonly Query _query;
    private readonly List<Hit> _hits;
    private readonly Stack<SFrame> _stack = new();

    public Searcher(XmlDocument document, string query, List<Hit> hits)
    {
        _document = document;
        _parser = XmlParser.Create(document);
        _query = Query.Parse(query);
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
            default:
                return SearchStep.Work;
        }
    }

    private void OnStart(XmlEvent ev)
    {
        if (_stack.Count == 0)
        {
            var root = new SFrame
            {
                Path = DocumentPath.Root,
                Scope = ev.Scope,
                Name = ev.Name,
                Ns = ev.Ns,
                Local = ev.Local,
            };
            if (ev.Attributes.Count > 0)
                root.SawAttribute = true;
            _stack.Push(root);
            SearchAttributes(root, ev);
            if (ev.Empty)
                OnEnd();
            return;
        }

        var parent = _stack.Peek();
        var key = FieldKey.Elem(ev.Ns, ev.Local);
        var count = parent.Counts.GetValueOrDefault(key) + 1;
        parent.Counts[key] = count;
        if (count == 1)
            parent.Names[key] = ev.Name;
        var display = parent.Names[key];
        if (count == 2)
            Promote(parent, key, display);
        var index = count >= 2 ? count : (int?)null;
        var frame = new SFrame
        {
            Path = parent.Path.Field(display, index),
            Scope = ev.Scope,
            Name = display,
            Ns = ev.Ns,
            Local = ev.Local,
            Index = index,
            SawAttribute = ev.Attributes.Count > 0,
        };
        parent.SawElement = true;
        _stack.Push(frame);
        SearchAttributes(frame, ev);
        if (ev.Empty)
            OnEnd();
    }

    private void OnText(XmlEvent ev)
    {
        if (_stack.Count == 0 || !ev.HasText)
            return;
        if (ExpandedText.IsWhitespaceOnly(_document, [ev.Text]))
            return;
        _stack.Peek().Text.Add(ev.Text);
    }

    private void OnEnd()
    {
        if (_stack.Count == 0)
            return;
        var frame = _stack.Pop();
        if (_stack.Count == 0 || !frame.IsSimple || frame.Index is not null)
            SearchOwnText(frame);
        else
            SearchSimple(frame);
    }

    private void SearchAttributes(SFrame frame, XmlEvent ev)
    {
        foreach (var attr in ev.Attributes)
        {
            var path = frame.Path.Field("@" + attr.Name);
            SearchValue(path, "@" + attr.Name, attribute: true, attr.Ns, attr.Local, frame.Scope, [attr.Value], simple: false);
        }
    }

    private void SearchOwnText(SFrame frame)
    {
        if (frame.Text.Count == 0)
            return;
        var path = frame.Path.Field("#text");
        SearchValue(path, "#text", attribute: false, "", "#text", frame.Scope, frame.Text, simple: false);
    }

    private void SearchSimple(SFrame frame)
    {
        if (frame.Text.Count == 0)
            return;
        if (frame.Index is null && _stack.Count > 0)
        {
            _stack.Peek().Pending[FieldKey.Elem(frame.Ns, frame.Local)] = new PendingSimple
            {
                Text = [..frame.Text],
                Scope = frame.Scope,
            };
        }

        SearchValue(frame.Path, frame.Name, attribute: false, frame.Ns, frame.Local, frame.Scope, frame.Text, simple: true);
    }

    private void SearchValue(
        DocumentPath path,
        string name,
        bool attribute,
        string ns,
        string local,
        NsScope scope,
        List<TextPiece> pieces,
        bool simple)
    {
        if (_query.IsText)
        {
            if (_query.Text.Length == 0)
                return;
            ExpandedText.Find(_document, pieces, _query.Text, offset => Add(path, offset, pieces, simpleText: simple, fieldOnSimple: false));
            return;
        }

        if (_query.Local.Length == 0 || !_query.Matches(attribute, ns, local, scope))
            return;
        if (_query.Exact)
        {
            if (ExpandedText.EqualsExact(_document, pieces, _query.Value))
                Add(path, 0, pieces, simpleText: false, fieldOnSimple: simple);
            return;
        }

        ExpandedText.Find(_document, pieces, _query.Value, offset => Add(path, offset, pieces, simpleText: false, fieldOnSimple: simple));
    }

    private void Add(DocumentPath path, int offset, List<TextPiece> pieces, bool simpleText, bool fieldOnSimple)
    {
        var snippetStart = Math.Max(0, offset - 16);
        var snippet = ExpandedText.Slice(_document, pieces, snippetStart, 48, out _);
        var hit = new Hit(offset, snippet)
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
        for (var i = _hits.Count - 1; i >= 0; i--)
        {
            var hit = _hits[i];
            if (hit.Steps.Count <= depth)
                continue;
            var step = hit.Steps[depth];
            if (step.Name != name || step.OneBasedIndex is not null)
                continue;
            if (hit.FieldOnSimple && hit.Steps.Count == depth + 1)
            {
                _hits.RemoveAt(i);
                continue;
            }

            hit.Steps[depth] = step with { OneBasedIndex = 1 };
            if (hit.SimpleText && hit.Steps.Count == depth + 1)
            {
                hit.Steps.Add(new DocumentPath.Step("#text", null));
                hit.SimpleText = false;
            }
        }

        if (parent.Pending.Remove(key, out var pending) && !_query.IsText && _query.Local == "#text")
        {
            var path = parent.Path.Field(name, 1).Field("#text");
            SearchValue(path, "#text", false, "", "#text", pending.Scope, pending.Text, simple: false);
        }
    }

    private sealed class SFrame
    {
        public DocumentPath Path = DocumentPath.Root;
        public NsScope Scope = NsScope.Empty;
        public string Name = "";
        public string Ns = "";
        public string Local = "";
        public int? Index;
        public bool SawAttribute;
        public bool SawElement;
        public bool IsSimple => !SawAttribute && !SawElement;
        public List<TextPiece> Text { get; } = [];
        public Dictionary<FieldKey, int> Counts { get; } = [];
        public Dictionary<FieldKey, string> Names { get; } = [];
        public Dictionary<FieldKey, PendingSimple> Pending { get; } = [];
    }

    private sealed class PendingSimple
    {
        public List<TextPiece> Text { get; init; } = [];
        public NsScope Scope { get; init; } = NsScope.Empty;
    }
}

internal sealed class Query
{
    public bool IsText { get; private init; }
    public bool Exact { get; private init; }
    public bool Attribute { get; private init; }
    public string Prefix { get; private init; } = "";
    public string Local { get; private init; } = "";
    public string Value { get; private init; } = "";
    public string Text { get; private init; } = "";

    public static Query Parse(string query)
    {
        var equals = query.IndexOf('=');
        var tilde = query.IndexOf('~');
        var op = equals < 0 ? tilde : tilde < 0 ? equals : Math.Min(equals, tilde);
        if (op < 0)
            return new Query { IsText = true, Text = query };
        var name = query[..op].Trim();
        var raw = query[(op + 1)..];
        var index = 0;
        while (index < raw.Length && raw[index] is ' ' or '\t' or '\r' or '\n')
            index++;
        var attribute = name.StartsWith('@');
        var body = attribute ? name[1..] : name;
        var prefix = "";
        var local = body;
        var colon = body.IndexOf(':');
        if (colon > 0 && body.LastIndexOf(':') == colon && colon < body.Length - 1)
        {
            prefix = body[..colon];
            local = body[(colon + 1)..];
        }

        return new Query
        {
            Exact = query[op] == '=',
            Attribute = attribute,
            Prefix = prefix,
            Local = local,
            Value = raw[index..],
        };
    }

    public bool Matches(bool attribute, string ns, string local, NsScope scope)
    {
        if (Local == "#text")
            return !attribute && local == "#text";
        if (Attribute != attribute || !string.Equals(Local, local, StringComparison.Ordinal))
            return false;
        if (attribute)
        {
            if (Prefix.Length == 0)
                return ns.Length == 0;
            return scope.TryResolve(Prefix, out var uri) && uri == ns;
        }

        if (Prefix.Length == 0)
            return ns.Length == 0 || (scope.Default.Length > 0 && ns == scope.Default);
        return scope.TryResolve(Prefix, out var resolved) && resolved == ns;
    }
}
