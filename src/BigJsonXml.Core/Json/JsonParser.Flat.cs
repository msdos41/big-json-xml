using System.Buffers;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace BigJsonXml.Json;

internal interface IJsonItem
{
    void ValueHit(ReadOnlySpan<DocumentPath.Step> trail, int offset, int length);

    void NameHit(ReadOnlySpan<DocumentPath.Step> trail, int offset, int length, string name);
}

internal sealed partial class JsonParser
{
    internal enum ItemTake : byte
    {
        None,
        Keys,
        Value,
        Defer,
        Repeated,
    }

    private enum Walk : byte
    {
        Ok,
        Defer,
        Repeated,
    }

    // One array item, after a finished item has left the phase on the comma.
    // A fancy string or a cut-off window rewinds so the event reader takes that item once.
    internal ItemTake TryTakeArrayItem(long deadline, out long start, out long stopByte, out int stopLine, out int stopColumn)
    {
        start = 0;
        stopByte = 0;
        stopLine = 1;
        stopColumn = 1;
        if (_failed || _done || _inString || _top < 0 || _phase[_top] != PhArrNext)
            return ItemTake.None;
        _sameShape = false;
        if (!_lineDirty)
            _lineAt = _pos;
        else
            _holdLine = true;
        Mark();
        if (SkipSpace() != Gap.Ok || !TryByte(out var head, out _))
        {
            Rewind();
            return ItemTake.Defer;
        }

        if (head == (byte)']')
        {
            _holdLine = false;
            _lineDirty = false;
            _lineAt = _pos;
            return ItemTake.None;
        }
        if (head != (byte)',')
        {
            Rewind();
            return ItemTake.Defer;
        }

        Consume(head);
        if (!_lineDirty)
            _lineAt = _pos;
        _directKeys.Clear();
        var repeated = default(Place);
        var walked = WalkValue(0, deadline, ref repeated, inArray: true);
        if (walked == Walk.Ok)
        {
            if (_holdLine)
                _lineDirty = true;
            else
            {
                _lineDirty = false;
                _lineAt = _pos;
            }

            start = _takenStart;
            return _takenObject ? ItemTake.Keys : ItemTake.Value;
        }

        if (walked == Walk.Repeated)
        {
            stopByte = repeated.Pos;
            stopLine = repeated.Line;
            stopColumn = repeated.Column;
            return ItemTake.Repeated;
        }

        Rewind();
        return ItemTake.Defer;
    }

    // One later array item. Plain ASCII hits are reported. A fancy string rewinds that item.
    internal ItemTake TryVisitItem(long deadline, IJsonItem sink, string needle, byte[] needleBytes)
    {
        _sink = sink;
        _needleText = needle;
        _needleBytes = needleBytes;
        _seeking = true;
        _trailCount = 0;
        try
        {
            return TryTakeArrayItem(deadline, out _, out _, out _, out _);
        }
        finally
        {
            _seeking = false;
            _sink = null;
        }
    }

    private Walk WalkValue(int depth, long deadline, ref Place repeated, bool inArray)
    {
        if (TimedOut(deadline) || SkipSpace() != Gap.Ok || !TryByte(out var kind, out _))
            return Walk.Defer;
        if (depth == 0)
        {
            _takenStart = _pos;
            _takenObject = kind == (byte)'{';
        }

        var pushed = false;
        if (_seeking && inArray && kind != (byte)'{')
        {
            PushTrail("#value");
            ConsiderName("#value");
            pushed = true;
        }

        try
        {
            if (kind == (byte)'{')
                return WalkObject(depth, deadline, ref repeated);
            if (kind == (byte)'[')
                return WalkArray(deadline, ref repeated);
            var start = _pos;
            if (kind == (byte)'"')
            {
                if (!SkipPlain(deadline, out _))
                    return Walk.Defer;
            }
            else if (kind == (byte)'-' || IsDigit(kind))
            {
                if (!WalkNumber())
                    return Walk.Defer;
            }
            else if (kind == (byte)'t')
            {
                if (!WalkLiteral("true"))
                    return Walk.Defer;
            }
            else if (kind == (byte)'f')
            {
                if (!WalkLiteral("false"))
                    return Walk.Defer;
            }
            else if (kind == (byte)'n')
            {
                if (!WalkLiteral("null"))
                    return Walk.Defer;
            }
            else
            {
                return Walk.Defer;
            }

            if (_seeking)
                SearchRaw(start, _pos);
            return Walk.Ok;
        }
        finally
        {
            if (pushed)
                PopTrail();
        }
    }

    private Walk WalkObject(int depth, long deadline, ref Place repeated)
    {
        if (depth == 0 && !_seeking && _haveShape)
            return WalkShaped(deadline);
        Consume((byte)'{');
        var keys = RentKeys();
        var first = true;
        try
        {
            while (true)
            {
                if (TimedOut(deadline) || SkipSpace() != Gap.Ok || !TryByte(out var mark, out _))
                    return Walk.Defer;
                if (mark == (byte)'}')
                {
                    Consume(mark);
                    if (depth == 0 && !_seeking)
                        LearnShape();
                    return Walk.Ok;
                }

                if (!first)
                {
                    if (mark != (byte)',')
                        return Walk.Defer;
                    Consume(mark);
                    if (TimedOut(deadline) || SkipSpace() != Gap.Ok || !TryByte(out mark, out _))
                        return Walk.Defer;
                }

                first = false;
                if (!WalkKey(deadline, out var name, out var quote))
                    return Walk.Defer;
                if (!keys.Add(name))
                {
                    repeated = quote;
                    return Walk.Repeated;
                }

                if (depth == 0)
                    _directKeys.Add(name);
                var named = false;
                if (_seeking)
                {
                    PushTrail(name);
                    ConsiderName(name);
                    named = true;
                }

                try
                {
                    if (SkipSpace() != Gap.Ok || !TryByte(out mark, out _) || mark != (byte)':')
                        return Walk.Defer;
                    Consume(mark);
                    if (SkipSpace() != Gap.Ok || !TryByte(out var next, out _))
                        return Walk.Defer;
                    if (!_seeking && next is (byte)'{' or (byte)'[')
                    {
                        if (!SkipContainer(deadline, next))
                            return Walk.Defer;
                    }
                    else
                    {
                        var inner = WalkValue(depth + 1, deadline, ref repeated, inArray: false);
                        if (inner != Walk.Ok)
                            return inner;
                    }
                }
                finally
                {
                    if (named)
                        PopTrail();
                }
            }
        }
        finally
        {
            keys.Clear();
            if (_keySets.Count < 64)
                _keySets.Push(keys);
        }
    }

    private Walk WalkArray(long deadline, ref Place repeated)
    {
        Consume((byte)'[');
        var first = true;
        var count = 0;
        while (true)
        {
            if (TimedOut(deadline) || SkipSpace() != Gap.Ok || !TryByte(out var mark, out _))
                return Walk.Defer;
            if (mark == (byte)']')
            {
                Consume(mark);
                return Walk.Ok;
            }

            if (!first)
            {
                if (mark != (byte)',')
                    return Walk.Defer;
                Consume(mark);
            }

            first = false;
            count++;
            if (_seeking && _trailCount > 0)
                IndexTrail(count);
            var inner = WalkValue(1, deadline, ref repeated, inArray: true);
            if (inner != Walk.Ok)
                return inner;
        }
    }

    private bool WalkKey(long deadline, out string name, out Place quote)
    {
        name = "";
        quote = Here();
        if (!TryByte(out var mark, out _) || mark != (byte)'"')
            return false;
        var content = _pos + 1;
        if (!SkipPlain(deadline, out var length) || length > StringBudget)
            return false;
        name = InternSource(content, length);
        return true;
    }

    private bool SkipPlain(long deadline, out int length)
    {
        length = 0;
        Consume((byte)'"');
        var content = _pos;
        while (true)
        {
            if (TimedOut(deadline))
                return false;
            if (!Window(out var span, out _))
                return false;
            var stop = span.IndexOfAny(StringStops);
            if (stop < 0)
            {
                Bump(span.Length);
                continue;
            }

            if (stop > 0)
                Bump(stop);
            if (!TryByte(out var mark, out _) || mark != (byte)'"')
                return false;
            length = (int)(_pos - content);
            Consume(mark);
            return true;
        }
    }

    private bool WalkNumber()
    {
        if (!TryByte(out var first, out _))
            return false;
        if (first == (byte)'-')
        {
            Consume(first);
            if (!TryByte(out first, out _) || !IsDigit(first))
                return false;
        }

        if (!IsDigit(first))
            return false;
        Consume(first);
        if (first == (byte)'0')
        {
            if (TryByte(out var after, out var need))
            {
                if (IsDigit(after))
                    return false;
            }
            else if (need)
            {
                return false;
            }
        }
        else if (TakeDigits() == Digit.Need)
        {
            return false;
        }

        if (TryByte(out var mark, out var more))
        {
            if (mark == (byte)'.')
            {
                Consume(mark);
                if (TakeDigits() != Digit.Some)
                    return false;
            }
        }
        else if (more)
        {
            return false;
        }

        if (!TryByte(out mark, out more))
            return !more;
        if (mark is not ((byte)'e' or (byte)'E'))
            return true;
        Consume(mark);
        if (!TryByte(out var exp, out _))
            return false;
        if (exp is (byte)'+' or (byte)'-')
        {
            Consume(exp);
            if (!TryByte(out exp, out _))
                return false;
        }

        if (!IsDigit(exp))
            return false;
        Consume(exp);
        return TakeDigits() != Digit.Need;
    }

    private bool WalkLiteral(string word)
    {
        for (var i = 0; i < word.Length; i++)
        {
            if (!TryByte(out var mark, out _) || mark != (byte)word[i])
                return false;
            Consume(mark);
        }

        return true;
    }

    private void Bump(int count)
    {
        _pos += count;
        if (_holdLine)
        {
            _column += count;
            return;
        }
        _column += count;
        _cr = false;
    }

    private HashSet<string> RentKeys()
    {
        if (!_keySets.TryPop(out var keys))
            return new HashSet<string>(StringComparer.Ordinal);
        return keys;
    }

    // The direct keys already match a previous item. A mismatch rewinds so the event reader records the new columns.
    private Walk WalkShaped(long deadline)
    {
        if (!_lineDirty)
            _lineAt = _pos;
        _holdLine = true;
        _sameShape = false;
        Consume((byte)'{');
        if (!Window(out var span, out _))
            return Walk.Defer;
        var i = 0;
        var charged = 0;
        var origin = _pos;
        var matched = 0;
        var shape = _shapeKeys;
        var afterValue = false;
        var checks = 0;
        while (true)
        {
            if ((++checks & 63) == 0 && TimedOut(deadline))
                return Walk.Defer;
            if (i >= span.Length)
            {
                Charge(span[charged..]);
                _pos = origin + span.Length;
                if (!Window(out span, out _))
                    return Walk.Defer;
                origin = _pos;
                i = 0;
                charged = 0;
                continue;
            }

            i = SkipGap(span, i);
            if (i >= span.Length)
                continue;

            var mark = span[i];
            if (mark == (byte)'}')
            {
                var empty = !afterValue && matched == 0 && shape.Length == 0;
                if (!empty && (!afterValue || matched != shape.Length))
                {
                    DropShape();
                    return Walk.Defer;
                }

                i++;
                _column++;
                Charge(span[charged..i]);
                _pos = origin + i;
                _sameShape = true;
                return Walk.Ok;
            }

            if (afterValue)
            {
                if (mark != (byte)',')
                {
                    DropShape();
                    return Walk.Defer;
                }

                i++;
                _column++;
                afterValue = false;
                continue;
            }

            if (matched == shape.Length || mark != (byte)'"')
            {
                DropShape();
                return Walk.Defer;
            }

            var expect = shape[matched];
            var len = expect.Length;
            var end = i + 1 + len;
            if ((uint)end >= (uint)span.Length)
                return Walk.Defer;
            if (span[end] != (byte)'"' || !span.Slice(i + 1, len).SequenceEqual(expect))
            {
                DropShape();
                return Walk.Defer;
            }

            i = end + 1;
            _column += len + 2;
            matched++;
            if (!SkipShapedSpace(ref span, ref i, ref origin, ref charged))
                return Walk.Defer;
            if (span[i] != (byte)':')
            {
                DropShape();
                return Walk.Defer;
            }

            i++;
            _column++;
            if (!SkipShapedSpace(ref span, ref i, ref origin, ref charged))
                return Walk.Defer;
            mark = span[i];
            if (mark == (byte)'"')
            {
                var valueAt = i;
                var valueClose = PlainClose(span, i);
                if (valueClose < 0)
                    return Walk.Defer;
                i = valueClose + 1;
                _column += i - valueAt;
            }
            else if (mark is (byte)'{' or (byte)'[')
            {
                var opener = origin + i;
                var line = _line;
                var column = _column;
                var cr = _cr;
                var at = _lineAt;
                var dirty = _lineDirty;
                _pos = opener;
                if (!SkipFast(deadline, mark))
                {
                    _pos = opener;
                    _line = line;
                    _column = column;
                    _cr = cr;
                    _lineAt = at;
                    _lineDirty = dirty;
                    _holdLine = false;
                    _windowAt = -1;
                    _windowCount = 0;
                    if (_lineAt < _pos)
                        SyncLine();
                    _lineAt = _pos;
                    _lineDirty = false;
                    if (!SkipContainer(deadline, mark))
                        return Walk.Defer;
                    _lineAt = _pos;
                    _holdLine = true;
                }

                if (!Window(out span, out _))
                    return Walk.Defer;
                origin = _pos;
                i = 0;
                charged = 0;
            }
            else
            {
                var atEnd = origin + span.Length >= _source.Length;
                if (!atEnd && span.Length - i < 24)
                {
                    Charge(span[charged..i]);
                    _pos = origin + i;
                    if (!TryByte(out mark, out _) || !SkipAtom(deadline, mark))
                        return Walk.Defer;
                    if (!Window(out span, out _))
                        return Walk.Defer;
                    origin = _pos;
                    i = 0;
                    charged = 0;
                }
                else
                {
                    var atom = i;
                    if (!EatAtom(span, ref i, atEnd))
                        return Walk.Defer;
                    _column += i - atom;
                }
            }

            afterValue = true;
        }
    }

    private bool SkipShapedSpace(ref ReadOnlySpan<byte> span, ref int i, ref long origin, ref int charged)
    {
        while (true)
        {
            if (i >= span.Length)
            {
                Charge(span[charged..]);
                _pos = origin + i;
                if (!Window(out span, out _))
                    return false;
                origin = _pos;
                i = 0;
                charged = 0;
                continue;
            }

            var next = SkipGap(span, i);
            if (next >= span.Length)
            {
                i = span.Length;
                continue;
            }

            i = next;
            return true;
        }
    }

    private void LearnShape()
    {
        var count = _directKeys.Count;
        if (_shapeKeys.Length != count)
            _shapeKeys = new byte[count][];
        for (var i = 0; i < count; i++)
        {
            var name = _directKeys[i];
            var bytes = _shapeKeys[i];
            if (bytes is null || bytes.Length != name.Length)
                bytes = new byte[name.Length];
            for (var c = 0; c < name.Length; c++)
            {
                var unit = name[c];
                if (unit > 127)
                {
                    DropShape();
                    return;
                }

                bytes[c] = (byte)unit;
            }

            _shapeKeys[i] = bytes;
        }

        _haveShape = true;
    }

    private bool KeyEquals(long content, byte[] expected)
    {
        var length = expected.Length;
        if (content >= _windowAt && content + length <= _windowAt + _windowCount)
            return _window.AsSpan((int)(content - _windowAt), length).SequenceEqual(expected);
        Span<byte> buf = stackalloc byte[128];
        if (length > buf.Length || ReadSource(content, buf[..length]) != length)
            return false;
        return buf[..length].SequenceEqual(expected);
    }

    private bool SkipAtom(long deadline, byte mark)
    {
        if (mark == (byte)'"')
            return SkipPlain(deadline, out _);
        if (mark == (byte)'t')
            return WalkLiteral("true");
        if (mark == (byte)'f')
            return WalkLiteral("false");
        if (mark == (byte)'n')
            return WalkLiteral("null");
        if (mark == (byte)'-' || IsDigit(mark))
            return WalkNumber();
        return false;
    }

    // Plain ASCII nested value. A fancy byte, a repeated key, or a bad number returns false so the general walk can take it.
    private bool SkipFast(long deadline, byte open)
    {
        if (!Avx2.IsSupported)
            return false;
        const byte stKey = 0;
        const byte stColon = 1;
        const byte stValue = 2;
        const byte stComma = 3;
        const byte stArr = 4;
        const byte stArrComma = 5;
        Span<Frame> frames = stackalloc Frame[32];
        Span<(long At, int Len)> keys = stackalloc (long, int)[256];
        Span<ulong> fps = stackalloc ulong[256];
        frames[0] = new Frame(open == (byte)'{', 0);
        var depth = 0;
        var keyTop = 0;
        var checks = 0;
        _pos++;
        _column++;
        if (!Window(out var span, out _))
            return false;
        var origin = _pos;
        var i = 0;
        var quoteV = Vector256.Create((byte)'"');
        var slashV = Vector256.Create((byte)'\\');
        var limitV = Vector256.Create((sbyte)32);
        var spV = Vector256.Create((byte)' ');
        var tabV = Vector256.Create((byte)'\t');
        var crV = Vector256.Create((byte)'\r');
        var lfV = Vector256.Create((byte)'\n');
        while (true)
        {
            if ((++checks & 255) == 0 && TimedOut(deadline))
                return false;
            if (i >= span.Length)
            {
                _pos = origin + i;
                if (!Window(out span, out _))
                    return false;
                origin = _pos;
                i = 0;
                continue;
            }

            if (i + 32 <= span.Length)
            {
                var v = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(span), (nuint)i);
                var crM = Avx2.CompareEqual(v, crV);
                var lfM = Avx2.CompareEqual(v, lfV);
                var ws = Avx2.MoveMask(Avx2.Or(
                    Avx2.Or(Avx2.CompareEqual(v, spV), Avx2.CompareEqual(v, tabV)),
                    Avx2.Or(crM, lfM)));
                var take = ws == -1 ? 32 : BitOperations.TrailingZeroCount((uint)~ws);
                if (take > 0)
                {
                    var crBits = (uint)Avx2.MoveMask(crM);
                    var lfBits = (uint)Avx2.MoveMask(lfM);
                    if (take < 32)
                    {
                        var bits = (1u << take) - 1u;
                        crBits &= bits;
                        lfBits &= bits;
                    }

                    if (crBits != 0 || lfBits != 0 || _cr)
                        NoteMask(crBits, lfBits, take);
                    else
                        _column += take;
                    i += take;
                }

                if (take == 32)
                    continue;
            }
            else
            {
                var next = PastSpace(span, i);
                if (next >= span.Length)
                {
                    if (next > i)
                        NoteBreaksSlow(span[i..]);
                    i = span.Length;
                    continue;
                }

                if (next > i)
                    NoteBreaksSlow(span[i..next]);
                i = next;
            }

            var mark = span[i];
            var state = frames[depth].State;
            if (mark == (byte)'"')
            {
                if (state is not (stKey or stValue or stArr))
                    return false;
                var content = origin + i + 1;
                i++;
                var closed = false;
                var bad = false;
                while (!closed && !bad)
                {
                    if (i >= span.Length)
                    {
                        _pos = origin + i;
                        if (!Window(out span, out _))
                            return false;
                        origin = _pos;
                        i = 0;
                        continue;
                    }

                    if (i + 32 <= span.Length)
                    {
                        var v = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(span), (nuint)i);
                        var q = Avx2.MoveMask(Avx2.CompareEqual(v, quoteV));
                        var low = Avx2.CompareGreaterThan(limitV, v.AsSByte()).AsByte();
                        var stop = Avx2.MoveMask(Avx2.Or(low, Avx2.CompareEqual(v, slashV)));
                        var hit = q | stop;
                        if (hit == 0)
                        {
                            i += 32;
                            continue;
                        }

                        var bit = BitOperations.TrailingZeroCount((uint)hit);
                        i += bit;
                        if (((stop >> bit) & 1) != 0)
                            bad = true;
                        else
                            closed = true;
                        continue;
                    }

                    var b = span[i];
                    if (b == (byte)'"')
                    {
                        closed = true;
                        continue;
                    }

                    if (b < 32 || b >= 128 || b == (byte)'\\')
                        bad = true;
                    else
                        i++;
                }

                if (bad)
                    return false;
                var length = (int)(origin + i - content);
                i++;
                _column += length + 2;
                if (state == stKey)
                {
                    if (length > 128 || frames[depth].Keys == 24 || keyTop == keys.Length)
                        return false;
                    ulong fp = 14695981039346656037UL;
                    var keyAt = (int)(content - origin);
                    if (keyAt >= 0 && keyAt + length <= span.Length)
                    {
                        var text = span.Slice(keyAt, length);
                        for (var n = 0; n < text.Length; n++)
                            fp = (fp ^ text[n]) * 1099511628211UL;
                    }

                    var keyOrigin = frames[depth].KeyOrigin;
                    for (var k = 0; k < frames[depth].Keys; k++)
                    {
                        var prior = keys[keyOrigin + k];
                        if (fps[keyOrigin + k] == fp && prior.Len == length && SameBytes(prior.At, content, length))
                            return false;
                    }

                    fps[keyTop] = fp;
                    keys[keyTop++] = (content, length);
                    frames[depth].Keys++;
                    frames[depth].State = stColon;
                }
                else
                {
                    frames[depth].Any = true;
                    frames[depth].State = state == stArr ? stArrComma : stComma;
                }

                continue;
            }

            if (mark == (byte)'}')
            {
                if (!frames[depth].Object || (frames[depth].State != stComma && !(frames[depth].State == stKey && frames[depth].Keys == 0)))
                    return false;
                i++;
                _column++;
                keyTop = frames[depth].KeyOrigin;
                if (depth == 0)
                {
                    _pos = origin + i;
                    return true;
                }

                depth--;
                frames[depth].Any = true;
                frames[depth].State = frames[depth].Object ? stComma : stArrComma;
                continue;
            }

            if (mark == (byte)']')
            {
                if (frames[depth].Object || (frames[depth].State != stArrComma && !(frames[depth].State == stArr && !frames[depth].Any)))
                    return false;
                i++;
                _column++;
                if (depth == 0)
                {
                    _pos = origin + i;
                    return true;
                }

                depth--;
                frames[depth].Any = true;
                frames[depth].State = frames[depth].Object ? stComma : stArrComma;
                continue;
            }

            if (mark == (byte)':')
            {
                if (state != stColon)
                    return false;
                i++;
                _column++;
                frames[depth].State = stValue;
                continue;
            }

            if (mark == (byte)',')
            {
                if (state == stComma)
                    frames[depth].State = stKey;
                else if (state == stArrComma)
                    frames[depth].State = stArr;
                else
                    return false;
                i++;
                _column++;
                continue;
            }

            if (mark is (byte)'{' or (byte)'[')
            {
                if (state is not (stValue or stArr) || depth + 1 == frames.Length || keyTop + 24 > keys.Length)
                    return false;
                i++;
                _column++;
                depth++;
                frames[depth] = new Frame(mark == (byte)'{', keyTop);
                continue;
            }

            if (state is not (stValue or stArr))
                return false;
            var atEnd = origin + span.Length >= _source.Length;
            if (!atEnd && span.Length - i < 24)
            {
                _pos = origin + i;
                if (!TryByte(out mark, out _) || !SkipAtom(deadline, mark))
                    return false;
                if (!Window(out span, out _))
                    return false;
                origin = _pos;
                i = 0;
            }
            else
            {
                var atom = i;
                if (!EatAtom(span, ref i, atEnd))
                    return false;
                _column += i - atom;
                frames[depth].Any = true;
                frames[depth].State = state == stArr ? stArrComma : stComma;
            }

            if (span.Length == 0)
                return false;
            if (i == 0 && origin == _pos && frames[depth].State == state)
            {
                frames[depth].Any = true;
                frames[depth].State = state == stArr ? stArrComma : stComma;
            }
        }
    }

    // Nested bytes are not columns. A repeated key, a fancy value, or a cut-off window defers the whole item.
    private bool SkipContainer(long deadline, byte open)
    {
        const byte stKey = 0;
        const byte stColon = 1;
        const byte stValue = 2;
        const byte stComma = 3;
        const byte stArr = 4;
        const byte stArrComma = 5;
        Span<Frame> frames = stackalloc Frame[32];
        Span<(long At, int Len)> keys = stackalloc (long, int)[256];
        frames[0] = new Frame(open == (byte)'{', 0);
        var depth = 0;
        var keyTop = 0;
        var checks = 0;
        Consume(open);
        if (!Window(out var span, out _))
            return false;
        var i = 0;
        var charged = 0;
        var origin = _pos;
        while (true)
        {
            if ((++checks & 127) == 0 && TimedOut(deadline))
                return false;
            if (i >= span.Length)
            {
                Charge(span[charged..]);
                _pos = origin + span.Length;
                if (!Window(out span, out _))
                    return false;
                origin = _pos;
                i = 0;
                charged = 0;
                continue;
            }

            i = PastSpace(span, i);
            if (i >= span.Length)
                continue;

            var mark = span[i];
            var state = frames[depth].State;
            if (mark == (byte)'"')
            {
                if (state is not (stKey or stValue or stArr))
                    return false;
                var close = PlainClose(span, i);
                long content;
                int length;
                if (close == -1)
                {
                    Charge(span[charged..i]);
                    _pos = origin + i;
                    content = _pos + 1;
                    if (!SkipPlain(deadline, out length))
                        return false;
                    if (!NoteString(frames, keys, ref depth, ref keyTop, state, content, length))
                        return false;
                    if (!Window(out span, out _))
                        return false;
                    origin = _pos;
                    i = 0;
                    charged = 0;
                    continue;
                }

                if (close < 0)
                    return false;
                content = origin + i + 1;
                length = close - i - 1;
                i = close + 1;
                if (!NoteString(frames, keys, ref depth, ref keyTop, state, content, length))
                    return false;
                continue;
            }

            if (mark == (byte)'}')
            {
                if (!frames[depth].Object)
                    return false;
                var empty = state == stKey && frames[depth].Keys == 0;
                if (!empty && state != stComma)
                    return false;
                i++;
                keyTop = frames[depth].KeyOrigin;
                if (depth == 0)
                {
                    Charge(span[charged..i]);
                    _pos = origin + i;
                    return true;
                }

                depth--;
                frames[depth].Any = true;
                frames[depth].State = frames[depth].Object ? stComma : stArrComma;
                continue;
            }

            if (mark == (byte)']')
            {
                if (frames[depth].Object)
                    return false;
                var empty = state == stArr && !frames[depth].Any;
                if (!empty && state != stArrComma)
                    return false;
                i++;
                if (depth == 0)
                {
                    Charge(span[charged..i]);
                    _pos = origin + i;
                    return true;
                }

                depth--;
                frames[depth].Any = true;
                frames[depth].State = frames[depth].Object ? stComma : stArrComma;
                continue;
            }

            if (mark == (byte)':')
            {
                if (state != stColon)
                    return false;
                i++;
                frames[depth].State = stValue;
                continue;
            }

            if (mark == (byte)',')
            {
                if (state == stComma)
                    frames[depth].State = stKey;
                else if (state == stArrComma)
                    frames[depth].State = stArr;
                else
                    return false;
                i++;
                continue;
            }

            if (mark is (byte)'{' or (byte)'[')
            {
                if (state is not (stValue or stArr))
                    return false;
                if (depth + 1 == frames.Length || keyTop + 24 > keys.Length)
                    return false;
                i++;
                depth++;
                frames[depth] = new Frame(mark == (byte)'{', keyTop);
                continue;
            }

            if (state is not (stValue or stArr))
                return false;
            var atEnd = origin + span.Length >= _source.Length;
            if (!atEnd && span.Length - i < 24)
            {
                Charge(span[charged..i]);
                _pos = origin + i;
                if (!TryByte(out mark, out _) || !SkipAtom(deadline, mark))
                    return false;
                if (!Window(out span, out _))
                    return false;
                origin = _pos;
                i = 0;
                charged = 0;
            }
            else if (!EatAtom(span, ref i, atEnd))
            {
                return false;
            }

            frames[depth].Any = true;
            frames[depth].State = state == stArr ? stArrComma : stComma;
        }
    }

    private bool NoteString(
        Span<Frame> frames,
        Span<(long At, int Len)> keys,
        ref int depth,
        ref int keyTop,
        byte state,
        long content,
        int length)
    {
        const byte stKey = 0;
        const byte stColon = 1;
        const byte stArr = 4;
        const byte stComma = 3;
        const byte stArrComma = 5;
        if (state == stKey)
        {
            if (length > 128 || frames[depth].Keys == 24 || keyTop == keys.Length)
                return false;
            var keyOrigin = frames[depth].KeyOrigin;
            var priorCount = frames[depth].Keys;
            for (var k = 0; k < priorCount; k++)
            {
                var prior = keys[keyOrigin + k];
                if (prior.Len == length && SameBytes(prior.At, content, length))
                    return false;
            }

            keys[keyTop++] = (content, length);
            frames[depth].Keys++;
            frames[depth].State = stColon;
            return true;
        }

        frames[depth].Any = true;
        frames[depth].State = state == stArr ? stArrComma : stComma;
        return true;
    }

    private bool EatAtom(ReadOnlySpan<byte> span, ref int i, bool atEnd)
    {
        var mark = span[i];
        if (mark == (byte)'t')
            return EatWord(span, ref i, "true"u8, atEnd);
        if (mark == (byte)'f')
            return EatWord(span, ref i, "false"u8, atEnd);
        if (mark == (byte)'n')
            return EatWord(span, ref i, "null"u8, atEnd);
        if (mark == (byte)'-' || IsDigit(mark))
            return EatNumber(span, ref i, atEnd);
        return false;
    }

    private bool EatWord(ReadOnlySpan<byte> span, ref int i, ReadOnlySpan<byte> word, bool atEnd)
    {
        if (i + word.Length > span.Length)
            return false;
        if (!span.Slice(i, word.Length).SequenceEqual(word))
            return false;
        var after = i + word.Length;
        if (after == span.Length && !atEnd)
            return false;
        if (after < span.Length && !IsValueEnd(span[after]))
            return false;
        i = after;
        return true;
    }

    private bool EatNumber(ReadOnlySpan<byte> span, ref int i, bool atEnd)
    {
        var mark = span[i];
        if (mark == (byte)'-')
        {
            if (++i >= span.Length || !IsDigit(span[i]))
                return false;
            mark = span[i];
        }

        if (!IsDigit(mark))
            return false;
        if (mark == (byte)'0')
        {
            i++;
            if (i < span.Length && IsDigit(span[i]))
                return false;
        }
        else
        {
            i++;
            while (i < span.Length && IsDigit(span[i]))
                i++;
        }

        if (i < span.Length && span[i] == (byte)'.')
        {
            i++;
            var digits = 0;
            while (i < span.Length && IsDigit(span[i]))
            {
                i++;
                digits++;
            }

            if (digits == 0)
                return false;
        }

        if (i < span.Length && span[i] is (byte)'e' or (byte)'E')
        {
            i++;
            if (i < span.Length && span[i] is (byte)'+' or (byte)'-')
                i++;
            var digits = 0;
            while (i < span.Length && IsDigit(span[i]))
            {
                i++;
                digits++;
            }

            if (digits == 0)
                return false;
        }

        if (i == span.Length && !atEnd)
            return false;
        if (i < span.Length && !IsValueEnd(span[i]))
            return false;
        return true;
    }

    // Spaces and a single line feed stay here. A carriage return uses the general counter.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int SkipGap(ReadOnlySpan<byte> span, int i)
    {
        var n = span.Length;
        while ((uint)i < (uint)n)
        {
            var b = span[i];
            if (b == (byte)' ' || b == (byte)'\t')
            {
                if (_cr)
                    _cr = false;
                var start = i;
                i++;
                while (i < n && (span[i] == (byte)' ' || span[i] == (byte)'\t'))
                    i++;
                _column += i - start;
                continue;
            }

            if (b == (byte)'\n' && !_cr)
            {
                i++;
                var column = 1;
                while (i < n && (span[i] == (byte)' ' || span[i] == (byte)'\t'))
                {
                    i++;
                    column++;
                }

                _line++;
                _column = column;
                continue;
            }

            if (b != (byte)'\r' && b != (byte)'\n' && !_cr)
                return i;
            var next = PastSpace(span, i);
            if (next > i)
                NoteBreaksSlow(span[i..Math.Min(next, n)]);
            return next;
        }

        return i;
    }

    private static int PastSpace(ReadOnlySpan<byte> span, int i)
    {
        var next = span[i..].IndexOfAnyExcept(SpaceBytes);
        return next < 0 ? span.Length : i + next;
    }

    // Index of the closing quote, -1 when the window ends first, -2 when the text is not plain.
    private static int PlainClose(ReadOnlySpan<byte> span, int open)
    {
        var stop = span[(open + 1)..].IndexOfAny(StringStops);
        if (stop < 0)
            return -1;
        return span[open + 1 + stop] == (byte)'"' ? open + 1 + stop : -2;
    }

    private static bool IsValueEnd(byte mark) =>
        mark is (byte)' ' or (byte)'\t' or (byte)'\n' or (byte)'\r' or (byte)',' or (byte)'}' or (byte)']';

    // Bits of crBits and lfBits cover the first `take` bytes. A CR LF pair is one line.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void NoteMask(uint crBits, uint lfBits, int take)
    {
        var skipped = 0;
        if (_cr)
        {
            _cr = false;
            if ((lfBits & 1u) != 0)
            {
                lfBits &= ~1u;
                skipped = 1;
            }
        }

        var paired = (crBits << 1) & lfBits;
        var breaks = crBits | lfBits;
        if (breaks == 0)
        {
            _column += take - skipped;
            return;
        }

        _line += BitOperations.PopCount(crBits) + BitOperations.PopCount(lfBits & ~paired);
        var last = BitOperations.Log2(breaks);
        _column = take - last;
        _cr = (crBits & (1u << last)) != 0;
    }

    // One window at a time. Plain text has no raw break, so a count of the line feeds is the line change.
    private void Charge(ReadOnlySpan<byte> run)
    {
        if (run.Length == 0 || _holdLine)
            return;
        var start = 0;
        if (_cr)
        {
            _cr = false;
            if (run[0] == (byte)'\n')
                start = 1;
        }

        var body = run[start..];
        if (body.Length == 0)
            return;
        if (body[^1] == (byte)'\r')
        {
            Charge(body[..^1]);
            _line++;
            _column = 1;
            _cr = true;
            return;
        }

        var newlines = body.Count((byte)'\n');
        if (!_breaksTrusted)
        {
            var returns = body.Count((byte)'\r');
            if (returns != newlines || (returns > 0 && !Pairs(body)))
            {
                NoteBreaksSlow(body);
                return;
            }

            if (returns > 0)
                _breaksTrusted = true;
        }

        if (newlines == 0)
        {
            _column += body.Length;
            return;
        }

        _line += newlines;
        _column = body.Length - body.LastIndexOf((byte)'\n');
    }

    private static bool Pairs(ReadOnlySpan<byte> body)
    {
        for (var i = 0; i < body.Length; i++)
        {
            if (body[i] != (byte)'\r')
                continue;
            if (i + 1 >= body.Length || body[i + 1] != (byte)'\n')
                return false;
            i++;
        }

        return true;
    }

    private void NoteBreaksSlow(ReadOnlySpan<byte> run)
    {
        var index = 0;
        if (_cr && run.Length > 0)
        {
            _cr = false;
            if (run[0] == (byte)'\n')
                index = 1;
        }

        var line = _line;
        var column = _column;
        var cr = false;
        for (; index < run.Length; index++)
        {
            var value = run[index];
            if (cr)
            {
                cr = false;
                if (value == (byte)'\n')
                    continue;
            }

            if (value == (byte)'\r')
            {
                line++;
                column = 1;
                cr = true;
                continue;
            }

            if (value == (byte)'\n')
            {
                line++;
                column = 1;
                continue;
            }

            column++;
        }

        _line = line;
        _column = column;
        _cr = cr;
    }

    private struct Frame(bool objectMode, int keyOrigin)
    {
        public byte State = objectMode ? (byte)0 : (byte)4;
        public bool Object = objectMode;
        public int Keys;
        public int KeyOrigin = keyOrigin;
        public bool Any;
    }

    private bool SameBytes(long left, long right, int length)
    {
        if (length <= 0)
            return true;
        if (length > 128)
            return false;
        if (left >= _windowAt && right >= _windowAt
            && left + length <= _windowAt + _windowCount
            && right + length <= _windowAt + _windowCount)
        {
            var span = _window.AsSpan();
            return span.Slice((int)(left - _windowAt), length)
                .SequenceEqual(span.Slice((int)(right - _windowAt), length));
        }

        Span<byte> a = stackalloc byte[128];
        Span<byte> b = stackalloc byte[128];
        // A short read cannot prove the keys differ, so the item takes the event parser.
        if (ReadSource(left, a[..length]) != length || ReadSource(right, b[..length]) != length)
            return true;
        return a[..length].SequenceEqual(b[..length]);
    }

    private static bool TimedOut(long deadline) => deadline != 0 && Stopwatch.GetTimestamp() >= deadline;

    private void PushTrail(string name)
    {
        if (_trailCount == _trail.Length)
            Array.Resize(ref _trail, _trail.Length * 2);
        _trail[_trailCount++] = new DocumentPath.Step(name, null);
    }

    private void PopTrail() => _trailCount--;

    private void IndexTrail(int oneBased)
    {
        var step = _trail[_trailCount - 1];
        _trail[_trailCount - 1] = new DocumentPath.Step(step.Name, oneBased);
    }

    private void ConsiderName(string name)
    {
        if (_sink is null || _needleText.Length == 0 || name.Length < _needleText.Length)
            return;
        var from = 0;
        while (from <= name.Length - _needleText.Length)
        {
            var found = name.IndexOf(_needleText, from, StringComparison.Ordinal);
            if (found < 0)
                return;
            _sink.NameHit(_trail.AsSpan(0, _trailCount), found, _needleText.Length, name);
            from = found + 1;
        }
    }

    private void SearchRaw(long start, long end)
    {
        var needle = _needleBytes;
        if (_sink is null || needle.Length == 0)
            return;
        var length = (int)(end - start);
        if (length < needle.Length)
            return;
        Span<byte> stack = stackalloc byte[256];
        byte[]? rented = null;
        var buf = length <= stack.Length ? stack[..length] : default;
        if (length > stack.Length)
        {
            rented = ArrayPool<byte>.Shared.Rent(length);
            buf = rented.AsSpan(0, length);
        }

        try
        {
            if (ReadSource(start, buf) != length)
                return;
            var from = 0;
            while (from <= buf.Length - needle.Length)
            {
                var found = buf[from..].IndexOf(needle);
                if (found < 0)
                    return;
                _sink.ValueHit(_trail.AsSpan(0, _trailCount), from + found, needle.Length);
                from += found + 1;
            }
        }
        finally
        {
            if (rented is not null)
                ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private void DropShape() => _haveShape = false;
}
