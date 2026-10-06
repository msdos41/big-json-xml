using System.Text;
using BigJsonXml.Xml;

namespace BigJsonXml.Json;

internal enum JsonEventKind
{
    NeedMore,
    Continue,
    Error,
    Done,
    Refuse,
    ObjectStart,
    ObjectEnd,
    ArrayStart,
    ArrayEnd,
    Key,
    Scalar,
}

internal sealed class JsonEvent
{
    public JsonEventKind Kind;
    public long ByteStart;
    public long ByteEnd;
    public string Name = "";
    public string Message = "";
    public int Line = 1;
    public int Column = 1;
    public long ErrorByte;

    public static JsonEvent More() => new() { Kind = JsonEventKind.NeedMore };

    public static JsonEvent Again() => new() { Kind = JsonEventKind.Continue };
}

internal static class JsonSniff
{
    public enum Kind { NeedMore, Xml, Json, Refuse }

    public readonly struct Result(Kind kind, int line, int column)
    {
        public Kind Kind { get; } = kind;
        public int Line { get; } = line;
        public int Column { get; } = column;
    }

    public static Result Look(IByteSource source)
    {
        long pos = 0;
        if (!PassBom(source, ref pos, out var needBom))
            return needBom ? new Result(Kind.NeedMore, 1, 1) : new Result(Kind.Refuse, 1, 1);

        var line = 1;
        var column = 1;
        var cr = false;
        while (true)
        {
            if (pos >= source.Length)
                return source.IsComplete ? new Result(Kind.Refuse, line, column) : new Result(Kind.NeedMore, line, column);
            Span<byte> one = stackalloc byte[1];
            if (source.Read(pos, one) != 1)
                return new Result(Kind.NeedMore, line, column);
            var b = one[0];
            if (b is (byte)' ' or (byte)'\t' or (byte)'\n' or (byte)'\r')
            {
                pos++;
                if (cr)
                {
                    cr = false;
                    if (b == (byte)'\n')
                        continue;
                }

                if (b == (byte)'\r')
                {
                    line++;
                    column = 1;
                    cr = true;
                }
                else if (b == (byte)'\n')
                {
                    line++;
                    column = 1;
                }
                else
                {
                    column++;
                }

                continue;
            }

            if (b == (byte)'<')
                return new Result(Kind.Xml, line, column);
            if (b is (byte)'{' or (byte)'[' or (byte)'"' or (byte)'-' or (byte)'t' or (byte)'f' or (byte)'n' || b is >= (byte)'0' and <= (byte)'9')
                return new Result(Kind.Json, line, column);
            return new Result(Kind.Refuse, line, column);
        }
    }

    private static bool PassBom(IByteSource source, ref long pos, out bool needMore)
    {
        needMore = false;
        if (source.Length == 0)
        {
            if (!source.IsComplete)
            {
                needMore = true;
                return false;
            }

            return true;
        }
        Span<byte> head = stackalloc byte[3];
        var count = source.Read(0, head);
        if (head[0] != 0xEF)
            return true;
        if (count < 3)
        {
            if (!source.IsComplete && (count < 2 || head[1] == 0xBB))
            {
                needMore = true;
                return false;
            }

            return true;
        }

        if (head[1] == 0xBB && head[2] == 0xBF)
            pos = 3;
        return true;
    }
}

/// <summary>
/// One forward reading of a JSON value. A document read also watches for a second value.
/// </summary>
internal sealed class JsonParser
{
    private const int PhValue = 0;
    private const int PhKey = 1;
    private const int PhKeyRequired = 2;
    private const int PhColon = 3;
    private const int PhObjNext = 4;
    private const int PhArrItem = 5;
    private const int PhArrRequired = 6;
    private const int PhArrNext = 7;
    private const int PhAfter = 8;
    private const int StringBudget = 8192;

    private readonly IByteSource _source;
    private readonly bool _oneValue;
    private readonly Dictionary<string, string> _intern = new(StringComparer.Ordinal);
    private readonly byte[] _window = new byte[8192];
    private int[] _phase = new int[32];
    private int _top;
    private long _pos;
    private long _windowAt = -1;
    private int _windowCount;
    private int _line = 1;
    private int _column = 1;
    private bool _cr;
    private bool _bomDone;
    private bool _done;
    private bool _failed;
    private JsonEvent? _error;
    private bool _inString;
    private bool _stringIsKey;
    private long _stringStart;
    private int _stringLine = 1;
    private int _stringColumn = 1;
    private bool _escape;
    private int _hex;
    private int _hexLeft;
    private int _high;
    private bool _wantLow;
    private StringBuilder? _key;
    private long _markPos;
    private int _markLine = 1;
    private int _markColumn = 1;
    private bool _markCr;

    private JsonParser(IByteSource source, long offset, bool oneValue)
    {
        _source = source;
        _pos = offset;
        _oneValue = oneValue;
        _phase[0] = PhValue;
        if (offset != 0)
            _bomDone = true;
    }

    public long FileByte => _pos < 0 ? 0 : _pos;

    public static JsonParser Document(XmlDocument document) => new(document.Source, 0, oneValue: false);

    public static JsonParser Fragment(XmlDocument document, long offset) => new(document.Source, offset, oneValue: true);

    public JsonEvent Read()
    {
        if (_failed)
            return _error!;
        if (_done)
            return End();
        if (!_bomDone && !SkipBom())
            return JsonEvent.More();
        if (_inString)
            return ScanString();
        while (true)
        {
            if (_top < 0)
            {
                _done = true;
                return End();
            }

            JsonEvent? ev = _phase[_top] switch
            {
                PhValue => ReadValue(),
                PhKey => ReadKey(required: false),
                PhKeyRequired => ReadKey(required: true),
                PhColon => ReadColon(),
                PhObjNext => ReadObjectNext(),
                PhArrItem => ReadArrayItem(required: false),
                PhArrRequired => ReadArrayItem(required: true),
                PhArrNext => ReadArrayNext(),
                _ => ReadAfter(),
            };
            if (ev is not null)
                return ev;
        }
    }

    private JsonEvent? ReadValue()
    {
        var space = SkipSpace();
        if (space == Gap.Need)
            return JsonEvent.More();
        if (space == Gap.End)
            return FailUnfinished();
        if (!TryByte(out var b, out var need))
            return need ? JsonEvent.More() : FailUnfinished();
        if (b == (byte)'{')
        {
            var at = Here();
            Consume(b);
            Replace(PhKey);
            return Event(JsonEventKind.ObjectStart, at);
        }

        if (b == (byte)'[')
        {
            var at = Here();
            Consume(b);
            Replace(PhArrItem);
            return Event(JsonEventKind.ArrayStart, at);
        }

        if (b == (byte)'"')
        {
            BeginString(key: false);
            return ScanString();
        }

        if (b == (byte)'-' || IsDigit(b))
            return ReadNumber();
        if (b == (byte)'t')
            return ReadLiteral("true");
        if (b == (byte)'f')
            return ReadLiteral("false");
        if (b == (byte)'n')
            return ReadLiteral("null");
        return Fail("The byte is not valid JSON.");
    }

    private JsonEvent? ReadKey(bool required)
    {
        var space = SkipSpace();
        if (space == Gap.Need)
            return JsonEvent.More();
        if (space == Gap.End)
            return FailUnfinished();
        if (!TryByte(out var b, out var need))
            return need ? JsonEvent.More() : FailUnfinished();
        if (b == (byte)'}')
            return required ? Fail("The byte is not valid JSON.") : EndObject();
        if (b != (byte)'"')
            return Fail("The byte is not valid JSON.");
        BeginString(key: true);
        return ScanString();
    }

    private JsonEvent? ReadColon()
    {
        var space = SkipSpace();
        if (space == Gap.Need)
            return JsonEvent.More();
        if (space == Gap.End)
            return FailUnfinished();
        if (!TryByte(out var b, out var need))
            return need ? JsonEvent.More() : FailUnfinished();
        if (b != (byte)':')
            return Fail("The byte is not valid JSON.");
        Consume(b);
        Push(PhValue);
        return null;
    }

    private JsonEvent? ReadObjectNext()
    {
        var space = SkipSpace();
        if (space == Gap.Need)
            return JsonEvent.More();
        if (space == Gap.End)
            return FailUnfinished();
        if (!TryByte(out var b, out var need))
            return need ? JsonEvent.More() : FailUnfinished();
        if (b == (byte)'}')
            return EndObject();
        if (b != (byte)',')
            return Fail("The byte is not valid JSON.");
        Consume(b);
        Replace(PhKeyRequired);
        return null;
    }

    private JsonEvent? ReadArrayItem(bool required)
    {
        var space = SkipSpace();
        if (space == Gap.Need)
            return JsonEvent.More();
        if (space == Gap.End)
            return FailUnfinished();
        if (!TryByte(out var b, out var need))
            return need ? JsonEvent.More() : FailUnfinished();
        if (b == (byte)']')
            return required ? Fail("The byte is not valid JSON.") : EndArray();
        Push(PhValue);
        return null;
    }

    private JsonEvent? ReadArrayNext()
    {
        var space = SkipSpace();
        if (space == Gap.Need)
            return JsonEvent.More();
        if (space == Gap.End)
            return FailUnfinished();
        if (!TryByte(out var b, out var need))
            return need ? JsonEvent.More() : FailUnfinished();
        if (b == (byte)']')
            return EndArray();
        if (b != (byte)',')
            return Fail("The byte is not valid JSON.");
        Consume(b);
        Replace(PhArrRequired);
        return null;
    }

    private JsonEvent ReadAfter()
    {
        var space = SkipSpace();
        if (space == Gap.Need)
            return JsonEvent.More();
        if (space == Gap.End)
        {
            _done = true;
            return End();
        }

        if (!TryByte(out var b, out var need))
            return need ? JsonEvent.More() : End();
        if (IsValueStart(b))
            return Refuse();
        return Fail("The byte is not valid JSON.");
    }

    private JsonEvent EndObject()
    {
        var at = Here();
        Consume((byte)'}');
        Pop();
        FinishValue();
        return Event(JsonEventKind.ObjectEnd, at);
    }

    private JsonEvent EndArray()
    {
        var at = Here();
        Consume((byte)']');
        Pop();
        FinishValue();
        return Event(JsonEventKind.ArrayEnd, at);
    }

    private void FinishValue()
    {
        if (_top < 0)
        {
            if (_oneValue)
                _done = true;
            else
            {
                _top = 0;
                _phase[0] = PhAfter;
            }

            return;
        }

        if (_phase[_top] == PhColon)
            _phase[_top] = PhObjNext;
        else if (_phase[_top] is PhArrItem or PhArrRequired)
            _phase[_top] = PhArrNext;
    }

    private JsonEvent ReadLiteral(string word)
    {
        Mark();
        for (var i = 0; i < word.Length; i++)
        {
            if (!TryByte(out var b, out var need))
            {
                Rewind();
                return need ? JsonEvent.More() : FailUnfinished();
            }

            if (b != (byte)word[i])
                return Fail("The byte is not valid JSON.");
            Consume(b);
        }

        var start = new Place(_markPos, _markLine, _markColumn);
        Pop();
        FinishValue();
        return Event(JsonEventKind.Scalar, start);
    }

    private JsonEvent ReadNumber()
    {
        Mark();
        if (!TryByte(out var first, out var need))
            return need ? JsonEvent.More() : FailUnfinished();
        if (first == (byte)'-')
        {
            Consume(first);
            if (!TryByte(out first, out need))
            {
                Rewind();
                return need ? JsonEvent.More() : FailUnfinished();
            }

            if (!IsDigit(first))
                return Fail("The byte is not valid JSON.");
        }

        Consume(first);
        if (first == (byte)'0')
        {
            if (!TryByte(out var after, out need))
            {
                if (need)
                {
                    Rewind();
                    return JsonEvent.More();
                }
            }
            else if (IsDigit(after))
            {
                return Fail("The byte is not valid JSON.");
            }
        }
        else
        {
            var moreDigits = TakeDigits();
            if (moreDigits is Digit.Need)
            {
                Rewind();
                return JsonEvent.More();
            }
        }

        if (!TryByte(out var mark, out need))
        {
            if (need)
            {
                Rewind();
                return JsonEvent.More();
            }
        }
        else if (mark == (byte)'.')
        {
            Consume(mark);
            var fraction = TakeDigits();
            if (fraction == Digit.Need)
            {
                Rewind();
                return JsonEvent.More();
            }

            if (fraction == Digit.None)
                return FailUnfinished();
        }

        if (!TryByte(out mark, out need))
        {
            if (need)
            {
                Rewind();
                return JsonEvent.More();
            }
        }
        else if (mark is (byte)'e' or (byte)'E')
        {
            Consume(mark);
            if (!TryByte(out var exp, out need))
            {
                Rewind();
                return need ? JsonEvent.More() : FailUnfinished();
            }

            if (exp is (byte)'+' or (byte)'-')
            {
                Consume(exp);
                if (!TryByte(out exp, out need))
                {
                    Rewind();
                    return need ? JsonEvent.More() : FailUnfinished();
                }
            }

            if (!IsDigit(exp))
                return Fail("The byte is not valid JSON.");
            Consume(exp);
            var exponent = TakeDigits();
            if (exponent == Digit.Need)
            {
                Rewind();
                return JsonEvent.More();
            }
        }

        var start = new Place(_markPos, _markLine, _markColumn);
        Pop();
        FinishValue();
        return Event(JsonEventKind.Scalar, start);
    }

    private enum Digit { None, Some, Need }

    private Digit TakeDigits()
    {
        var any = false;
        while (true)
        {
            if (!TryByte(out var b, out var need))
                return need ? Digit.Need : any ? Digit.Some : Digit.None;
            if (!IsDigit(b))
                return any ? Digit.Some : Digit.None;
            Consume(b);
            any = true;
        }
    }

    private void BeginString(bool key)
    {
        _inString = true;
        _stringIsKey = key;
        _stringStart = _pos;
        _stringLine = _line;
        _stringColumn = _column;
        Consume((byte)'"');
        _escape = false;
        _hexLeft = 0;
        _wantLow = false;
        if (key)
        {
            _key ??= new StringBuilder();
            _key.Clear();
        }
    }

    private JsonEvent ScanString()
    {
        var steps = 0;
        while (true)
        {
            if (!TryByte(out var b, out var need))
            {
                if (need)
                    return JsonEvent.More();
                _inString = false;
                return FailUnfinished();
            }

            if (_hexLeft > 0)
            {
                var nibble = Hex(b);
                if (nibble < 0)
                {
                    _inString = false;
                    return Fail("The byte is not valid JSON.");
                }

                Consume(b);
                _hex = (_hex << 4) | nibble;
                _hexLeft--;
                if (_hexLeft == 0 && !AcceptUnit(_hex))
                {
                    _inString = false;
                    return Fail("The byte is not valid JSON.");
                }
            }
            else if (_escape)
            {
                _escape = false;
                if (b == (byte)'u')
                {
                    Consume(b);
                    _hex = 0;
                    _hexLeft = 4;
                }
                else if (!Mapped(b, out var unit))
                {
                    _inString = false;
                    return Fail("The byte is not valid JSON.");
                }
                else
                {
                    Consume(b);
                    if (!AcceptUnit(unit))
                    {
                        _inString = false;
                        return Fail("The byte is not valid JSON.");
                    }
                }
            }
            else if (b == (byte)'"')
            {
                if (_wantLow)
                {
                    _inString = false;
                    return Fail("The byte is not valid JSON.");
                }

                Consume(b);
                _inString = false;
                return FinishString();
            }
            else if (b == (byte)'\\')
            {
                Consume(b);
                _escape = true;
            }
            else if (b < 0x20)
            {
                _inString = false;
                return Fail("The byte is not valid JSON.");
            }
            else if (b < 0x80)
            {
                if (_wantLow)
                {
                    _inString = false;
                    return Fail("The byte is not valid JSON.");
                }

                Consume(b);
                if (_stringIsKey)
                    _key!.Append((char)b);
            }
            else
            {
                if (_wantLow)
                {
                    _inString = false;
                    return Fail("The byte is not valid JSON.");
                }

                if (!LookUtf8(out var point, out var size, out var utfNeed))
                {
                    if (utfNeed)
                        return JsonEvent.More();
                    _inString = false;
                    return Fail("The document is not valid UTF-8.");
                }

                if (point < 0x20)
                {
                    _inString = false;
                    return Fail("The byte is not valid JSON.");
                }

                Advance(size);
                if (_stringIsKey)
                    AppendPoint(point);
            }

            steps++;
            if (steps < StringBudget)
                continue;
            if (_pos < _source.Length)
                return JsonEvent.Again();
            if (!_source.IsComplete)
                return JsonEvent.More();
        }
    }

    private JsonEvent FinishString()
    {
        var start = new Place(_stringStart, _stringLine, _stringColumn);
        if (_stringIsKey)
        {
            var name = Intern(_key!.ToString());
            Replace(PhColon);
            var ev = Event(JsonEventKind.Key, start);
            ev.Name = name;
            return ev;
        }

        Pop();
        FinishValue();
        return Event(JsonEventKind.Scalar, start);
    }

    private bool AcceptUnit(int unit)
    {
        if (_wantLow)
        {
            if (unit is < 0xDC00 or > 0xDFFF)
                return false;
            _wantLow = false;
            if (_stringIsKey)
            {
                _key!.Append((char)_high);
                _key.Append((char)unit);
            }

            return true;
        }

        if (unit is >= 0xD800 and <= 0xDBFF)
        {
            _wantLow = true;
            _high = unit;
            return true;
        }

        if (unit is >= 0xDC00 and <= 0xDFFF)
            return false;
        if (_stringIsKey)
            _key!.Append((char)unit);
        return true;
    }

    private void AppendPoint(int point)
    {
        if (point <= 0xFFFF)
        {
            _key!.Append((char)point);
            return;
        }

        var rest = point - 0x10000;
        _key!.Append((char)(0xD800 + (rest >> 10)));
        _key.Append((char)(0xDC00 + (rest & 0x3FF)));
    }

    private bool LookUtf8(out int point, out int size, out bool needMore)
    {
        Span<byte> data = stackalloc byte[4];
        Span<byte> one = stackalloc byte[1];
        var have = 0;
        for (var i = 0; i < 4; i++)
        {
            if (_pos + i >= _source.Length)
                break;
            if (_source.Read(_pos + i, one) != 1)
                break;
            data[i] = one[0];
            have++;
        }

        var complete = _source.IsComplete || have == 4;
        return JsonUtf8.TryDecode(data[..have], complete, out point, out size, out needMore);
    }

    private void Advance(int count)
    {
        _pos += count;
        _cr = false;
        _column += 1;
    }

    private string Intern(string name)
    {
        if (_intern.TryGetValue(name, out var existing))
            return existing;
        _intern[name] = name;
        return name;
    }

    private JsonEvent Event(JsonEventKind kind, Place start) => new()
    {
        Kind = kind,
        ByteStart = start.Pos,
        ByteEnd = _pos,
        Line = start.Line,
        Column = start.Column,
    };

    private JsonEvent Fail(string message)
    {
        _failed = true;
        _inString = false;
        _error = new JsonEvent
        {
            Kind = JsonEventKind.Error,
            Message = message,
            ErrorByte = _pos,
            ByteStart = _pos,
            Line = _line,
            Column = _column,
        };
        return _error;
    }

    private JsonEvent FailUnfinished() => Fail("The JSON value is not finished.");

    private JsonEvent Refuse()
    {
        _failed = true;
        _error = new JsonEvent
        {
            Kind = JsonEventKind.Refuse,
            Message = "A JSON document is one value.",
            ErrorByte = _pos,
            ByteStart = _pos,
            Line = _line,
            Column = _column,
        };
        return _error;
    }

    private JsonEvent End() => new() { Kind = JsonEventKind.Done, Line = _line, Column = _column, ByteStart = _pos };

    private enum Gap { Ok, Need, End }

    private Gap SkipSpace()
    {
        while (true)
        {
            if (!TryByte(out var b, out var need))
                return need ? Gap.Need : Gap.End;
            if (b is not ((byte)' ' or (byte)'\t' or (byte)'\n' or (byte)'\r'))
                return Gap.Ok;
            Consume(b);
        }
    }

    private bool SkipBom()
    {
        _bomDone = true;
        if (_pos != 0 || _source.Length == 0)
            return true;
        Span<byte> head = stackalloc byte[3];
        var count = _source.Read(0, head);
        if (count >= 3 && head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF)
            _pos = 3;
        else if (count > 0 && head[0] == 0xEF && !_source.IsComplete && count < 3)
        {
            _bomDone = false;
            return false;
        }

        return true;
    }

    private bool TryByte(out byte value, out bool needMore)
    {
        value = 0;
        needMore = false;
        if (_pos < _windowAt || _pos >= _windowAt + _windowCount)
        {
            if (_pos >= _source.Length)
            {
                needMore = !_source.IsComplete;
                return false;
            }

            var count = _source.Read(_pos, _window);
            if (count <= 0)
            {
                needMore = !_source.IsComplete;
                return false;
            }

            _windowAt = _pos;
            _windowCount = count;
        }

        value = _window[(int)(_pos - _windowAt)];
        return true;
    }

    private void Consume(byte value)
    {
        _pos++;
        if (_cr)
        {
            _cr = false;
            if (value == (byte)'\n')
                return;
        }

        if (value == (byte)'\r')
        {
            _line++;
            _column = 1;
            _cr = true;
            return;
        }

        if (value == (byte)'\n')
        {
            _line++;
            _column = 1;
            return;
        }

        _column++;
    }

    private void Mark()
    {
        _markPos = _pos;
        _markLine = _line;
        _markColumn = _column;
        _markCr = _cr;
    }

    private void Rewind()
    {
        _pos = _markPos;
        _line = _markLine;
        _column = _markColumn;
        _cr = _markCr;
    }

    private Place Here() => new(_pos, _line, _column);

    private void Push(int phase)
    {
        if (_top + 1 >= _phase.Length)
            Array.Resize(ref _phase, _phase.Length * 2);
        _phase[++_top] = phase;
    }

    private void Pop() => _top--;

    private void Replace(int phase) => _phase[_top] = phase;

    private static bool IsDigit(byte value) => value is >= (byte)'0' and <= (byte)'9';

    private static bool IsValueStart(byte value) =>
        value is (byte)'{' or (byte)'[' or (byte)'"' or (byte)'-' or (byte)'t' or (byte)'f' or (byte)'n'
        || value is >= (byte)'0' and <= (byte)'9';

    private static int Hex(byte value) => value switch
    {
        >= (byte)'0' and <= (byte)'9' => value - (byte)'0',
        >= (byte)'a' and <= (byte)'f' => value - (byte)'a' + 10,
        >= (byte)'A' and <= (byte)'F' => value - (byte)'A' + 10,
        _ => -1,
    };

    private static bool Mapped(byte value, out int unit)
    {
        switch (value)
        {
            case (byte)'"': unit = '"'; return true;
            case (byte)'\\': unit = '\\'; return true;
            case (byte)'/': unit = '/'; return true;
            case (byte)'b': unit = '\b'; return true;
            case (byte)'f': unit = '\f'; return true;
            case (byte)'n': unit = '\n'; return true;
            case (byte)'r': unit = '\r'; return true;
            case (byte)'t': unit = '\t'; return true;
            default: unit = 0; return false;
        }
    }

    private readonly record struct Place(long Pos, int Line, int Column);
}
