using System.Text;

namespace BigJsonXml.Xml;

internal enum XmlEventKind
{
    NeedMore,
    Start,
    End,
    Text,
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
    public TextPiece Text;
    public bool HasText;
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

    private XmlParser(XmlDocument document, bool fragment)
    {
        _document = document;
        _fragment = fragment;
    }

    public static XmlParser Create(XmlDocument document) => new(document, false);

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
        _mark = Save();
        _signal = Signal.None;
        var ev = ReadCore();
        if (_signal == Signal.NeedMore)
        {
            Restore(_mark);
            return XmlEvent.More();
        }

        return _error is null ? ev : ErrorEvent();
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

    private XmlEvent ReadMarkup()
    {
        var startByte = _byte;
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
                return new XmlEvent
                {
                    Kind = XmlEventKind.Text,
                    HasText = true,
                    Text = entity < 0
                        ? TextPiece.Bytes(start, end, cdata: true)
                        : new TextPiece { EntityId = entity, Start = start, End = end, CData = true },
                };
            }
        }
    }

    private XmlEvent? ReadText()
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
                    return new XmlEvent
                    {
                        Kind = XmlEventKind.Text,
                        HasText = true,
                        Text = entity < 0 ? TextPiece.Bytes(start, before) : TextPiece.Chars(entity, start, before),
                    };
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
        return new XmlEvent
        {
            Kind = XmlEventKind.Text,
            HasText = true,
            Text = entity < 0 ? TextPiece.Bytes(start, end) : TextPiece.Chars(entity, start, end),
        };
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
        var raw = new List<RawAttr>();
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
            if (!SkipSpaceAllowEmpty() || !Expect("=") || !SkipSpaceAllowEmpty())
                return SignalEvent();
            if (!ReadQuoted(out var piece))
                return SignalEvent();
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

        var seen = new HashSet<(string, string)>();
        var attributes = new List<XmlAttr>(raw.Count);
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
            if (!seen.Add((ns, attr.Prefix == "xmlns" ? attr.Local : attr.Local == "xmlns" && attr.Prefix.Length == 0 ? "xmlns" : attr.Local)))
                return Fail("An attribute is repeated.");
            attributes.Add(new XmlAttr
            {
                Prefix = attr.Prefix,
                Local = identityLocal,
                Ns = ns,
                Name = Intern(display),
                Value = attr.Value,
            });
        }

        var elementNs = prefix.Length == 0
            ? scope.Default
            : scope.TryResolve(prefix, out var resolved) ? resolved : "";
        var name = prefix.Length == 0 ? local : prefix + ":" + local;
        var ev = new XmlEvent
        {
            Kind = XmlEventKind.Start,
            Prefix = Intern(prefix),
            Local = Intern(local),
            Ns = elementNs,
            Name = Intern(name),
            Empty = empty,
            ByteStart = byteStart,
            ByteEnd = _inputs.Count == 0 ? _byte : byteStart,
            Scope = scope,
            Attributes = attributes,
        };
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
        _open.Pop();
        _scope = _scopes.Pop();
        if (_open.Count == 0 && !_fragment)
            _phase = Phase.Epilogue;
        return new XmlEvent
        {
            Kind = XmlEventKind.End,
            Prefix = prefix,
            Local = local,
            Name = name,
            ByteStart = byteStart,
            ByteEnd = _inputs.Count == 0 ? _byte : byteStart,
        };
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
            return Fail("External DTD subsets are not allowed.") is XmlEvent && false;
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

        if (!ReadQuotedLiteral(out var replacement))
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

    private bool ReadQuoted(out TextPiece piece)
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

            if (c == '<')
                return Fail("A '<' is not allowed in this value.") is XmlEvent && false;
            if (c == '&' && !ReadReference(attribute: true, out _))
                return false;
        }

        piece = entity < 0 ? TextPiece.Bytes(start, end, attribute: true) : TextPiece.Chars(entity, start, end, attribute: true);
        return true;
    }

    private bool ReadQuotedLiteral(out string value)
    {
        value = "";
        if (!ReadQuoted(out var piece))
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
        foreach (var expected in text)
        {
            if (!ReadChar(out var c) || c != expected)
            {
                Restore(save);
                return false;
            }
        }

        Restore(save);
        return _error is null && _signal == Signal.None;
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
    }

    private static bool IsNameStart(char value) => value is '_' or ':' || char.IsLetter(value);

    private static bool IsNameChar(char value) => IsNameStart(value) || value is '-' or '.' || char.IsDigit(value);

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
    }

    private sealed class CharInput
    {
        public int EntityId;
        public string Text = "";
        public int Position;
    }

    private readonly record struct RawAttr(string Prefix, string Local, TextPiece Value);
}
