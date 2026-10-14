using System.Buffers;
using System.Text;

namespace BigJsonXml.Xml;

internal enum XmlEventKind
{
    NeedMore,
    Start,
    End,
    Text,
    Continue,
    Flat,
    Done,
    Error,
}

internal sealed class XmlAttr
{
    public string Prefix = "";
    public string Local = "";
    public string Ns = "";
    public string Name = "";
    public TextPiece Value;
}

internal sealed class XmlEvent
{
    public XmlEventKind Kind;
    public string Prefix = "";
    public string Local = "";
    public string Ns = "";
    public string Name = "";
    public bool Empty;
    public long ByteStart;
    public long ByteEnd;
    public NsScope Scope = NsScope.Empty;
    public List<XmlAttr> Attributes = [];
    public List<FlatField> FlatFields = [];
    public bool SameShape;
    public bool SearchMiss;
    public bool MissAttribute;
    public TextPiece Text;
    public bool HasText;
    public bool TrustedText;
    public string Message = "";
    public int Line = 1;
    public int Column = 1;
    public long ErrorByte;

    public static XmlEvent More() => new() { Kind = XmlEventKind.NeedMore };

    public static XmlEvent EndOfDocument() => new() { Kind = XmlEventKind.Done };
}

internal sealed class XmlParser
{
    private readonly XmlDocument _document;
    private readonly bool _fragment;
    private readonly Dictionary<string, string> _intern = new(StringComparer.Ordinal);
    private readonly Stack<string> _open = new();
    private readonly Stack<NsScope> _scopes = new();
    private readonly Stack<CharInput> _inputs = new();

    private long _byte;
    private long _limit = long.MaxValue;
    private int _line = 1;
    private int _column = 1;
    private bool _afterCr;
    private bool _bootstrapped;
    private Phase _phase = Phase.Prologue;
    private NsScope _scope = NsScope.Empty;
    private string? _error;
    private long _errorByte;
    private int _errorLine = 1;
    private int _errorColumn = 1;
    private bool _hasLow;
    private char _low;
    private Checkpoint _mark;
    private Signal _signal;
    private byte[]? _window;
    private long _windowAt;
    private int _windowCount;
    private byte[]? _skipWindow;
    private long _textFrom = -1;
    private bool _textNonWs;
    private int _textLine = 1;
    private int _textColumn = 1;
    private bool _textAfterCr;
    private int _brackets;
    private readonly List<RawAttr> _raw = [];
    private readonly List<XmlAttr> _attrs = [];
    private readonly HashSet<(string Ns, string Local)> _seen = [];
    private readonly Stack<XmlAttr> _attrPool = new();
    private readonly XmlEvent _event = new();
    private readonly Dictionary<int, string[]> _names = new();
    private readonly Dictionary<int, HashSet<string>> _flatAtDepth = new();
    private readonly Dictionary<(string Parent, string Child), ElementShape> _shapeCache = new();
    private readonly LinkedList<(string Parent, string Child)> _fifoOrder = new();
    private readonly Dictionary<string, HashSet<string>> _predictedByParent = new(StringComparer.Ordinal);
    private readonly HashSet<(string Parent, string Child)> _shapeBlacklist = new();
    private readonly Dictionary<string, string> _atColumns = new(StringComparer.Ordinal);
    private readonly List<FlatField> _flatFields = [];
    private readonly List<string> _flatShape = [];
    private readonly List<FieldKey> _shapeKeys = [];
    private readonly List<string> _shapeBuild = [];
    private readonly List<(int At, int Len)> _attrSpans = [];
    private int _shapeIndex;
    private bool _sameShape;
    private bool _haveShape;
    private string _hashText = "#text";
    private byte[] _searchNeedle = [];

    private static readonly SearchValues<byte> StopBytes = SearchValues.Create("<&"u8);
    private static readonly SearchValues<byte> SpaceBytes = SearchValues.Create(" \t\r\n"u8);
    private static readonly SearchValues<byte> TagMarks = SearchValues.Create("\"'>/"u8);

    private XmlParser(XmlDocument document, bool fragment)
    {
        _document = document;
        _fragment = fragment;
    }

    public static XmlParser Create(XmlDocument document) => new(document, false);

    internal long FileByte => _byte;
    internal long StartCount { get; private set; }
    internal int ShapeCacheCount => _shapeCache.Count;

    public static XmlParser Fragment(XmlDocument document, NsScope scope, long offset, long limit)
    {
        var parser = new XmlParser(document, true)
        {
            _bootstrapped = true,
            _scope = scope,
            _byte = offset,
            _limit = limit,
            _phase = Phase.Content,
        };
        return parser;
    }

    public XmlEvent Read()
    {
        if (_error is not null)
            return ErrorEvent();
        ReclaimAttrs();
        var complete = _document.Source.IsComplete;
        if (!complete)
            _mark = Save();
        _signal = Signal.None;
        var ev = ReadCore();
        if (_signal == Signal.NeedMore)
        {
            if (!complete)
                Restore(_mark);
            return XmlEvent.More();
        }

        return _error is null ? ev : ErrorEvent();
    }

    private void ReclaimAttrs()
    {
        foreach (var attr in _attrs)
            _attrPool.Push(attr);
        _attrs.Clear();
        _raw.Clear();
    }

    private XmlEvent ReadCore()
    {
        if (!_bootstrapped && !Bootstrap())
            return SignalEvent();
        return _phase switch
        {
            Phase.Prologue => ReadPrologue(),
            Phase.Content => ReadContent(),
            Phase.Epilogue => ReadEpilogue(),
            _ => XmlEvent.EndOfDocument(),
        };
    }

    private XmlEvent ReadPrologue()
    {
        while (true)
        {
            if (!SkipMisc())
                return SignalEvent();
            if (!TryPeek(out var c))
            {
                if (_signal == Signal.NeedMore)
                    return SignalEvent();
                return Fail("The document has no root element.");
            }

            if (c != '<')
                return Fail("The document has no root element.");
            var save = Save();
            if (!ReadChar(out _))
                return SignalEvent();
            if (!TryPeek(out var next))
            {
                Restore(save);
                return SignalEvent();
            }

            if (next == '!')
            {
                if (!ReadChar(out _))
                    return SignalEvent();
                if (!TryPeek(out var kind))
                {
                    Restore(save);
                    return SignalEvent();
                }

                if (kind == 'D')
                {
                    Restore(save);
                    if (!ParseDoctype())
                        return SignalEvent();
                    continue;
                }

                Restore(save);
                if (!SkipMisc())
                    return SignalEvent();
                continue;
            }

            Restore(save);
            return ReadStartTag();
        }
    }

    private XmlEvent ReadEpilogue()
    {
        if (!SkipMisc())
            return SignalEvent();
        if (!TryPeek(out var c))
        {
            if (_signal == Signal.NeedMore)
                return SignalEvent();
            _phase = Phase.Finished;
            return XmlEvent.EndOfDocument();
        }

        return c == '<'
            ? Fail("The document has more than one root element.")
            : Fail("The document has text after the root element.");
    }

    private XmlEvent ReadContent()
    {
        while (true)
        {
            if (_inputs.Count == 0 && _byte >= _limit)
                return XmlEvent.EndOfDocument();
            if (!TryPeek(out var c))
            {
                if (_signal == Signal.NeedMore)
                    return SignalEvent();
                return _open.Count == 0
                    ? XmlEvent.EndOfDocument()
                    : Fail("The document ends before a tag is closed.");
            }

            if (c == '<')
                return ReadMarkup();
            if (c == '&' && TryConsumeMarkupEntity())
            {
                if (_signal != Signal.None || _error is not null)
                    return SignalEvent();
                continue;
            }

            var text = ReadText();
            if (_signal != Signal.None || _error is not null)
                return SignalEvent();
            if (text is null)
                continue;
            return text;
        }
    }

    internal List<FlatField> FlatFields => _flatFields;

    internal bool FlatSameShape { get; private set; }

    // Search only needs the element bytes. The scan keeps the column shape.
    internal bool OmitFlatShape { get; set; }

    internal void UseSearchNeedle(byte[] needle) => _searchNeedle = needle;

    // Following same-name empty tags whose bytes do not hold the needle. A hit, a body, or an entity stays for the next read.
    internal int CountFlatMisses(string name, ReadOnlySpan<byte> needle)
    {
        if (!OmitFlatShape || name.Length is 0 or > 128 || needle.Length == 0 || _inputs.Count > 0 || _hasLow)
            return 0;
        foreach (var unit in name)
        {
            if (unit > 127)
                return 0;
        }

        if (!TrySpan(out var span, out _))
            return 0;

        Span<byte> literal = stackalloc byte[name.Length];
        for (var n = 0; n < name.Length; n++)
            literal[n] = (byte)name[n];

        var skipped = 0;
        var cursor = 0;
        while ((uint)cursor < (uint)span.Length)
        {
            var i = cursor;
            while ((uint)i < (uint)span.Length && IsXmlSpace(span[i]))
                i++;
            if ((uint)(i + literal.Length + 1) >= (uint)span.Length || span[i] != (byte)'<')
                break;
            if (!span.Slice(i + 1, literal.Length).SequenceEqual(literal))
                break;
            var after = i + 1 + literal.Length;
            if ((uint)after >= (uint)span.Length)
                break;
            var mark = span[after];
            if (!IsXmlSpace(mark) && mark != (byte)'>' && mark != (byte)'/')
                break;
            if (!TrySkipSimple(span, after, out var consumed) || consumed <= i)
                break;
            if (span.Slice(i, consumed - i).IndexOf(needle) >= 0)
                break;
            cursor = consumed;
            skipped++;
        }

        if (skipped > 0)
            _byte += cursor;
        return skipped;
    }

    internal bool TryNextFlat(string name, out long start, out long end)
    {
        start = 0;
        end = 0;
        var before = _byte;
        var signal = _signal;
        if (!SkipSpaceBytes() || !NextOpens(name) || !TryFlatElement(out var ev) || ev.Name != name)
        {
            _byte = before;
            _signal = signal;
            return false;
        }

        start = ev.ByteStart;
        end = ev.ByteEnd;
        return true;
    }

    private bool NextOpens(string name)
    {
        if (!TrySpan(out var span, out _) || span.Length < name.Length + 2 || span[0] != (byte)'<')
            return false;
        for (var i = 0; i < name.Length; i++)
        {
            if (span[i + 1] != (byte)name[i])
                return false;
        }

        return IsXmlSpace(span[name.Length + 1]) || span[name.Length + 1] is (byte)'>' or (byte)'/';
    }

    internal void PreferFlat(string name)
    {
        if (_open.Count == 0)
            return;
        // The next sibling is read with only this list's ancestors still open.
        // Promotion often runs while a descendant start is already on the stack.
        var depth = OpenDepthForNext(name);
        if (!_flatAtDepth.TryGetValue(depth, out var names))
        {
            names = new HashSet<string>(StringComparer.Ordinal);
            _flatAtDepth[depth] = names;
        }

        names.Add(name);

        var parentName = ParentNameFor(name);
        if (parentName is not null)
            RegisterPrediction(parentName, name);
    }

    private string? ParentNameFor(string name)
    {
        var found = false;
        foreach (var open in _open)
        {
            if (found)
                return open;
            if (open == name)
                found = true;
        }

        if (found)
            return null;

        return _open.Count > 0 ? _open.Peek() : null;
    }

    private void RegisterPrediction(string parentName, string childName)
    {
        var key = (parentName, childName);
        if (_shapeBlacklist.Contains(key))
            return;
        if (_shapeCache.ContainsKey(key))
            return;

        if (_fifoOrder.Count >= 512)
        {
            var oldest = _fifoOrder.First!.Value;
            _fifoOrder.RemoveFirst();
            _shapeCache.Remove(oldest);
            if (_predictedByParent.TryGetValue(oldest.Parent, out var children))
            {
                children.Remove(oldest.Child);
                if (children.Count == 0)
                    _predictedByParent.Remove(oldest.Parent);
            }
        }

        _fifoOrder.AddLast(key);
        var shape = new ElementShape { Parent = parentName, Child = childName };
        if (_haveShape && _flatShape.Count > 0)
        {
            shape.FlatShape = _flatShape.ToArray();
            shape.ShapeKeys = _shapeKeys.ToArray();
        }
        _shapeCache[key] = shape;

        if (!_predictedByParent.TryGetValue(parentName, out var list))
        {
            list = new HashSet<string>(StringComparer.Ordinal);
            _predictedByParent[parentName] = list;
        }
        list.Add(childName);
    }

    private void RemovePrediction((string Parent, string Child) key)
    {
        _shapeCache.Remove(key);
        _fifoOrder.Remove(key);
        if (_predictedByParent.TryGetValue(key.Parent, out var list))
        {
            list.Remove(key.Child);
            if (list.Count == 0)
                _predictedByParent.Remove(key.Parent);
        }
    }

    internal void Dispose()
    {
        _shapeCache.Clear();
        _fifoOrder.Clear();
        _predictedByParent.Clear();
        _shapeBlacklist.Clear();
        _flatAtDepth.Clear();
        _names.Clear();
        _intern.Clear();
        _window = null;
        _skipWindow = null;
    }

    private int OpenDepthForNext(string name)
    {
        var below = 0;
        var seen = false;
        foreach (var open in _open)
        {
            if (!seen)
            {
                if (open == name)
                    seen = true;
                continue;
            }

            below++;
        }

        return seen ? below : _open.Count;
    }

    private XmlEvent ReadMarkup()
    {
        if (TryFlatElement(out var flat))
            return flat;
        if (_searchNeedle.Length > 0 && _inputs.Count == 0 && !_hasLow && _document.Encoding.CodePage == 65001
            && TrySearchMiss(out var missed))
            return missed;
        if (_inputs.Count == 0 && !_hasLow && _document.Encoding.CodePage == 65001 && TryPlainMarkup(out var plain))
            return plain;
        var startByte = _byte;
        var markup = Save();
        if (!ReadChar(out _))
            return SignalEvent();
        if (!TryPeek(out var c))
            return _signal == Signal.NeedMore ? SignalEvent() : Fail("The document ends inside a tag.");
        if (c == '/')
            return ReadEndTag(startByte);
        if (c == '!')
            return ReadBang();
        if (c == '?')
        {
            // '<' is already consumed. The instruction reader expects the whole '<?'.
            Restore(markup);
            if (!SkipProcessingInstruction(declare: false))
                return SignalEvent();
            return ReadContent();
        }

        var tag = ReadStartTagAt(startByte);
        return _signal != Signal.None || _error is not null ? SignalEvent() : tag;
    }

    private XmlEvent ReadBang()
    {
        var save = Save();
        if (!Expect("!"))
            return SignalEvent();
        if (!TryPeek(out var c))
        {
            Restore(save);
            return _signal == Signal.NeedMore ? SignalEvent() : Fail("The document ends inside a tag.");
        }

        if (c == '-')
        {
            Restore(save);
            // The '<' was consumed by ReadMarkup. Put the comment reader on the '!'.
            if (!SkipCommentBody())
                return SignalEvent();
            return ReadContent();
        }

        if (c == '[')
        {
            if (!Expect("[CDATA["))
                return SignalEvent();
            return ReadCData();
        }

        return Fail("This declaration is not allowed here.");
    }

    private XmlEvent ReadCData()
    {
        var entity = CurrentEntity();
        var start = Cursor();
        var tail = "";
        var marks = new long[3];
        var seen = 0;
        while (true)
        {
            var at = Cursor();
            if (!ReadChar(out var c))
                return _signal == Signal.Eof ? Fail("A CDATA section is not closed.") : SignalEvent();
            marks[seen % 3] = at;
            seen++;
            tail = (tail + c)[^Math.Min(3, tail.Length + 1)..];
            if (tail == "]]>")
            {
                var end = marks[(seen - 3) % 3];
                return TextEvent(entity < 0
                    ? TextPiece.Bytes(start, end, cdata: true)
                    : new TextPiece { EntityId = entity, Start = start, End = end, CData = true }, trusted: false);
            }
        }
    }

    private XmlEvent? ReadText()
    {
        if (_inputs.Count > 0 || _hasLow || _document.Encoding.CodePage != 65001)
            return ReadTextSlow();
        return ReadTextBytes();
    }

    private XmlEvent? ReadTextSlow()
    {
        var entity = CurrentEntity();
        var depth = _inputs.Count;
        var start = Cursor();
        var previous = '\0';
        var previous2 = '\0';
        while (true)
        {
            if (_inputs.Count != depth)
                break;
            if (depth > 0 && _inputs.Peek().Position >= _inputs.Peek().Text.Length)
                break;
            var save = Save();
            if (!TryPeek(out var c))
            {
                if (_signal == Signal.NeedMore)
                    return SignalEvent();
                break;
            }

            if (c == '<')
                break;
            if (c == '&')
            {
                Restore(save);
                var before = Cursor();
                if (TryConsumeMarkupEntity())
                {
                    if (_signal != Signal.None || _error is not null)
                        return SignalEvent();
                    if (before == start)
                        return null;
                    return TextEvent(entity < 0 ? TextPiece.Bytes(start, before) : TextPiece.Chars(entity, start, before), trusted: false);
                }

                if (_signal == Signal.NeedMore || _error is not null)
                    return SignalEvent();
            }

            if (!ReadChar(out var read))
                return SignalEvent();
            if (previous2 == ']' && previous == ']' && read == '>')
                return Fail("']]>' is not allowed in text.");
            previous2 = previous;
            previous = read;
            if (read == '&' && !ReadReference(attribute: false, out _))
                return SignalEvent();
        }

        var end = _inputs.Count == depth ? Cursor() : start;
        if (end == start)
            return null;
        return TextEvent(entity < 0 ? TextPiece.Bytes(start, end) : TextPiece.Chars(entity, start, end), trusted: false);
    }

    private XmlEvent ReadStartTag()
    {
        var start = _inputs.Count == 0 ? _byte : 0;
        if (!ReadChar(out var c) || c != '<')
            return SignalEvent();
        return ReadStartTagAt(start);
    }

    private XmlEvent ReadStartTagAt(long byteStart)
    {
        if (!ReadQName(out var prefix, out var local))
            return SignalEvent();
        _raw.Clear();
        var raw = _raw;
        while (true)
        {
            if (!SkipSpaceAllowEmpty())
                return SignalEvent();
            if (!TryPeek(out var c))
                return _signal == Signal.NeedMore ? SignalEvent() : Fail("The document ends inside a tag.");
            if (c is '/' or '>')
                break;
            if (!ReadQName(out var ap, out var al))
                return SignalEvent();
            if (!SkipSpaceAllowEmpty())
                return SignalEvent();
            if (!TryPeek(out var after))
                return _signal == Signal.NeedMore ? SignalEvent() : Fail("The document ends inside a tag.");
            TextPiece piece;
            if (after == '=')
            {
                if (!Expect("=") || !SkipSpaceAllowEmpty() || !ReadQuoted(out piece))
                    return SignalEvent();
            }
            else
            {
                var entity = CurrentEntity();
                var at = Cursor();
                piece = entity < 0
                    ? TextPiece.Bytes(at, at, attribute: true)
                    : TextPiece.Chars(entity, at, at, attribute: true);
            }

            raw.Add(new RawAttr(ap, al, piece));
        }

        if (!ReadChar(out var end))
            return SignalEvent();
        var empty = false;
        if (end == '/')
        {
            empty = true;
            if (!Expect(">"))
                return SignalEvent();
        }
        else if (end != '>')
        {
            return Fail("A tag is not closed.");
        }

        return FinishStart(prefix, local, empty, byteStart);
    }

    private XmlEvent FinishStart(string prefix, string local, bool empty, long byteStart)
    {
        var raw = _raw;
        var scope = _scope;
        foreach (var attr in raw)
        {
            if (attr.Prefix.Length == 0 && attr.Local == "xmlns")
                scope = scope.WithDefault(ExpandLiteral(attr.Value));
            else if (attr.Prefix == "xmlns")
            {
                if (attr.Local is "xmlns" or "xml")
                    return Fail("This namespace declaration is not allowed.");
                var uri = ExpandLiteral(attr.Value);
                if (attr.Local == "xml" && uri != NsScope.XmlNamespace)
                    return Fail("The xml prefix cannot be rebound.");
                scope = scope.WithPrefix(attr.Local, uri);
            }
        }

        if (prefix.Length == 0)
        {
            // default namespace
        }
        else if (!scope.TryResolve(prefix, out _))
        {
            return Fail($"The prefix '{prefix}' is not declared.");
        }

        _seen.Clear();
        foreach (var attr in raw)
        {
            string ns;
            string display;
            if (attr.Prefix.Length == 0 && attr.Local == "xmlns")
            {
                ns = NsScope.XmlnsNamespace;
                display = "xmlns";
            }
            else if (attr.Prefix == "xmlns")
            {
                ns = NsScope.XmlnsNamespace;
                display = "xmlns:" + attr.Local;
            }
            else if (attr.Prefix.Length == 0)
            {
                ns = "";
                display = attr.Local;
            }
            else if (!scope.TryResolve(attr.Prefix, out ns!))
            {
                return Fail($"The prefix '{attr.Prefix}' is not declared.");
            }
            else
            {
                display = attr.Prefix + ":" + attr.Local;
            }

            var identityLocal = attr.Prefix == "xmlns" ? attr.Local : attr.Local;
            if (!_seen.Add((ns, attr.Prefix == "xmlns" ? attr.Local : attr.Local == "xmlns" && attr.Prefix.Length == 0 ? "xmlns" : attr.Local)))
                return Fail("An attribute is repeated.");
            var xmlAttr = _attrPool.Count > 0 ? _attrPool.Pop() : new XmlAttr();
            xmlAttr.Prefix = attr.Prefix;
            xmlAttr.Local = identityLocal;
            xmlAttr.Ns = ns;
            xmlAttr.Name = Intern(display);
            xmlAttr.Value = attr.Value;
            _attrs.Add(xmlAttr);
        }

        var elementNs = prefix.Length == 0
            ? scope.Default
            : scope.TryResolve(prefix, out var resolved) ? resolved : "";
        var name = prefix.Length == 0 ? local : prefix + ":" + local;
        var ev = Prepare(XmlEventKind.Start);
        StartCount++;
        ev.Prefix = Intern(prefix);
        ev.Local = Intern(local);
        ev.Ns = elementNs;
        ev.Name = Intern(name);
        ev.Empty = empty;
        ev.ByteStart = byteStart;
        ev.ByteEnd = _inputs.Count == 0 ? _byte : byteStart;
        ev.Scope = scope;
        ev.Attributes = _attrs;
        if (!empty)
        {
            _scopes.Push(_scope);
            _open.Push(name);
            _scope = scope;
            if (_phase == Phase.Prologue)
                _phase = Phase.Content;
        }
        else if (_open.Count == 0 && !_fragment)
        {
            _phase = Phase.Epilogue;
        }
        else if (_phase == Phase.Prologue)
        {
            _phase = Phase.Content;
        }

        return ev;
    }

    private XmlEvent ReadEndTag(long byteStart)
    {
        if (!Expect("/"))
            return SignalEvent();
        if (!ReadQName(out var prefix, out var local))
            return SignalEvent();
        if (!SkipSpaceAllowEmpty() || !Expect(">"))
            return SignalEvent();
        var name = prefix.Length == 0 ? local : prefix + ":" + local;
        if (_open.Count == 0 || _open.Peek() != name)
            return Fail(_open.Count == 0
                ? "An end tag has no matching start tag."
                : $"The end tag '{name}' does not match '{_open.Peek()}'.");
        DropFlat();
        _open.Pop();
        _scope = _scopes.Pop();
        if (_open.Count == 0 && !_fragment)
            _phase = Phase.Epilogue;
        var ev = Prepare(XmlEventKind.End);
        ev.Prefix = prefix;
        ev.Local = local;
        ev.Name = name;
        ev.ByteStart = byteStart;
        ev.ByteEnd = _inputs.Count == 0 ? _byte : byteStart;
        return ev;
    }

    private bool ParseDoctype()
    {
        if (!Expect("<!DOCTYPE"))
            return false;
        if (!SkipRequiredSpace() || !ReadQName(out _, out _))
            return false;
        if (!SkipSpaceAllowEmpty())
            return false;
        if (TryPeek(out var c) && (StartsWith("SYSTEM") || StartsWith("PUBLIC")))
        {
            if (!SkipExternalId() || !SkipSpaceAllowEmpty())
                return false;
        }

        if (TryPeek(out c) && c == '[')
        {
            if (!ReadChar(out _))
                return false;
            if (!ParseSubset())
                return false;
            if (!Expect("]"))
                return false;
        }

        return SkipSpaceAllowEmpty() && Expect(">");
    }

    private bool ParseSubset()
    {
        while (true)
        {
            if (!SkipSpaceAllowEmpty())
                return false;
            if (!TryPeek(out var c))
                return _signal != Signal.NeedMore && Fail("The document ends inside the DTD.") is XmlEvent && false;
            if (c == ']')
                return true;
            if (c == '%')
            {
                if (!ExpandParameterEntity())
                    return false;
                continue;
            }

            if (c != '<')
                return Fail("The DTD is not well-formed.") is XmlEvent && false;
            if (StartsWith("<!ENTITY"))
            {
                if (!ParseEntityDeclaration())
                    return false;
                continue;
            }

            if (StartsWith("<!--"))
            {
                if (!Expect("<"))
                    return false;
                if (!SkipCommentBody())
                    return false;
                continue;
            }

            if (StartsWith("<?"))
            {
                if (!SkipProcessingInstruction(declare: false))
                    return false;
                continue;
            }

            if (!SkipDeclaration())
                return false;
        }
    }

    private bool ParseEntityDeclaration()
    {
        if (!Expect("<!ENTITY"))
            return false;
        if (!SkipRequiredSpace())
            return false;
        if (TryPeek(out var mark) && mark == '%')
        {
            if (!ReadChar(out _) || !SkipRequiredSpace() || !ReadNmtoken(out var peName))
                return false;
            if (!SkipRequiredSpace())
                return false;
            if (StartsWith("SYSTEM") || StartsWith("PUBLIC"))
            {
                _document.Entities.Add(new EntityDef { Name = "%" + peName, External = true });
                return SkipExternalId() && SkipSpaceAllowEmpty() && Expect(">");
            }

            if (!ReadQuotedLiteral(out var value))
                return false;
            _document.Entities.Add(new EntityDef { Name = "%" + peName, Replacement = value, Markup = value.Contains('<') });
            return SkipSpaceAllowEmpty() && Expect(">");
        }

        if (!ReadNmtoken(out var name))
            return false;
        if (!SkipRequiredSpace())
            return false;
        if (StartsWith("SYSTEM") || StartsWith("PUBLIC"))
        {
            _document.Entities.Add(new EntityDef { Name = name, External = true });
            return SkipExternalId(optionalNData: true) && SkipSpaceAllowEmpty() && Expect(">");
        }

        if (!ReadQuotedLiteral(out var replacement, allowLessThan: true))
            return false;
        _document.Entities.Add(new EntityDef
        {
            Name = name,
            Replacement = replacement,
            Markup = replacement.Contains('<'),
        });
        return SkipSpaceAllowEmpty() && Expect(">");
    }

    private bool ExpandParameterEntity()
    {
        if (!Expect("%"))
            return false;
        if (!ReadNmtoken(out var name) || !Expect(";"))
            return false;
        var entity = FindEntity("%" + name);
        if (entity is null)
            return Fail($"The parameter entity '{name}' is not defined.") is XmlEvent && false;
        if (entity.External)
            return Fail("External entities are not allowed.") is XmlEvent && false;
        _inputs.Push(new CharInput { EntityId = EntityIndex(entity), Text = entity.Replacement });
        return true;
    }

    private bool SkipDeclaration()
    {
        if (!ReadChar(out var c) || c != '<')
            return false;
        var quote = '\0';
        while (true)
        {
            if (!ReadChar(out c))
                return _signal == Signal.Eof ? Fail("The document ends inside the DTD.") is XmlEvent && false : false;
            if (quote != '\0')
            {
                if (c == quote)
                    quote = '\0';
                continue;
            }

            if (c is '"' or '\'')
            {
                quote = c;
                continue;
            }

            if (c == '>')
                return true;
        }
    }

    private bool SkipExternalId(bool optionalNData = false)
    {
        if (StartsWith("PUBLIC"))
        {
            if (!Expect("PUBLIC") || !SkipRequiredSpace() || !ReadQuoted(out _))
                return false;
        }
        else if (!Expect("SYSTEM"))
        {
            return false;
        }

        if (!SkipRequiredSpace() || !ReadQuoted(out _))
            return false;
        if (!optionalNData)
            return true;
        var save = Save();
        if (!SkipSpaceAllowEmpty())
            return false;
        if (StartsWith("NDATA"))
            return Expect("NDATA") && SkipRequiredSpace() && ReadNmtoken(out _);
        Restore(save);
        return _signal != Signal.NeedMore && _error is null;
    }

    private bool SkipCommentBody()
    {
        if (!Expect("!--"))
            return false;
        var dash = 0;
        while (true)
        {
            if (!ReadChar(out var c))
                return _signal == Signal.Eof ? Fail("A comment is not closed.") is XmlEvent && false : false;
            if (c == '-')
            {
                dash++;
                if (dash == 2)
                {
                    if (!ReadChar(out var end))
                        return false;
                    if (end != '>')
                        return Fail("A comment contains '--'.") is XmlEvent && false;
                    return true;
                }
            }
            else
            {
                dash = 0;
            }
        }
    }

    private bool SkipProcessingInstruction(bool declare)
    {
        var start = Save();
        if (!Expect("<?"))
            return false;
        if (!ReadNmtoken(out var target))
            return false;
        if (!declare && target.Equals("xml", StringComparison.OrdinalIgnoreCase))
            return Fail("An xml declaration is only allowed at the start of the document.") is XmlEvent && false;
        while (true)
        {
            if (!ReadChar(out var c))
                return _signal == Signal.Eof ? Fail("A processing instruction is not closed.") is XmlEvent && false : false;
            if (c == '?' && TryPeek(out var next) && next == '>')
            {
                if (!ReadChar(out _))
                    return false;
                return true;
            }

            if (_signal == Signal.NeedMore)
            {
                Restore(start);
                return false;
            }
        }
    }

    private bool SkipMisc()
    {
        while (true)
        {
            if (!SkipSpaceAllowEmpty())
                return false;
            if (!TryPeek(out var c))
                return _signal != Signal.NeedMore;
            if (c != '<')
                return true;
            var save = Save();
            if (!ReadChar(out _))
                return false;
            if (!TryPeek(out var next))
            {
                Restore(save);
                return _signal != Signal.NeedMore;
            }

            if (next == '?')
            {
                Restore(save);
                if (!SkipProcessingInstruction(declare: false))
                    return false;
                continue;
            }

            if (next == '!')
            {
                var bang = Save();
                if (!ReadChar(out _) || !TryPeek(out var kind))
                {
                    Restore(save);
                    return _signal != Signal.NeedMore;
                }

                if (kind == '-')
                {
                    Restore(save);
                    if (!ReadChar(out _) || !SkipCommentBody())
                        return false;
                    continue;
                }

                Restore(bang);
                Restore(save);
                return true;
            }

            Restore(save);
            return true;
        }
    }

    private bool Bootstrap()
    {
        Span<byte> head = stackalloc byte[4];
        var count = _document.Source.Read(0, head);
        if (count < 2 && !_document.Source.IsComplete)
        {
            _signal = Signal.NeedMore;
            return false;
        }

        if (count >= 3 && head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF)
        {
            _document.Encoding = Encoding.UTF8;
            _byte = 3;
        }
        else if (count >= 2 && head[0] == 0xFF && head[1] == 0xFE)
        {
            _document.Encoding = Encoding.Unicode;
            _byte = 2;
        }
        else if (count >= 2 && head[0] == 0xFE && head[1] == 0xFF)
        {
            _document.Encoding = Encoding.BigEndianUnicode;
            _byte = 2;
        }
        else if (count >= 2 && head[0] == 0x3C && head[1] == 0x00)
        {
            _document.Encoding = Encoding.Unicode;
        }
        else if (count >= 2 && head[0] == 0x00 && head[1] == 0x3C)
        {
            _document.Encoding = Encoding.BigEndianUnicode;
        }
        else
        {
            _document.Encoding = Encoding.UTF8;
        }

        if (StartsWith("<?xml"))
        {
            if (!ReadDeclaration())
                return false;
        }

        _bootstrapped = true;
        return _error is null && _signal == Signal.None;
    }

    private bool ReadDeclaration()
    {
        if (!Expect("<?xml"))
            return false;
        string? encoding = null;
        while (true)
        {
            if (!SkipSpaceAllowEmpty())
                return false;
            if (TryPeek(out var c) && c == '?')
                break;
            if (!ReadNmtoken(out var attr))
                return false;
            if (!SkipSpaceAllowEmpty() || !Expect("=") || !SkipSpaceAllowEmpty() || !ReadQuotedLiteral(out var value))
                return false;
            if (attr == "encoding")
                encoding = value;
        }

        if (!Expect("?>"))
            return false;
        if (string.IsNullOrEmpty(encoding))
            return true;
        var next = ResolveEncoding(encoding);
        if (next is null)
            return Fail($"The encoding '{encoding}' is not supported.") is XmlEvent && false;
        if (!Compatible(_document.Encoding, next))
            return Fail("The encoding declaration does not match the document.") is XmlEvent && false;
        _document.Encoding = next;
        return true;
    }

    private static bool Compatible(Encoding sniffed, Encoding declared)
    {
        if (sniffed.CodePage == declared.CodePage)
            return true;
        var sniffed16 = sniffed.CodePage is 1200 or 1201;
        var declared16 = declared.CodePage is 1200 or 1201;
        if (sniffed16 != declared16)
            return false;
        return true;
    }

    private static Encoding? ResolveEncoding(string name)
    {
        var key = name.Trim().ToLowerInvariant();
        try
        {
            return key switch
            {
                "utf-8" => Encoding.UTF8,
                "utf-16" or "utf-16le" => Encoding.Unicode,
                "utf-16be" => Encoding.BigEndianUnicode,
                "iso-8859-1" or "latin1" => Encoding.Latin1,
                _ => Encoding.GetEncoding(name),
            };
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private bool ReadQuoted(out TextPiece piece) => ReadQuoted(out piece, allowLessThan: false);

    private bool ReadQuoted(out TextPiece piece, bool allowLessThan)
    {
        piece = default;
        if (!ReadChar(out var quote) || quote is not ('"' or '\''))
            return Fail("Expected a quoted value.") is XmlEvent && false;
        var entity = CurrentEntity();
        var start = Cursor();
        long end = start;
        while (true)
        {
            var at = Cursor();
            if (!ReadChar(out var c))
                return _signal == Signal.Eof ? Fail("A quoted value is not closed.") is XmlEvent && false : false;
            if (c == quote)
            {
                end = at;
                break;
            }

            if (c == '<' && !allowLessThan)
                return Fail("A '<' is not allowed in this value.") is XmlEvent && false;
            if (c == '&' && !ReadReference(attribute: true, out _))
                return false;
        }

        piece = entity < 0 ? TextPiece.Bytes(start, end, attribute: true) : TextPiece.Chars(entity, start, end, attribute: true);
        return true;
    }

    private bool ReadQuotedLiteral(out string value) => ReadQuotedLiteral(out value, allowLessThan: false);

    private bool ReadQuotedLiteral(out string value, bool allowLessThan)
    {
        value = "";
        if (!ReadQuoted(out var piece, allowLessThan))
            return false;
        value = ExpandLiteral(piece);
        return _error is null;
    }

    private string ExpandLiteral(TextPiece piece)
    {
        try
        {
            return ExpandedText.Slice(_document, [piece], 0, 1_000_000, out var more) is var text && more
                ? throw new InvalidOperationException("A namespace value is too long.")
                : text;
        }
        catch (InvalidOperationException ex)
        {
            Fail(ex.Message);
            return "";
        }
    }

    private bool ReadReference(bool attribute, out bool markup)
    {
        markup = false;
        if (!ReadChar(out var c))
            return false;
        if (c == '#')
            return ReadCharacterReference();
        var name = new StringBuilder();
        name.Append(c);
        while (TryPeek(out var next) && next != ';')
        {
            if (!ReadChar(out var part))
                return false;
            name.Append(part);
        }

        if (!Expect(";"))
            return false;
        var key = name.ToString();
        if (ExpandedCursor.Builtin(key, out _))
            return true;
        var entity = FindEntity(key);
        if (entity is null)
            return Fail($"The entity '{key}' is not defined.") is XmlEvent && false;
        if (entity.External)
            return Fail("External entities are not allowed.") is XmlEvent && false;
        if (entity.Markup)
        {
            if (attribute)
                return Fail("An attribute value cannot contain an element.") is XmlEvent && false;
            markup = true;
            return true;
        }

        return CheckEntityClosure(entity, []);
    }

    private bool ReadCharacterReference()
    {
        if (!TryPeek(out var c))
            return false;
        var hex = c is 'x' or 'X';
        if (hex && !ReadChar(out _))
            return false;
        var digits = new StringBuilder();
        while (TryPeek(out var next) && next != ';')
        {
            if (!ReadChar(out var digit))
                return false;
            digits.Append(digit);
        }

        if (!Expect(";"))
            return false;
        if (!int.TryParse(digits.ToString(), hex ? System.Globalization.NumberStyles.HexNumber : System.Globalization.NumberStyles.None, null, out var code)
            || code is 0 or > 0x10FFFF or >= 0xD800 and <= 0xDFFF)
            return Fail("A character reference is not valid.") is XmlEvent && false;
        return true;
    }

    private bool CheckEntityClosure(EntityDef entity, HashSet<string> stack)
    {
        if (!stack.Add(entity.Name))
            return Fail("The entity reference is circular.") is XmlEvent && false;
        for (var i = 0; i < entity.Replacement.Length; i++)
        {
            if (entity.Replacement[i] != '&' || i + 1 >= entity.Replacement.Length)
                continue;
            if (entity.Replacement[i + 1] == '#')
                continue;
            var end = entity.Replacement.IndexOf(';', i + 1);
            if (end < 0)
                return Fail("An entity reference is incomplete.") is XmlEvent && false;
            var name = entity.Replacement[(i + 1)..end];
            if (ExpandedCursor.Builtin(name, out _))
            {
                i = end;
                continue;
            }

            var next = FindEntity(name);
            if (next is null)
                return Fail($"The entity '{name}' is not defined.") is XmlEvent && false;
            if (next.External)
                return Fail("External entities are not allowed.") is XmlEvent && false;
            if (!CheckEntityClosure(next, stack))
                return false;
            i = end;
        }

        stack.Remove(entity.Name);
        return true;
    }

    private bool TryConsumeMarkupEntity()
    {
        var save = Save();
        if (!TryPeek(out var c) || c != '&')
            return false;
        if (!ReadChar(out _))
            return _signal == Signal.NeedMore;
        if (!ReadReference(attribute: false, out var markup))
        {
            if (_signal == Signal.NeedMore || _error is not null)
                return true;
            Restore(save);
            return false;
        }

        if (!markup)
        {
            Restore(save);
            return false;
        }

        // ReadReference consumed a markup entity but did not push it. Push now.
        // The name was validated. Re-read is unnecessary: recover the entity from the saved text.
        Restore(save);
        if (!ReadChar(out _) || !ReadNmtoken(out var name) || !Expect(";"))
            return true;
        var entity = FindEntity(name);
        if (entity is null)
            return true;
        _inputs.Push(new CharInput { EntityId = EntityIndex(entity), Text = entity.Replacement });
        return true;
    }

    private bool ReadQName(out string prefix, out string local)
    {
        prefix = "";
        local = "";
        if (!ReadNmtoken(out var token))
            return false;
        var colon = token.IndexOf(':');
        if (colon < 0)
        {
            local = Intern(token);
            return true;
        }

        if (colon == 0 || colon != token.LastIndexOf(':') || colon == token.Length - 1)
            return Fail("A name is not valid.") is XmlEvent && false;
        prefix = Intern(token[..colon]);
        local = Intern(token[(colon + 1)..]);
        return true;
    }

    private bool ReadNmtoken(out string token)
    {
        if (_inputs.Count == 0 && !_hasLow && _document.Encoding.CodePage == 65001)
            return ReadAsciiName(out token);
        return ReadNmtokenSlow(out token);
    }

    private bool ReadNmtokenSlow(out string token)
    {
        token = "";
        if (!ReadChar(out var first))
            return false;
        if (!IsNameStart(first))
            return Fail("A name is not valid.") is XmlEvent && false;
        var builder = new StringBuilder();
        builder.Append(first);
        while (TryPeek(out var c) && IsNameChar(c))
        {
            if (!ReadChar(out var next))
                return false;
            builder.Append(next);
        }

        if (_signal == Signal.NeedMore)
            return false;
        token = builder.ToString();
        return true;
    }

    private bool SkipRequiredSpace()
    {
        if (!TryPeek(out var c) || c is not (' ' or '\t' or '\r' or '\n'))
            return _signal == Signal.NeedMore ? false : Fail("Expected whitespace.") is XmlEvent && false;
        return SkipSpaceAllowEmpty();
    }

    private bool SkipSpaceAllowEmpty()
    {
        if (_inputs.Count == 0 && !_hasLow && _document.Encoding.CodePage == 65001)
            return SkipSpaceBytes();
        return SkipSpaceSlow();
    }

    private bool SkipSpaceSlow()
    {
        while (TryPeek(out var c) && c is ' ' or '\t' or '\r' or '\n')
        {
            if (!ReadChar(out _))
                return false;
        }

        return _signal != Signal.NeedMore && _error is null;
    }

    private bool StartsWith(string text)
    {
        var save = Save();
        var signal = _signal;
        var error = _error;
        foreach (var expected in text)
        {
            if (!ReadChar(out var c) || c != expected)
            {
                Restore(save);
                _signal = signal;
                _error = error;
                return false;
            }
        }

        var matched = _error is null;
        Restore(save);
        _signal = signal;
        _error = error;
        return matched;
    }

    private bool Expect(string text)
    {
        foreach (var expected in text)
        {
            if (!ReadChar(out var c))
                return false;
            if (c != expected)
                return Fail($"Expected '{text}'.") is XmlEvent && false;
        }

        return true;
    }

    private bool TryPeek(out char value)
    {
        value = default;
        if (_hasLow)
        {
            value = _low;
            return true;
        }

        if (_inputs.Count > 0)
        {
            var input = _inputs.Peek();
            if (input.Position < input.Text.Length)
            {
                value = input.Text[input.Position];
                return true;
            }
        }
        else if (_document.Encoding.CodePage == 65001 && TrySpan(out var span, out _))
        {
            if (span[0] < 0x80)
            {
                value = (char)span[0];
                return true;
            }
        }

        var save = Save();
        var ok = ReadChar(out value);
        var signal = _signal;
        var error = _error;
        Restore(save);
        _signal = signal;
        _error = error;
        return ok;
    }

    private bool ReadChar(out char value)
    {
        value = default;
        if (_hasLow)
        {
            value = _low;
            _hasLow = false;
            Note(value);
            return true;
        }

        while (_inputs.Count > 0)
        {
            var input = _inputs.Peek();
            if (input.Position >= input.Text.Length)
            {
                _inputs.Pop();
                continue;
            }

            value = input.Text[input.Position++];
            Note(value);
            return true;
        }

        if (_byte >= _limit)
        {
            _signal = Signal.Eof;
            return false;
        }

        if (_document.Encoding.CodePage == 65001 && TrySpan(out var ascii, out var asciiNeedMore) && ascii[0] < 0x80)
        {
            _byte++;
            value = (char)ascii[0];
            Note(value);
            return true;
        }
        else if (_document.Encoding.CodePage == 65001 && !TrySpan(out _, out asciiNeedMore) && asciiNeedMore)
        {
            _signal = Signal.NeedMore;
            return false;
        }

        var sourceEnd = _document.Source.Length;
        if (_byte >= sourceEnd)
        {
            if (!_document.Source.IsComplete && _byte < _limit)
                _signal = Signal.NeedMore;
            else
                _signal = Signal.Eof;
            return false;
        }

        var limit = _limit < sourceEnd ? _limit : sourceEnd;
        var start = _byte;
        if (!CharDecoders.TryDecode(_document.Encoding, _document.Source, _byte, limit, out value, out var bytes, out var needMore, out var error))
        {
            if (needMore && _limit > sourceEnd)
            {
                _signal = Signal.NeedMore;
                return false;
            }

            if (error is not null)
                return Fail(error) is XmlEvent && false;
            _signal = Signal.Eof;
            return false;
        }

        _byte += bytes;
        if (char.IsHighSurrogate(value) && CharDecoders.TryDecodeLowSurrogate(_document.Encoding, _document.Source, start, out var low))
        {
            _low = low;
            _hasLow = true;
        }

        Note(value);
        return true;
    }

    private void Note(char value)
    {
        if (value == '\r')
        {
            _line++;
            _column = 1;
            _afterCr = true;
            return;
        }

        if (value == '\n')
        {
            if (!_afterCr)
            {
                _line++;
                _column = 1;
            }

            _afterCr = false;
            return;
        }

        _column++;
        _afterCr = false;
    }

    private long Cursor() => _inputs.Count > 0 ? _inputs.Peek().Position : _byte;

    private int CurrentEntity() => _inputs.Count > 0 ? _inputs.Peek().EntityId : -1;

    private EntityDef? FindEntity(string name)
    {
        foreach (var entity in _document.Entities)
        {
            if (entity.Name == name)
                return entity;
        }

        return null;
    }

    private int EntityIndex(EntityDef entity)
    {
        for (var i = 0; i < _document.Entities.Count; i++)
        {
            if (ReferenceEquals(_document.Entities[i], entity))
                return i;
        }

        return -1;
    }

    private string Intern(string value) => _intern.TryGetValue(value, out var existing) ? existing : _intern[value] = value;

    private XmlEvent Fail(string message)
    {
        if (_error is null)
        {
            _error = message;
            _errorByte = _inputs.Count == 0 ? _byte : _mark.Byte;
            if (_inputs.Count == 0)
                Locate(_errorByte);
            _errorLine = _line;
            _errorColumn = _column;
        }

        return ErrorEvent();
    }

    private XmlEvent ErrorEvent() => new()
    {
        Kind = XmlEventKind.Error,
        Message = _error ?? "The document is not well-formed.",
        ErrorByte = _errorByte,
        Line = _errorLine,
        Column = _errorColumn,
    };

    private XmlEvent SignalEvent() => _error is not null ? ErrorEvent() : _signal == Signal.NeedMore ? XmlEvent.More() : ErrorEvent();

    private Checkpoint Save() => new()
    {
        Byte = _byte,
        Line = _line,
        Column = _column,
        AfterCr = _afterCr,
        Depth = _inputs.Count,
        CharPos = _inputs.Count > 0 ? _inputs.Peek().Position : 0,
        HasLow = _hasLow,
        Low = _low,
        TextFrom = _textFrom,
        TextNonWs = _textNonWs,
        TextLine = _textLine,
        TextColumn = _textColumn,
        TextAfterCr = _textAfterCr,
        Brackets = _brackets,
    };

    private void Restore(Checkpoint mark)
    {
        while (_inputs.Count > mark.Depth)
            _inputs.Pop();
        if (_inputs.Count > 0)
            _inputs.Peek().Position = mark.CharPos;
        _byte = mark.Byte;
        _line = mark.Line;
        _column = mark.Column;
        _afterCr = mark.AfterCr;
        _hasLow = mark.HasLow;
        _low = mark.Low;
        _textFrom = mark.TextFrom;
        _textNonWs = mark.TextNonWs;
        _textLine = mark.TextLine;
        _textColumn = mark.TextColumn;
        _textAfterCr = mark.TextAfterCr;
        _brackets = mark.Brackets;
    }

    private static bool IsNameStart(char value) => value is '_' or ':' || char.IsLetter(value);

    private static bool IsNameChar(char value) => IsNameStart(value) || value is '-' or '.' || char.IsDigit(value);

    private XmlEvent Prepare(XmlEventKind kind)
    {
        _event.Kind = kind;
        _event.Prefix = "";
        _event.Local = "";
        _event.Ns = "";
        _event.Name = "";
        _event.Empty = false;
        _event.ByteStart = 0;
        _event.ByteEnd = 0;
        _event.Scope = _scope;
        _event.Attributes = _attrs;
        _event.Text = default;
        _event.HasText = false;
        _event.TrustedText = false;
        _event.Message = "";
        _event.Line = _line;
        _event.Column = _column;
        _event.ErrorByte = 0;
        _event.SameShape = false;
        _event.SearchMiss = false;
        _event.MissAttribute = false;
        return _event;
    }

    private XmlEvent TextEvent(TextPiece piece, bool trusted)
    {
        var ev = Prepare(XmlEventKind.Text);
        ev.HasText = true;
        ev.TrustedText = trusted;
        ev.Text = piece;
        return ev;
    }

    private XmlEvent? ReadTextBytes()
    {
        if (_textFrom < 0)
        {
            _textFrom = _byte;
            _textNonWs = false;
            _textLine = _line;
            _textColumn = _column;
            _textAfterCr = _afterCr;
            _brackets = 0;
        }

        var budget = 64 * 1024;
        while (budget > 0)
        {
            if (!TrySpan(out var span, out var needMore))
            {
                if (needMore)
                {
                    _signal = Signal.NeedMore;
                    return SignalEvent();
                }

                return FinishText();
            }

            if (_brackets >= 2 && span[0] == (byte)'>')
                return Fail("']]>' is not allowed in text.");
            var available = span;
            var capped = available.Length > budget;
            if (capped)
                span = available[..budget];
            var stop = span.IndexOfAny(StopBytes);
            var run = stop < 0 ? span : span[..stop];
            var atEnd = stop < 0 && !capped && _byte + span.Length >= _document.Source.Length && _document.Source.IsComplete;
            if (run.Length >= 48 && run.IndexOfAnyInRange((byte)0x80, (byte)0xFF) < 0)
            {
                if (run.IndexOf((byte)']') >= 0 && run.IndexOf("]]>"u8) >= 0)
                    return Fail("']]>' is not allowed in text.");
                if (run.Length > 0)
                {
                    if (run.IndexOfAnyExcept(SpaceBytes) >= 0)
                        _textNonWs = true;
                    _brackets = TrailingBrackets(run, _brackets);
                    _byte += run.Length;
                    budget -= run.Length;
                }

                if (stop < 0)
                {
                    if (budget == 0)
                        return ContinueText();
                    continue;
                }

                if (span[stop] == (byte)'<')
                    return FinishText();
                RewindText();
                return ReadTextSlow();
            }

            var text = MeasureUtf8(run, allowPartialTail: stop < 0 && !atEnd);
            if (text.Error is not null)
            {
                _byte += text.Count;
                return Fail(text.Error);
            }

            // The budget or the window can leave a lead byte with none of its continuation bytes.
            if (text.Count == 0 && run.Length > 0 && stop < 0 && !atEnd)
            {
                if (!capped)
                {
                    var seen = available.Length;
                    _windowCount = 0;
                    if (!TrySpan(out var wider, out var more) || wider.Length <= seen)
                    {
                        if (more)
                        {
                            _signal = Signal.NeedMore;
                            return SignalEvent();
                        }

                        return Fail("The document ends in the middle of a character.");
                    }

                    continue;
                }

                var needed = available[0] switch
                {
                    >= 0xC2 and <= 0xDF => 2,
                    >= 0xE0 and <= 0xEF => 3,
                    >= 0xF0 and <= 0xF4 => 4,
                    _ => 0,
                };
                if (needed == 0)
                    return Fail("The document is not valid UTF-8.");
                if (needed > available.Length)
                    return Fail("The document ends in the middle of a character.");
                var probe = MeasureUtf8(available[..needed], allowPartialTail: false);
                if (probe.Error is not null || probe.Count != needed)
                {
                    _byte += probe.Count;
                    return Fail(probe.Error ?? "The document is not valid UTF-8.");
                }

                run = available[..probe.Count];
            }
            else if (text.Count < run.Length)
            {
                run = run[..text.Count];
            }
            if (run.Length > 0)
            {
                if (run.IndexOfAnyExcept(SpaceBytes) >= 0)
                    _textNonWs = true;
                if (run.IndexOf("]]>"u8) >= 0)
                    return Fail("']]>' is not allowed in text.");
                _brackets = TrailingBrackets(run, _brackets);
                _byte += run.Length;
                budget -= run.Length;
            }

            if (stop < 0)
            {
                if (budget == 0)
                    return ContinueText();
                continue;
            }

            var mark = span[stop];
            if (mark == (byte)'<')
                return FinishText();
            RewindText();
            return ReadTextSlow();
        }

        return ContinueText();
    }

    private XmlEvent ContinueText()
    {
        var ev = Prepare(XmlEventKind.Continue);
        return ev;
    }

    private XmlEvent? FinishText()
    {
        var start = _textFrom;
        var nonWs = _textNonWs;
        _textFrom = -1;
        _textNonWs = false;
        _brackets = 0;
        if (start < 0 || _byte == start || !nonWs)
            return null;
        return TextEvent(TextPiece.Bytes(start, _byte), trusted: true);
    }

    private void RewindText()
    {
        if (_textFrom < 0)
            return;
        _byte = _textFrom;
        _line = _textLine;
        _column = _textColumn;
        _afterCr = _textAfterCr;
        _textFrom = -1;
        _textNonWs = false;
        _brackets = 0;
    }

    private bool SkipSpaceBytes()
    {
        while (true)
        {
            if (!TrySpan(out var span, out var needMore))
            {
                if (needMore)
                {
                    _signal = Signal.NeedMore;
                    return false;
                }

                return _error is null;
            }

            var n = span.IndexOfAnyExcept(SpaceBytes);
            if (n < 0)
            {
                _byte += span.Length;
                continue;
            }

            if (n > 0)
                _byte += n;

            return _error is null && _signal != Signal.NeedMore;
        }
    }

    private bool SplitAsciiName(ReadOnlySpan<byte> bytes, out string prefix, out string local)
    {
        prefix = "";
        local = "";
        var colon = bytes.IndexOf((byte)':');
        if (colon < 0)
        {
            local = InternAscii(bytes);
            return local.Length > 0;
        }

        if (colon == 0 || colon == bytes.Length - 1 || bytes[(colon + 1)..].Contains((byte)':'))
            return false;
        prefix = InternAscii(bytes[..colon]);
        local = InternAscii(bytes[(colon + 1)..]);
        return prefix.Length > 0 && local.Length > 0;
    }

    private bool ReadAsciiName(out string token)
    {
        token = "";
        if (!TrySpan(out var span, out var needMore))
        {
            _signal = needMore ? Signal.NeedMore : Signal.Eof;
            return false;
        }

        if (span[0] >= 0x80)
            return ReadNmtokenSlow(out token);
        if (!IsAsciiNameStart(span[0]))
            return Fail("A name is not valid.") is XmlEvent && false;
        var n = 1;
        while (n < span.Length && IsAsciiNameChar(span[n]))
            n++;
        if (n == span.Length)
        {
            var next = _byte + n;
            if (next < _limit && (next < _document.Source.Length || !_document.Source.IsComplete))
                return ReadNmtokenSlow(out token);
        }
        else if (span[n] >= 0x80)
        {
            return ReadNmtokenSlow(out token);
        }

        token = InternAscii(span[..n]);
        _byte += n;
        return true;
    }

    private string InternAscii(ReadOnlySpan<byte> bytes)
    {
        var hash = unchecked((int)HashAscii(bytes));
        if (_names.TryGetValue(hash, out var bucket))
        {
            foreach (var item in bucket)
            {
                if (AsciiEquals(item, bytes))
                    return item;
            }

            var created = AsciiString(bytes);
            var grown = new string[bucket.Length + 1];
            Array.Copy(bucket, grown, bucket.Length);
            grown[^1] = created;
            _names[hash] = grown;
            _intern[created] = created;
            return created;
        }

        var text = AsciiString(bytes);
        _names[hash] = [text];
        _intern[text] = text;
        return text;
    }

    private static bool AsciiEquals(string text, ReadOnlySpan<byte> bytes)
    {
        if (text.Length != bytes.Length)
            return false;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != bytes[i])
                return false;
        }

        return true;
    }

    private static string AsciiString(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length > 512)
            return Encoding.UTF8.GetString(bytes);
        Span<char> chars = stackalloc char[bytes.Length];
        for (var i = 0; i < bytes.Length; i++)
            chars[i] = (char)bytes[i];
        return new string(chars);
    }

    private static uint HashAscii(ReadOnlySpan<byte> bytes)
    {
        var hash = 2166136261u;
        foreach (var b in bytes)
            hash = (hash ^ b) * 16777619u;
        return hash;
    }

    private bool TrySpan(out ReadOnlySpan<byte> span, out bool needMore)
    {
        span = default;
        needMore = false;
        if (_byte >= _limit)
            return false;
        var length = _document.Source.Length;
        if (_byte >= length)
        {
            needMore = !_document.Source.IsComplete && _byte < _limit;
            return false;
        }

        var window = MarkupWindow();
        var visibleEnd = _windowAt + _windowCount;
        if (visibleEnd > _limit)
            visibleEnd = _limit;
        var inside = _byte >= _windowAt && _byte < _windowAt + _windowCount;
        var have = inside ? (int)(visibleEnd - _byte) : 0;
        var could = (int)Math.Min(window.Length, Math.Min(length, _limit) - _byte);
        // A UTF-8 character is at most 4 bytes. A shorter tail is not the end of the file,
        // and leaving it in place makes the text reader wait on bytes it will never see.
        if (!inside || (have < could && have < 4))
        {
            var want = (int)Math.Min(window.Length, length - _byte);
            var read = _document.Source.Read(_byte, window.AsSpan(0, want));
            if (read <= 0)
            {
                needMore = !_document.Source.IsComplete;
                return false;
            }

            _windowAt = _byte;
            _windowCount = read;
        }

        var end = _windowAt + _windowCount;
        if (end > _limit)
            end = _limit;
        if (_byte >= end)
            return false;
        span = window.AsSpan((int)(_byte - _windowAt), (int)(end - _byte));
        return span.Length > 0;
    }

    private byte[] MarkupWindow() => _window ??= new byte[_fragment ? 4096 : 256 * 1024];

    internal bool TryWindow(long start, long end, out ReadOnlySpan<byte> slice)
    {
        slice = default;
        var window = _window;
        if (window is null || start < _windowAt || end > _windowAt + _windowCount || end < start)
            return false;
        slice = window.AsSpan((int)(start - _windowAt), (int)(end - start));
        return true;
    }

    /// <summary>
    /// The start tag is already open. Move to its end tag and leave that tag for the next read.
    /// Nested markup is not turned into events; a later open of that nested element reads it.
    /// </summary>
    internal void SkipNestedContent()
    {
        if (_error is not null || _inputs.Count > 0 || _hasLow || _open.Count == 0)
            return;
        if (_document.Encoding.CodePage != 65001)
            return;
        var buf = _skipWindow ??= new byte[64 * 1024];
        var count = 0;
        var i = 0;
        var depth = 1;

        bool Take(out byte value)
        {
            if (i >= count && !Refill())
            {
                value = 0;
                return false;
            }

            value = buf[i++];
            return true;
        }

        bool Refill()
        {
            if (i > 0)
            {
                AdvanceLines(buf.AsSpan(0, i));
                _byte += i;
                var tail = count - i;
                if (tail > 0)
                    Buffer.BlockCopy(buf, i, buf, 0, tail);
                count = tail;
                i = 0;
            }

            var cap = buf.Length - count;
            if (cap == 0)
                return count > i;
            var origin = _byte + count;
            var fileLeft = _document.Source.Length - origin;
            if (_limit < _document.Source.Length)
                fileLeft = Math.Min(fileLeft, _limit - origin);
            if (fileLeft <= 0)
                return false;
            var want = (int)Math.Min(cap, fileLeft);
            var read = _document.Source.Read(origin, buf.AsSpan(count, want));
            if (read <= 0)
                return false;
            count += read;
            return true;
        }

        bool Die(string message)
        {
            Fail(message);
            return false;
        }

        void Finish(int endAt)
        {
            AdvanceLines(buf.AsSpan(0, endAt));
            _byte += endAt;
            _windowCount = 0;
        }

        bool SkipUntil(byte first, byte second, byte third)
        {
            var prev = 0;
            var prev2 = 0;
            while (Take(out var b))
            {
                if (prev2 == first && prev == second && b == third)
                    return true;
                prev2 = prev;
                prev = b;
            }

            return Die("The document ends before a tag is closed.");
        }

        bool SkipStart()
        {
            var quote = 0;
            var sawSlash = false;
            while (true)
            {
                if (i >= count && !Refill())
                    return Die("The document ends before a tag is closed.");
                if (quote != 0)
                {
                    var quoted = buf.AsSpan(i, count - i);
                    var end = quoted.IndexOf((byte)quote);
                    if (end < 0)
                    {
                        i = count;
                        continue;
                    }

                    i += end + 1;
                    quote = 0;
                    continue;
                }

                var span = buf.AsSpan(i, count - i);
                var rel = span.IndexOfAny(TagMarks);
                if (rel < 0)
                {
                    if (sawSlash && span.IndexOfAnyExcept(SpaceBytes) >= 0)
                        return Die("A tag is not closed.");
                    i = count;
                    continue;
                }

                if (sawSlash && span[..rel].IndexOfAnyExcept(SpaceBytes) >= 0)
                    return Die("A tag is not closed.");
                i += rel;
                var b = buf[i++];
                if (b is (byte)'"' or (byte)'\'')
                {
                    quote = b;
                    sawSlash = false;
                    continue;
                }

                if (b == (byte)'>')
                {
                    if (!sawSlash)
                        depth++;
                    return true;
                }

                sawSlash = true;
            }
        }

        while (depth > 0)
        {
            if (i >= count && !Refill())
            {
                Die("The document ends before a tag is closed.");
                return;
            }

            var text = buf.AsSpan(i, count - i);
            var mark = text.IndexOf((byte)'<');
            if (mark < 0)
            {
                i = count;
                continue;
            }

            i += mark;
            var at = i;
            var marked = _byte;
            i++;
            if (i >= count && !Refill())
            {
                Die("The document ends before a tag is closed.");
                return;
            }

            var next = buf[i++];

            if (next == (byte)'/' && depth == 1)
            {
                if (_byte != marked)
                {
                    // The '<' filled the buffer and was counted. Put the cursor back on it.
                    _byte--;
                    if (_column > 1)
                        _column--;
                    _windowCount = 0;
                    return;
                }

                Finish(at);
                return;
            }

            if (next == (byte)'/')
            {
                var closed = false;
                while (Take(out var end))
                {
                    if (end == (byte)'>')
                    {
                        depth--;
                        closed = true;
                        break;
                    }
                }

                if (!closed)
                {
                    Die("The document ends before a tag is closed.");
                    return;
                }

                continue;
            }

            if (next == (byte)'!')
            {
                if (!Take(out var bang) || !Take(out var bang2))
                {
                    Die("The document ends before a tag is closed.");
                    return;
                }

                if (bang == (byte)'-' && bang2 == (byte)'-')
                {
                    if (!SkipUntil((byte)'-', (byte)'-', (byte)'>'))
                        return;
                    continue;
                }

                if (bang == (byte)'[' && bang2 == (byte)'C' && SkipLiteral("DATA["u8))
                {
                    if (!SkipUntil((byte)']', (byte)']', (byte)'>'))
                        return;
                    continue;
                }

                Die("This declaration is not allowed here.");
                return;
            }

            if (next == (byte)'?')
            {
                var prev = (byte)'?';
                var closed = false;
                while (Take(out var pi))
                {
                    if (prev == (byte)'?' && pi == (byte)'>')
                    {
                        closed = true;
                        break;
                    }

                    prev = pi;
                }

                if (!closed)
                {
                    Die("The document ends before a tag is closed.");
                    return;
                }

                continue;
            }

            if (!SkipStart())
                return;
        }

        bool SkipLiteral(ReadOnlySpan<byte> literal)
        {
            foreach (var expected in literal)
            {
                if (!Take(out var found) || found != expected)
                    return false;
            }

            return true;
        }
    }

    private void DropFlat()
    {
        if (_open.Count > 0)
            _flatAtDepth.Remove(_open.Count);
    }

    private bool TryFlatElement(out XmlEvent ev)
    {
        ev = _event;
        if (_inputs.Count > 0 || _hasLow || _document.Encoding.CodePage != 65001 || _open.Count == 0)
            return false;

        var hasDepth = _flatAtDepth.TryGetValue(_open.Count, out var names) && names.Count > 0;
        var parentName = _open.Peek();
        var hasPredicted = _predictedByParent.TryGetValue(parentName, out var predicted) && predicted.Count > 0;

        if (!hasDepth && !hasPredicted)
            return false;

        if (!TrySpan(out var span, out _) || span.Length < 3 || span[0] != (byte)'<' || span[1] is (byte)'/' or (byte)'!' or (byte)'?')
            return false;
        var index = 1;
        if (!ScanName(span, ref index, out var nameAt, out var nameLen))
            return false;

        string? matched = null;
        var speculative = false;

        if (hasDepth)
        {
            foreach (var candidate in names!)
            {
                if (candidate.Length != nameLen)
                    continue;
                var same = true;
                for (var i = 0; i < nameLen; i++)
                {
                    if ((byte)candidate[i] != span[nameAt + i])
                    {
                        same = false;
                        break;
                    }
                }

                if (same)
                {
                    matched = candidate;
                    break;
                }
            }
        }

        if (matched is null && hasPredicted)
        {
            foreach (var candidate in predicted!)
            {
                if (candidate.Length != nameLen)
                    continue;
                var key = (parentName, candidate);
                if (_shapeBlacklist.Contains(key))
                    continue;

                var same = true;
                for (var i = 0; i < nameLen; i++)
                {
                    if ((byte)candidate[i] != span[nameAt + i])
                    {
                        same = false;
                        break;
                    }
                }

                if (same)
                {
                    matched = candidate;
                    speculative = true;
                    break;
                }
            }
        }

        if (matched is null)
            return false;

        ElementShape? cachedShape = null;
        if (speculative && _shapeCache.TryGetValue((parentName, matched), out cachedShape) && cachedShape.FlatShape is not null)
        {
            _flatShape.Clear();
            _flatShape.AddRange(cachedShape.FlatShape);
            _shapeKeys.Clear();
            _shapeKeys.AddRange(cachedShape.ShapeKeys!);
            _haveShape = true;
        }

        _flatFields.Clear();
        if (!ConsumeFlat(span, index, matched, out var consumed) && !RetryFlat(matched, out consumed))
        {
            _flatFields.Clear();
            if (speculative)
            {
                var key = (parentName, matched);
                _shapeBlacklist.Add(key);
                RemovePrediction(key);
                _haveShape = false;
            }
            return false;
        }

        if (speculative)
        {
            if (_shapeCache.TryGetValue((parentName, matched), out var cached))
            {
                if (cached.FlatShape is null && _flatShape.Count > 0)
                {
                    cached.FlatShape = _flatShape.ToArray();
                    cached.ShapeKeys = _shapeKeys.ToArray();
                }

                if (cached.Fields is null && _flatFields.Count > 0)
                {
                    cached.Fields = _flatFields.ToArray();
                }
            }

            if (FlatSameShape && _flatFields.Count == 0 && cachedShape?.Fields is not null)
            {
                _flatFields.AddRange(cachedShape.Fields);
            }

            if (!_flatAtDepth.TryGetValue(_open.Count, out var currentFlat))
            {
                currentFlat = new HashSet<string>(StringComparer.Ordinal);
                _flatAtDepth[_open.Count] = currentFlat;
            }
            currentFlat.Add(matched);
        }

        var start = _byte;
        _byte += consumed;
        ev = Prepare(XmlEventKind.Flat);
        ev.Name = matched;
        ev.Local = matched;
        ev.ByteStart = start;
        ev.ByteEnd = _byte;
        ev.FlatFields = _flatFields;
        ev.SameShape = FlatSameShape;
        return true;
    }

    private bool RetryFlat(string matched, out int consumed)
    {
        consumed = 0;
        _flatFields.Clear();
        var window = MarkupWindow();
        var have = _windowAt + _windowCount - _byte;
        if (have >= window.Length || _byte + have >= _document.Source.Length)
            return false;
        _windowCount = 0;
        if (!TrySpan(out var span, out _) || span.Length < 3 || span[0] != (byte)'<' || span[1] is (byte)'/' or (byte)'!' or (byte)'?')
            return false;
        var index = 1;
        if (!ScanName(span, ref index, out var nameAt, out var nameLen) || nameLen != matched.Length)
            return false;
        for (var i = 0; i < nameLen; i++)
        {
            if ((byte)matched[i] != span[nameAt + i])
                return false;
        }

        return ConsumeFlat(span, index, matched, out consumed);
    }

    private bool ConsumeFlat(ReadOnlySpan<byte> span, int index, string name, out int consumed)
    {
        consumed = 0;
        if (OmitFlatShape && TrySkipSimple(span, index, out consumed))
            return true;
        _shapeBuild.Clear();
        _shapeIndex = 0;
        _sameShape = _haveShape;
        _attrSpans.Clear();
        if (!SkipFlatAttributes(span, ref index, record: !OmitFlatShape))
            return false;
        if (index >= span.Length)
            return false;
        if (span[index] == (byte)'/' && index + 1 < span.Length && span[index + 1] == (byte)'>')
        {
            consumed = index + 2;
            FinishShape();
            return true;
        }

        if (span[index] != (byte)'>')
            return false;
        index++;
        var depth = 1;
        var sawText = false;
        while (depth > 0)
        {
            if (index >= span.Length)
                return false;
            if (span[index] != (byte)'<')
            {
                var rest = span[index..];
                var rel = rest.IndexOfAny(StopBytes);
                if ((uint)rel >= (uint)rest.Length || rest[rel] != (byte)'<')
                    return false;
                if (depth == 1 && rest[..rel].IndexOfAnyExcept(SpaceBytes) >= 0 && !FlatText(rest[..rel], !OmitFlatShape, ref sawText))
                    return false;
                index += rel;
                continue;
            }

            if (index + 1 >= span.Length)
                return false;
            if (span[index + 1] == (byte)'/')
            {
                index += 2;
                if (!ScanName(span, ref index, out var endAt, out var endLen))
                    return false;
                if (depth == 1 && (endLen != name.Length || !BytesEqual(span.Slice(endAt, endLen), name)))
                    return false;
                while (index < span.Length && IsXmlSpace(span[index]))
                    index++;
                if (index >= span.Length || span[index] != (byte)'>')
                    return false;
                index++;
                depth--;
                continue;
            }

            if (span[index + 1] is (byte)'!' or (byte)'?')
                return false;
            if (depth != 1)
            {
                if (!SkipStartTag(span, ref index, out var nestedEmpty))
                    return false;
                if (!nestedEmpty)
                    depth++;
                continue;
            }

            if (TryPlainChild(span, ref index))
                continue;

            index++;
            if (!ScanName(span, ref index, out var childAt, out var childLen))
                return false;
            Accept(span.Slice(childAt, childLen), NameKind.Element);

            if (!SkipFlatAttributes(span, ref index, record: false))
                return false;
            if (index >= span.Length)
                return false;
            if (span[index] == (byte)'/' && index + 1 < span.Length && span[index + 1] == (byte)'>')
            {
                index += 2;
                continue;
            }

            if (span[index] != (byte)'>')
                return false;
            index++;
            depth++;
        }

        consumed = index;
        FinishShape();
        return true;
    }

    private bool TryPlainChild(ReadOnlySpan<byte> span, ref int index)
    {
        var nameAt = index + 1;
        if ((uint)nameAt >= (uint)span.Length || !IsAsciiNameStart(span[nameAt]))
            return false;
        var cursor = nameAt + 1;
        while ((uint)cursor < (uint)span.Length && IsAsciiNameChar(span[cursor]))
            cursor++;
        if ((uint)cursor >= (uint)span.Length)
            return false;
        var nameLen = cursor - nameAt;
        while ((uint)cursor < (uint)span.Length && IsXmlSpace(span[cursor]))
            cursor++;
        if ((uint)(cursor + 1) < (uint)span.Length && span[cursor] == (byte)'/' && span[cursor + 1] == (byte)'>')
        {
            RecordChild(span.Slice(nameAt, nameLen));
            index = cursor + 2;
            return true;
        }

        if ((uint)cursor >= (uint)span.Length || span[cursor] != (byte)'>')
            return false;
        var text = cursor + 1;
        var end = text;
        while ((uint)end < (uint)span.Length)
        {
            var bite = span[end];
            if (bite is (byte)'<' or (byte)'&')
                break;
            end++;
        }

        if ((uint)(end + 2 + nameLen) >= (uint)span.Length || span[end] != (byte)'<' || span[end + 1] != (byte)'/')
            return false;
        if (!span.Slice(end + 2, nameLen).SequenceEqual(span.Slice(nameAt, nameLen)) || span[end + 2 + nameLen] != (byte)'>')
            return false;
        RecordChild(span.Slice(nameAt, nameLen));
        index = end + 3 + nameLen;
        return true;
    }

    // An empty tag's bytes are enough for a search. A body, an entity, or a nested tag uses the general walk.
    private bool TrySkipSimple(ReadOnlySpan<byte> span, int index, out int consumed)
    {
        consumed = 0;
        var quote = (byte)0;
        var i = index;
        while ((uint)i < (uint)span.Length)
        {
            var b = span[i++];
            if (quote != 0)
            {
                if (b == quote)
                    quote = 0;
                else if (b is (byte)'<' or (byte)'&')
                    return false;
                continue;
            }

            if (b is (byte)'"' or (byte)'\'')
            {
                quote = b;
                continue;
            }

            if (b == (byte)'/')
            {
                if ((uint)i >= (uint)span.Length || span[i] != (byte)'>')
                    return false;
                consumed = i + 1;
                return true;
            }

            if (b is (byte)'>' or (byte)'<')
                return false;
        }

        return false;
    }

    private void RecordChild(ReadOnlySpan<byte> bytes)
    {
        if (!OmitFlatShape)
            Accept(bytes, NameKind.Element);
    }

    private void Accept(ReadOnlySpan<byte> bytes, NameKind kind)
    {
        if (_sameShape
            && _shapeIndex < _flatShape.Count
            && _shapeKeys[_shapeIndex].Kind == kind
            && _flatShape[_shapeIndex].Length == bytes.Length
            && AsciiEqual(bytes, _flatShape[_shapeIndex]))
        {
            _shapeIndex++;
            return;
        }

        if (_sameShape)
        {
            _sameShape = false;
            for (var i = 0; i < _shapeIndex; i++)
            {
                var known = _flatShape[i];
                _shapeBuild.Add(known);
                _flatFields.Add(FieldOf(_shapeKeys[i], known));
            }
        }

        var made = InternAscii(bytes);
        var key = kind switch
        {
            NameKind.Attribute => FieldKey.Attr("", made),
            NameKind.Text => FieldKey.Text,
            _ => FieldKey.Elem("", made),
        };
        _shapeBuild.Add(made);
        _flatFields.Add(FieldOf(key, made));
        _shapeIndex = int.MaxValue;
    }

    private void AcceptText()
    {
        if (_sameShape
            && _shapeIndex < _flatShape.Count
            && _shapeKeys[_shapeIndex].Kind == NameKind.Text)
        {
            _shapeIndex++;
            return;
        }

        if (_sameShape)
        {
            _sameShape = false;
            for (var i = 0; i < _shapeIndex; i++)
            {
                var known = _flatShape[i];
                _shapeBuild.Add(known);
                _flatFields.Add(FieldOf(_shapeKeys[i], known));
            }
        }

        _shapeBuild.Add(_hashText);
        _flatFields.Add(new FlatField(FieldKey.Text, _hashText));
        _shapeIndex = int.MaxValue;
    }

    private FlatField FieldOf(FieldKey key, string local)
    {
        var name = key.Kind == NameKind.Attribute ? AttributeColumn(local) : local;
        return new FlatField(key, name);
    }

    private void FinishShape()
    {
        if (OmitFlatShape)
            return;
        if (_haveShape && _sameShape && _shapeIndex == _flatShape.Count)
        {
            FlatSameShape = true;
            _shapeIndex = 0;
            return;
        }

        if (_sameShape)
        {
            for (var i = 0; i < _shapeIndex; i++)
            {
                var known = _flatShape[i];
                _shapeBuild.Add(known);
                _flatFields.Add(FieldOf(_shapeKeys[i], known));
            }
        }

        FlatSameShape = false;
        _flatShape.Clear();
        _shapeKeys.Clear();
        _flatShape.AddRange(_shapeBuild);
        foreach (var field in _flatFields)
            _shapeKeys.Add(field.Key);
        _shapeBuild.Clear();
        _shapeIndex = 0;
        _haveShape = true;
    }

    private static bool AsciiEqual(ReadOnlySpan<byte> bytes, string text)
    {
        for (var i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] != (byte)text[i])
                return false;
        }

        return true;
    }

    private bool FlatText(ReadOnlySpan<byte> text, bool record, ref bool sawText)
    {
        if (text.IsEmpty)
            return true;
        if (text.IndexOf((byte)'&') >= 0)
            return false;
        if (text.IndexOfAnyInRange((byte)0x80, (byte)0xFF) >= 0)
            return FlatTextSlow(text, record, ref sawText);
        if (record && !sawText && text.IndexOfAnyExcept(SpaceBytes) >= 0)
        {
            sawText = true;
            AcceptText();
        }

        return true;
    }

    private bool FlatTextSlow(ReadOnlySpan<byte> text, bool record, ref bool sawText)
    {
        var index = 0;
        var nonWs = false;
        while (index < text.Length)
        {
            var b = text[index];
            if (b == (byte)'&')
                return false;
            if (b >= 0x80)
            {
                if (!SkipUtf8(text, ref index))
                    return false;
                nonWs = true;
                continue;
            }

            if (!IsXmlSpace(b))
                nonWs = true;
            index++;
        }

        if (record && nonWs && !sawText)
        {
            sawText = true;
            AcceptText();
        }

        return true;
    }

    private bool SkipFlatAttributes(ReadOnlySpan<byte> span, ref int index, bool record)
    {
        while (true)
        {
            while (index < span.Length && IsXmlSpace(span[index]))
                index++;
            if (index >= span.Length)
                return false;
            if (span[index] is (byte)'>' or (byte)'/')
                return true;
            if (!ScanName(span, ref index, out var attrAt, out var attrLen))
                return false;
            var attr = span.Slice(attrAt, attrLen);
            if (attr.Contains((byte)':') || attr.SequenceEqual("xmlns"u8))
                return false;
            if (record)
            {
                for (var seen = 0; seen < _attrSpans.Count; seen++)
                {
                    var prev = _attrSpans[seen];
                    if (prev.Len == attrLen && span.Slice(prev.At, prev.Len).SequenceEqual(attr))
                        return false;
                }

                _attrSpans.Add((attrAt, attrLen));
                Accept(attr, NameKind.Attribute);
            }

            while (index < span.Length && IsXmlSpace(span[index]))
                index++;
            if (index >= span.Length)
                return false;
            if (span[index] != (byte)'=')
                continue;
            index++;
            while (index < span.Length && IsXmlSpace(span[index]))
                index++;
            if (index >= span.Length || span[index] is not ((byte)'"' or (byte)'\''))
                return false;
            if (!SkipQuoted(span, ref index))
                return false;
        }
    }

    private bool SkipQuoted(ReadOnlySpan<byte> span, ref int index)
    {
        if (index >= span.Length)
            return false;
        var quote = span[index++];
        var rest = span[index..];
        var rel = rest.IndexOf(quote);
        if (rel < 0)
            return false;
        var body = rest[..rel];
        if (body.IndexOf((byte)'<') >= 0 || (OmitFlatShape && body.IndexOf((byte)'&') >= 0))
            return false;
        if (OmitFlatShape)
        {
            index += rel + 1;
            return true;
        }

        var high = body.IndexOfAnyInRange((byte)0x80, (byte)0xFF);
        if (high >= 0)
        {
            var cursor = high;
            while (cursor < body.Length)
            {
                if (body[cursor] < 0x80)
                {
                    cursor++;
                    continue;
                }

                if (!SkipUtf8(body, ref cursor))
                    return false;
            }
        }

        index += rel + 1;
        return true;
    }

    private bool SkipStartTag(ReadOnlySpan<byte> span, ref int index, out bool empty)
    {
        empty = false;
        index++;
        while ((uint)index < (uint)span.Length)
        {
            var mark = span[index];
            if (mark == (byte)'>')
            {
                index++;
                return true;
            }

            if (mark == (byte)'/' && index + 1 < span.Length && span[index + 1] == (byte)'>')
            {
                index += 2;
                empty = true;
                return true;
            }

            if (mark is (byte)'"' or (byte)'\'')
            {
                index++;
                var rel = span[index..].IndexOf(mark);
                if (rel < 0)
                    return false;
                var body = span.Slice(index, rel);
                if (body.IndexOf((byte)'<') >= 0 || (OmitFlatShape && body.IndexOf((byte)'&') >= 0))
                    return false;
                index += rel + 1;
                continue;
            }

            if (mark == (byte)'&')
                return false;
            index++;
        }

        return false;
    }

    private string AttributeColumn(string local)
    {
        if (_atColumns.TryGetValue(local, out var found))
            return found;
        var text = "@" + local;
        _atColumns[local] = text;
        return text;
    }

    private static bool BytesEqual(ReadOnlySpan<byte> bytes, string text)
    {
        if (bytes.Length != text.Length)
            return false;
        for (var i = 0; i < text.Length; i++)
        {
            if (bytes[i] != (byte)text[i])
                return false;
        }

        return true;
    }

    // A start tag with no needle, entity, or prefix needs no attribute objects. The text reader still sees a body.
    private bool TrySearchMiss(out XmlEvent ev)
    {
        ev = _event;
        if (!TrySpan(out var span, out _) || span.Length < 3 || span[0] != (byte)'<' || span[1] is (byte)'/' or (byte)'!' or (byte)'?')
            return false;
        var i = 1;
        if (!ScanName(span, ref i, out var nameAt, out var nameLen) || span.Slice(nameAt, nameLen).Contains((byte)':'))
            return false;

        var tag = i;
        var quote = (byte)0;
        while ((uint)tag < (uint)span.Length)
        {
            var b = span[tag];
            if (quote != 0)
            {
                if (b == quote)
                    quote = 0;
                else if (b is (byte)'<' or (byte)'&' || b >= 0x80)
                    return false;
                tag++;
                continue;
            }

            if (b is (byte)'"' or (byte)'\'')
            {
                quote = b;
                tag++;
                continue;
            }

            if (b is (byte)'<' or (byte)'&' || b >= 0x80)
                return false;
            if (b == (byte)'/')
            {
                var j = tag + 1;
                while ((uint)j < (uint)span.Length && IsXmlSpace(span[j]))
                    j++;
                if ((uint)j >= (uint)span.Length || span[j] != (byte)'>')
                    return false;
                return FinishSearchMiss(span, j + 1, nameAt, nameLen, empty: true, out ev);
            }

            if (b == (byte)'>')
                return FinishSearchMiss(span, tag + 1, nameAt, nameLen, empty: false, out ev);
            tag++;
        }

        return false;
    }

    private bool FinishSearchMiss(ReadOnlySpan<byte> span, int consumed, int nameAt, int nameLen, bool empty, out XmlEvent ev)
    {
        var body = span.Slice(0, consumed);
        if (body.IndexOf((byte)':') >= 0 || body.IndexOf(_searchNeedle) >= 0 || body.IndexOf("xmlns"u8) >= 0)
        {
            ev = _event;
            return false;
        }

        var hasAttribute = body.Slice(nameAt + nameLen).Contains((byte)'=');
        if (!CommitPlainStart(span, consumed, nameAt, nameLen, empty, out ev))
            return false;
        ev.SearchMiss = true;
        ev.MissAttribute = hasAttribute;
        return true;
    }

    private bool TryPlainMarkup(out XmlEvent ev)
    {
        ev = _event;
        if (!TrySpan(out var span, out _) || span.Length < 2 || span[0] != (byte)'<')
            return false;
        if (span[1] == (byte)'/')
            return TryPlainEnd(span, out ev);
        if (span[1] is (byte)'!' or (byte)'?')
            return false;
        return TryPlainStart(span, out ev);
    }

    private bool TryPlainEnd(ReadOnlySpan<byte> span, out XmlEvent ev)
    {
        ev = _event;
        var i = 2;
        if (!ScanName(span, ref i, out var nameAt, out var nameLen) || !SplitAsciiName(span.Slice(nameAt, nameLen), out var prefix, out var local))
            return false;
        while (i < span.Length && IsXmlSpace(span[i]))
            i++;
        if (i >= span.Length || span[i] != (byte)'>')
            return false;
        i++;
        var name = prefix.Length == 0 ? local : prefix + ":" + local;
        if (_open.Count == 0 || !ReferenceEquals(_open.Peek(), name) && _open.Peek() != name)
            return false;
        var start = _byte;
        _byte += i;
        DropFlat();
        _open.Pop();
        _scope = _scopes.Pop();
        if (_open.Count == 0 && !_fragment)
            _phase = Phase.Epilogue;
        ev = Prepare(XmlEventKind.End);
        ev.Prefix = prefix;
        ev.Local = local;
        ev.Name = name;
        ev.ByteStart = start;
        ev.ByteEnd = _byte;
        return true;
    }

    private bool TryPlainStart(ReadOnlySpan<byte> span, out XmlEvent ev)
    {
        ev = _event;
        var i = 1;
        if (!ScanName(span, ref i, out var nameAt, out var nameLen))
            return false;
        var qualify = span.Slice(nameAt, nameLen).Contains((byte)':');
        var marked = _raw.Count;
        while (true)
        {
            if (i >= span.Length)
            {
                _raw.RemoveRange(marked, _raw.Count - marked);
                return false;
            }

            if (span[i] == (byte)'>')
                return CommitStart(span, i + 1, nameAt, nameLen, empty: false, qualify, marked, out ev);
            if (span[i] == (byte)'/' && i + 1 < span.Length && span[i + 1] == (byte)'>')
                return CommitStart(span, i + 2, nameAt, nameLen, empty: true, qualify, marked, out ev);
            if (!IsXmlSpace(span[i]))
            {
                _raw.RemoveRange(marked, _raw.Count - marked);
                return false;
            }

            while (i < span.Length && IsXmlSpace(span[i]))
                i++;
            if (i >= span.Length || span[i] is (byte)'>' or (byte)'/')
                continue;
            if (!ScanName(span, ref i, out var attrAt, out var attrLen))
            {
                _raw.RemoveRange(marked, _raw.Count - marked);
                return false;
            }

            var attrName = span.Slice(attrAt, attrLen);
            if (!SplitAsciiName(attrName, out var ap, out var al))
            {
                _raw.RemoveRange(marked, _raw.Count - marked);
                return false;
            }

            if (ap.Length > 0 || al == "xmlns")
                qualify = true;

            while (i < span.Length && IsXmlSpace(span[i]))
                i++;
            if (i >= span.Length)
            {
                _raw.RemoveRange(marked, _raw.Count - marked);
                return false;
            }

            TextPiece piece;
            if (span[i] == (byte)'=')
            {
                i++;
                while (i < span.Length && IsXmlSpace(span[i]))
                    i++;
                if (i >= span.Length || span[i] is not ((byte)'"' or (byte)'\''))
                {
                    _raw.RemoveRange(marked, _raw.Count - marked);
                    return false;
                }

                var quote = span[i];
                i++;
                var valueAt = i;
                var rest = span[i..];
                var rel = rest.IndexOf(quote);
                if (rel < 0)
                {
                    _raw.RemoveRange(marked, _raw.Count - marked);
                    return false;
                }

                var body = rest[..rel];
                if (body.IndexOf((byte)'<') >= 0 || body.IndexOf((byte)'&') >= 0)
                {
                    _raw.RemoveRange(marked, _raw.Count - marked);
                    return false;
                }

                var high = body.IndexOfAnyInRange((byte)0x80, (byte)0xFF);
                if (high >= 0)
                {
                    var cursor = high;
                    while (cursor < body.Length)
                    {
                        if (body[cursor] < 0x80)
                        {
                            cursor++;
                            continue;
                        }

                        if (!SkipUtf8(body, ref cursor))
                        {
                            _raw.RemoveRange(marked, _raw.Count - marked);
                            return false;
                        }
                    }
                }

                i += rel;
                piece = TextPiece.Bytes(_byte + valueAt, _byte + i, attribute: true);
                i++;
            }
            else
            {
                piece = TextPiece.Bytes(_byte + i, _byte + i, attribute: true);
            }

            _raw.Add(new RawAttr(ap, al, piece));
        }
    }

    private bool CommitStart(ReadOnlySpan<byte> span, int consumed, int nameAt, int nameLen, bool empty, bool qualify, int marked, out XmlEvent ev)
    {
        if (!qualify)
            return CommitPlainStart(span, consumed, nameAt, nameLen, empty, out ev);
        if (!SplitAsciiName(span.Slice(nameAt, nameLen), out var prefix, out var local))
        {
            _raw.RemoveRange(marked, _raw.Count - marked);
            ev = _event;
            return false;
        }

        var start = _byte;
        _byte += consumed;
        ev = FinishStart(prefix, local, empty, start);
        return true;
    }

    private bool CommitPlainStart(ReadOnlySpan<byte> span, int consumed, int nameAt, int nameLen, bool empty, out XmlEvent ev)
    {
        var local = InternAscii(span.Slice(nameAt, nameLen));
        var start = _byte;
        _byte += consumed;
        _seen.Clear();
        foreach (var attr in _raw)
        {
            if (!_seen.Add(("", attr.Local)))
            {
                ev = Fail("An attribute is repeated.");
                return true;
            }

            var xmlAttr = _attrPool.Count > 0 ? _attrPool.Pop() : new XmlAttr();
            xmlAttr.Prefix = "";
            xmlAttr.Local = attr.Local;
            xmlAttr.Ns = "";
            xmlAttr.Name = attr.Local;
            xmlAttr.Value = attr.Value;
            _attrs.Add(xmlAttr);
        }

        ev = Prepare(XmlEventKind.Start);
        StartCount++;
        ev.Local = local;
        ev.Name = local;
        ev.Empty = empty;
        ev.ByteStart = start;
        ev.ByteEnd = _byte;
        ev.Scope = _scope;
        ev.Attributes = _attrs;
        if (!empty)
        {
            _scopes.Push(_scope);
            _open.Push(local);
            if (_phase == Phase.Prologue)
                _phase = Phase.Content;
        }
        else if (_open.Count == 0 && !_fragment)
        {
            _phase = Phase.Epilogue;
        }
        else if (_phase == Phase.Prologue)
        {
            _phase = Phase.Content;
        }

        return true;
    }

    private static bool ScanName(ReadOnlySpan<byte> span, ref int index, out int start, out int length)
    {
        start = index;
        length = 0;
        if (index >= span.Length || !IsAsciiNameStart(span[index]) || span[index] >= 0x80)
            return false;
        index++;
        while (index < span.Length && IsAsciiNameChar(span[index]))
            index++;
        if (index == span.Length)
            return false;
        length = index - start;
        return length > 0;
    }

    private static bool IsXmlSpace(byte value) => value is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n';

    private void Locate(long offset)
    {
        _line = 1;
        _column = 1;
        _afterCr = false;
        var scratch = new byte[64 * 1024];
        var pos = 0L;
        while (pos < offset)
        {
            var want = (int)Math.Min(scratch.Length, offset - pos);
            var read = _document.Source.Read(pos, scratch.AsSpan(0, want));
            if (read <= 0)
                break;
            AdvanceLines(scratch.AsSpan(0, read));
            pos += read;
        }
    }

    private void AdvanceLines(ReadOnlySpan<byte> span)
    {
        if (span.IsEmpty)
            return;
        var start = 0;
        if (_afterCr)
        {
            if (span[0] == (byte)'\n')
                start = 1;
            _afterCr = false;
        }

        var body = span[start..];
        if (body.IsEmpty)
            return;
        var cr = body.Count((byte)'\r');
        var lf = body.Count((byte)'\n');
        _line += cr + lf - CountCrlf(body);
        var last = body.LastIndexOfAny((byte)'\r', (byte)'\n');
        if (last >= 0)
            _column = body.Length - last;
        else
            _column += body.Length;
        _afterCr = body[^1] == (byte)'\r';
    }

    private static int CountCrlf(ReadOnlySpan<byte> body)
    {
        var count = 0;
        var search = body;
        while (true)
        {
            var i = search.IndexOf((byte)'\r');
            if (i < 0 || i + 1 >= search.Length)
                break;
            if (search[i + 1] == (byte)'\n')
                count++;
            search = search[(i + 1)..];
        }

        return count;
    }

    private static int TrailingBrackets(ReadOnlySpan<byte> run, int previous)
    {
        var count = 0;
        for (var i = run.Length - 1; i >= 0 && run[i] == (byte)']'; i--)
        {
            count++;
            if (count >= 2)
                break;
        }

        if (count == run.Length)
            return Math.Min(2, previous + run.Length);
        return count;
    }

    private static bool IsAsciiNameStart(byte value) =>
        value is (byte)'_' or (byte)':' || value is >= (byte)'A' and <= (byte)'Z' or >= (byte)'a' and <= (byte)'z';

    private static bool IsAsciiNameChar(byte value) =>
        IsAsciiNameStart(value) || value is (byte)'-' or (byte)'.' or >= (byte)'0' and <= (byte)'9';

    private readonly record struct Utf8Take(int Count, string? Error);

    // Same acceptance as CharDecoders.DecodeUtf8. A partial tail is left for the next window.
    private static Utf8Take MeasureUtf8(ReadOnlySpan<byte> run, bool allowPartialTail)
    {
        var i = 0;
        while (i < run.Length)
        {
            var first = run[i];
            if (first < 0x80)
            {
                i++;
                continue;
            }

            var needed = first switch
            {
                >= 0xC2 and <= 0xDF => 2,
                >= 0xE0 and <= 0xEF => 3,
                >= 0xF0 and <= 0xF4 => 4,
                _ => 0,
            };
            if (needed == 0)
                return new Utf8Take(i, "The document is not valid UTF-8.");
            if (i + needed > run.Length)
            {
                return allowPartialTail
                    ? new Utf8Take(i, null)
                    : new Utf8Take(i, "The document ends in the middle of a character.");
            }

            for (var n = 1; n < needed; n++)
            {
                if ((run[i + n] & 0xC0) != 0x80)
                    return new Utf8Take(i, "The document is not valid UTF-8.");
            }

            var code = needed switch
            {
                2 => ((first & 0x1F) << 6) | (run[i + 1] & 0x3F),
                3 => ((first & 0x0F) << 12) | ((run[i + 1] & 0x3F) << 6) | (run[i + 2] & 0x3F),
                _ => ((first & 0x07) << 18) | ((run[i + 1] & 0x3F) << 12) | ((run[i + 2] & 0x3F) << 6) | (run[i + 3] & 0x3F),
            };
            if (code > 0x10FFFF || code is >= 0xD800 and <= 0xDFFF)
                return new Utf8Take(i, "The document contains a character XML does not allow.");
            i += needed;
        }

        return new Utf8Take(i, null);
    }

    private static bool SkipUtf8(ReadOnlySpan<byte> span, ref int index)
    {
        if ((uint)index >= (uint)span.Length || span[index] < 0x80)
            return false;
        var needed = span[index] switch
        {
            >= 0xC2 and <= 0xDF => 2,
            >= 0xE0 and <= 0xEF => 3,
            >= 0xF0 and <= 0xF4 => 4,
            _ => 0,
        };
        if (needed == 0 || index + needed > span.Length)
            return false;
        var measured = MeasureUtf8(span.Slice(index, needed), allowPartialTail: false);
        if (measured.Error is not null || measured.Count != needed)
            return false;
        index += needed;
        return true;
    }

    private enum Phase { Prologue, Content, Epilogue, Finished }

    private enum Signal { None, NeedMore, Eof }

    private struct Checkpoint
    {
        public long Byte;
        public int Line;
        public int Column;
        public bool AfterCr;
        public int Depth;
        public int CharPos;
        public bool HasLow;
        public char Low;
        public long TextFrom;
        public bool TextNonWs;
        public int TextLine;
        public int TextColumn;
        public bool TextAfterCr;
        public int Brackets;
    }

    private sealed class CharInput
    {
        public int EntityId;
        public string Text = "";
        public int Position;
    }

    private readonly record struct RawAttr(string Prefix, string Local, TextPiece Value);
}

internal readonly record struct FlatField(FieldKey Key, string Name);

internal sealed class ElementShape
{
    public required string Parent { get; init; }
    public required string Child { get; init; }
    public string[]? FlatShape { get; set; }
    public FieldKey[]? ShapeKeys { get; set; }
    public FlatField[]? Fields { get; set; }
}
