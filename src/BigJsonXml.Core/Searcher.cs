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
    private readonly TextSearch _search;
    private readonly List<Hit> _hits;
    private readonly Stack<SFrame> _stack = new();

    public Searcher(XmlDocument document, TextSearch search, List<Hit> hits)
    {
        _document = document;
        _parser = XmlParser.Create(document);
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
            SearchDisplayedName(root.Path, ev.Name, element: true, container: null);
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
            Parent = parent,
        };
        parent.SawElement = true;
        _stack.Push(frame);
        SearchDisplayedName(frame.Path, display, element: true, container: parent);
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
            var shown = "@" + attr.Name;
            frame.AttributeNames.Add(shown);
            var path = frame.Path.Field(shown);
            SearchDisplayedName(path, shown, element: false, container: frame);
            SearchValue(path, shown, attribute: true, attr.Ns, attr.Local, frame.Scope, [attr.Value], simple: false, container: frame);
        }
    }

    private void SearchOwnText(SFrame frame)
    {
        if (frame.Text.Count == 0)
            return;
        var path = frame.Path.Field("#text");
        SearchValue(path, "#text", attribute: false, "", "#text", frame.Scope, frame.Text, simple: false, container: frame);
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

        SearchValue(frame.Path, frame.Name, attribute: false, frame.Ns, frame.Local, frame.Scope, frame.Text, simple: true, container: frame.Parent);
    }

    private void SearchValue(
        DocumentPath path,
        string name,
        bool attribute,
        string ns,
        string local,
        NsScope scope,
        List<TextPiece> pieces,
        bool simple,
        SFrame? container)
    {
        if (!_search.Value || _search.FindWhat.Length == 0 || !Allows(name) || !Passes(container))
            return;
        var length = _search.FindWhat.Length;
        var dropOnPromote = simple && _search.FieldName.Length > 0;
        ExpandedText.Match(_document, pieces, _search, offset => Add(path, offset, length, simpleText: simple, fieldOnSimple: dropOnPromote));
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

        if (!parent.Pending.Remove(key, out var pending))
            return;
        if (_search.FieldName.Length == 0 || !Allows("#text"))
            return;
        var textPath = parent.Path.Field(name, 1).Field("#text");
        var entry = new SFrame { Name = name, Parent = parent };
        SearchValue(textPath, "#text", attribute: false, "", "#text", pending.Scope, pending.Text, simple: false, container: entry);
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
        if (container is null)
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

    private sealed class SFrame
    {
        public DocumentPath Path = DocumentPath.Root;
        public NsScope Scope = NsScope.Empty;
        public string Name = "";
        public string Ns = "";
        public string Local = "";
        public int? Index;
        public SFrame? Parent;
        public bool SawAttribute;
        public bool SawElement;
        public bool IsSimple => !SawAttribute && !SawElement;
        public List<TextPiece> Text { get; } = [];
        public List<string> AttributeNames { get; } = [];
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
