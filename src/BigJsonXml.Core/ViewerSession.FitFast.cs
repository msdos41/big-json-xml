using System.Buffers;
using BigJsonXml.Xml;

namespace BigJsonXml;

public sealed partial class ViewerSession
{
    private readonly byte[] _fitWindow = new byte[16 * 1024];

    // A scalar column is measured from the entry bytes. An entity, a nested value,
    // or a non-ASCII character falls back to the frame reader for that entry.
    private bool TryMeasureFast(FitRun run, long offset, FieldKey key)
    {
        if (key.Ns.Length != 0)
            return false;
        if (_grammar == Grammar.Json)
            return MeasureJson(run, offset, key);
        if (_grammar == Grammar.Xml)
            return MeasureXml(run, offset, key);
        return false;
    }

    private bool MeasureJson(FitRun run, long offset, FieldKey key)
    {
        if (key.Kind != NameKind.Element)
            return false;
        var scan = new ByteScan(_document.Source, _fitWindow, offset);
        if (!scan.SkipSpace() || !scan.Try(out var kind))
            return false;
        int chars;
        bool read;
        if (kind == (byte)'{')
            read = scan.MeasureObject(key.Local, out chars);
        else if (key.Local == "#value")
            read = scan.MeasureScalar(out chars);
        else
            return false;
        if (!read)
            return false;
        return ApplyFast(run, chars);
    }

    private bool MeasureXml(FitRun run, long offset, FieldKey key)
    {
        var scan = new ByteScan(_document.Source, _fitWindow, offset);
        if (!scan.SkipSpace() || !scan.Try(out var mark) || mark != (byte)'<')
            return false;
        bool read;
        int chars;
        if (key.Kind == NameKind.Attribute)
            read = scan.MeasureAttribute(key.Local, out chars);
        else if (key.Kind == NameKind.Text)
            read = scan.MeasureEntryText(out chars);
        else if (key.Kind == NameKind.Element)
            read = scan.MeasureChild(key.Local, out chars);
        else
            return false;
        if (!read)
            return false;
        return ApplyFast(run, chars);
    }

    private bool ApplyFast(FitRun run, int chars)
    {
        if (chars >= 0)
            run.Width = Math.Max(run.Width, CharsWidth(chars, gutter: false));
        return true;
    }

    private ref struct ByteScan(IByteSource source, byte[] window, long position)
    {
        private static readonly SearchValues<byte> QuoteOrSlash = SearchValues.Create("\"\\"u8);
        private static readonly SearchValues<byte> JsonNest = SearchValues.Create("{}[]\""u8);

        private readonly IByteSource _source = source;
        private readonly byte[] _window = window;
        private long _pos = position;
        private long _at = -1;
        private int _count;

        public bool MeasureObject(string local, out int chars)
        {
            chars = -1;
            if (!Take((byte)'{'))
                return false;
            var first = true;
            while (true)
            {
                if (!SkipSpace() || !Try(out var mark))
                    return false;
                if (mark == (byte)'}')
                    return true;
                if (!first)
                {
                    if (mark != (byte)',')
                        return false;
                    _pos++;
                    if (!SkipSpace() || !Try(out mark))
                        return false;
                }

                first = false;
                if (mark != (byte)'"')
                    return false;
                if (!PlainKey(local, out var matches))
                    return false;
                if (!SkipSpace() || !Try(out mark) || mark != (byte)':')
                    return false;
                _pos++;
                if (!SkipSpace())
                    return false;
                if (!matches)
                {
                    if (!SkipJsonValue())
                        return false;
                    continue;
                }

                if (!Try(out mark) || mark is (byte)'{' or (byte)'[')
                    return false;
                return MeasureScalar(out chars);
            }
        }

        public bool MeasureScalar(out int chars)
        {
            chars = -1;
            if (!Try(out var mark))
                return false;
            var start = _pos;
            if (mark == (byte)'"')
                return PlainString(out chars);
            if (mark == (byte)'-' || IsDigit(mark))
            {
                if (!SkipNumber())
                    return false;
            }
            else if (mark == (byte)'t')
            {
                if (!SkipWord("true"u8))
                    return false;
            }
            else if (mark == (byte)'f')
            {
                if (!SkipWord("false"u8))
                    return false;
            }
            else if (mark == (byte)'n')
            {
                if (!SkipWord("null"u8))
                    return false;
            }
            else
            {
                return false;
            }

            var raw = _pos - start;
            if (raw > int.MaxValue)
                return false;
            chars = (int)raw;
            return true;
        }

        public bool MeasureAttribute(string local, out int chars)
        {
            chars = -1;
            if (!Take((byte)'<') || !SkipXmlName() || !SkipSpace())
                return false;
            var seen = false;
            while (true)
            {
                if (!SkipSpace() || !Try(out var mark))
                    return false;
                if (mark is (byte)'>' or (byte)'/')
                    return true;
                if (!XmlName(out var nameAt, out var nameLen))
                    return false;
                if (nameLen == 5 && Eq(nameAt, "xmlns"u8) || Contains(nameAt, nameLen, (byte)':'))
                    return false;
                var match = nameLen == local.Length && Eq(nameAt, local);
                if (!SkipSpace() || !Try(out mark))
                    return false;
                if (mark != (byte)'=')
                {
                    if (match && seen)
                        return false;
                    if (match)
                    {
                        seen = true;
                        chars = 0;
                    }

                    continue;
                }

                _pos++;
                if (!SkipSpace() || !Try(out mark) || mark is not ((byte)'"' or (byte)'\''))
                    return false;
                if (!Quoted(out var quoted, out var plain))
                    return false;
                if (!match)
                    continue;
                if (seen || !plain)
                    return false;
                seen = true;
                chars = quoted;
            }
        }

        public bool MeasureEntryText(out int chars)
        {
            chars = -1;
            if (!OpenTag(out var empty))
                return false;
            if (empty)
                return true;
            if (!PlainText(out chars, out var child))
                return false;
            return !child;
        }

        public bool MeasureChild(string local, out int chars)
        {
            chars = -1;
            if (!OpenTag(out var empty))
                return false;
            if (empty)
                return true;
            var seen = false;
            while (true)
            {
                if (!SkipSpace() || !Try(out var mark))
                    return false;
                if (mark != (byte)'<')
                {
                    if (!SkipUntil((byte)'<'))
                        return false;
                    continue;
                }

                if (!Look(1, out var next))
                    return false;
                if (next == (byte)'/')
                    return true;
                if (next is (byte)'!' or (byte)'?')
                    return false;
                if (!OpenChild(out var nameAt, out var nameLen, out var self))
                    return false;
                var match = nameLen == local.Length && Eq(nameAt, local);
                if (self)
                {
                    if (match)
                        return false;
                    continue;
                }

                if (!match)
                {
                    if (!SkipBody())
                        return false;
                    continue;
                }

                if (seen)
                    return false;
                if (!PlainText(out chars, out var nested) || nested || chars < 0 || !SkipEndTag())
                    return false;
                seen = true;
            }
        }

        private bool PlainKey(string local, out bool matches)
        {
            matches = false;
            if (!Take((byte)'"'))
                return false;
            var matched = true;
            var index = 0;
            while (true)
            {
                if (!Try(out var mark))
                    return false;
                if (mark == (byte)'"')
                {
                    _pos++;
                    matches = matched && index == local.Length;
                    return true;
                }

                if (mark is (byte)'\\' or < 0x20 or >= 0x80)
                    return false;
                if (index >= local.Length || mark != (byte)local[index])
                    matched = false;
                index++;
                _pos++;
                if (index > 8192)
                    return false;
            }
        }

        private bool PlainString(out int chars)
        {
            chars = -1;
            var start = _pos;
            if (!Take((byte)'"'))
                return false;
            while (true)
            {
                if (!Fill(out var span))
                    return false;
                var stop = span.IndexOfAny(QuoteOrSlash);
                var run = stop < 0 ? span.Length : stop;
                if (run > 0 && span[..run].IndexOfAnyInRange((byte)0x80, (byte)0xFF) >= 0)
                    return false;
                if (run > 0 && span[..run].IndexOfAnyInRange((byte)0x00, (byte)0x1F) >= 0)
                    return false;
                if (stop < 0)
                {
                    _pos += span.Length;
                    continue;
                }

                if (span[stop] != (byte)'"')
                    return false;
                _pos += stop + 1;
                var raw = _pos - start;
                if (raw > int.MaxValue)
                    return false;
                chars = (int)raw;
                return true;
            }
        }

        private bool SkipJsonValue()
        {
            if (!Try(out var mark))
                return false;
            if (mark == (byte)'"')
                return Take((byte)'"') && SkipStringBody();
            if (mark == (byte)'{')
                return SkipNest();
            if (mark == (byte)'[')
                return SkipNest();
            if (mark == (byte)'-' || IsDigit(mark))
                return SkipNumber();
            if (mark == (byte)'t')
                return SkipWord("true"u8);
            if (mark == (byte)'f')
                return SkipWord("false"u8);
            if (mark == (byte)'n')
                return SkipWord("null"u8);
            return false;
        }

        private bool SkipNest()
        {
            _pos++;
            var depth = 1;
            while (depth > 0)
            {
                if (!Fill(out var span))
                    return false;
                var stop = span.IndexOfAny(JsonNest);
                if (stop < 0)
                {
                    _pos += span.Length;
                    continue;
                }

                var mark = span[stop];
                _pos += stop + 1;
                if (mark == (byte)'"')
                {
                    if (!SkipStringBody())
                        return false;
                    continue;
                }

                if (mark is (byte)'{' or (byte)'[')
                    depth++;
                else
                    depth--;
            }

            return true;
        }

        private bool SkipStringBody()
        {
            while (true)
            {
                if (!Fill(out var span))
                    return false;
                var stop = span.IndexOfAny(QuoteOrSlash);
                if (stop < 0)
                {
                    _pos += span.Length;
                    continue;
                }

                var mark = span[stop];
                _pos += stop + 1;
                if (mark == (byte)'"')
                    return true;
                if (mark < 0x20)
                    return false;
                if (!Try(out var escaped))
                    return false;
                _pos++;
                if (escaped != (byte)'u')
                    continue;
                for (var i = 0; i < 4; i++)
                {
                    if (!Try(out var hex) || Hex(hex) < 0)
                        return false;
                    _pos++;
                }
            }
        }

        private bool SkipNumber()
        {
            if (!Try(out var first))
                return false;
            if (first == (byte)'-')
            {
                _pos++;
                if (!Try(out first) || !IsDigit(first))
                    return false;
            }

            if (!IsDigit(first))
                return false;
            _pos++;
            if (first != (byte)'0')
                SkipDigits();
            else if (Try(out var after) && IsDigit(after))
                return false;

            if (Try(out var mark) && mark == (byte)'.')
            {
                _pos++;
                var before = _pos;
                SkipDigits();
                if (_pos == before)
                    return false;
            }

            if (!Try(out mark) || mark is not ((byte)'e' or (byte)'E'))
                return true;
            _pos++;
            if (!Try(out var exp))
                return false;
            if (exp is (byte)'+' or (byte)'-')
            {
                _pos++;
                if (!Try(out exp))
                    return false;
            }

            if (!IsDigit(exp))
                return false;
            _pos++;
            SkipDigits();
            return true;
        }

        private void SkipDigits()
        {
            while (Try(out var mark) && IsDigit(mark))
                _pos++;
        }

        private bool SkipWord(ReadOnlySpan<byte> word)
        {
            foreach (var expected in word)
            {
                if (!Try(out var mark) || mark != expected)
                    return false;
                _pos++;
            }

            return true;
        }

        private bool OpenTag(out bool empty)
        {
            empty = false;
            return Take((byte)'<') && SkipXmlName() && FinishTag(out empty);
        }

        private bool OpenChild(out long nameAt, out int nameLen, out bool empty)
        {
            nameAt = 0;
            nameLen = 0;
            empty = false;
            if (!Take((byte)'<') || !XmlName(out nameAt, out nameLen) || Contains(nameAt, nameLen, (byte)':'))
                return false;
            return FinishTag(out empty);
        }

        private bool FinishTag(out bool empty)
        {
            empty = false;
            while (true)
            {
                if (!SkipSpace() || !Try(out var mark))
                    return false;
                if (mark == (byte)'>')
                {
                    _pos++;
                    return true;
                }

                if (mark == (byte)'/' && Look(1, out var next) && next == (byte)'>')
                {
                    _pos += 2;
                    empty = true;
                    return true;
                }

                if (mark is (byte)'"' or (byte)'\'')
                {
                    if (!SkipQuoted())
                        return false;
                    continue;
                }

                if (mark == (byte)'&')
                    return false;
                _pos++;
            }
        }

        private bool PlainText(out int chars, out bool child)
        {
            chars = -1;
            child = false;
            var count = 0;
            var any = false;
            while (true)
            {
                if (!Fill(out var span))
                    return false;
                var rel = span.IndexOf((byte)'<');
                var run = rel < 0 ? span : span[..rel];
                for (var i = 0; i < run.Length; i++)
                {
                    var mark = run[i];
                    if (mark is (byte)'&' or >= 0x80 or (byte)'\r' or (byte)'\n' or (byte)'\t')
                        return false;
                    if (mark != (byte)' ')
                        any = true;
                    count++;
                }

                _pos += run.Length;
                if (rel < 0)
                    continue;
                if (!Look(1, out var next))
                    return false;
                if (next != (byte)'/')
                {
                    child = true;
                    return true;
                }

                if (any)
                    chars = count;
                return true;
            }
        }

        private bool SkipUntil(byte value)
        {
            while (true)
            {
                if (!Fill(out var span))
                    return false;
                var rel = span.IndexOf(value);
                if (rel < 0)
                {
                    _pos += span.Length;
                    continue;
                }

                _pos += rel;
                return true;
            }
        }

        private bool SkipBody()
        {
            var depth = 1;
            while (depth > 0)
            {
                if (!SkipUntil((byte)'<') || !Look(1, out var next))
                    return false;
                if (next == (byte)'/')
                {
                    if (!SkipEndTag())
                        return false;
                    depth--;
                    continue;
                }

                if (next is (byte)'!' or (byte)'?')
                    return false;
                if (!Take((byte)'<') || !SkipXmlName() || !FinishTag(out var empty))
                    return false;
                if (!empty)
                    depth++;
            }

            return true;
        }

        private bool SkipEndTag()
        {
            if (!Take((byte)'<') || !Take((byte)'/'))
                return false;
            while (Try(out var mark) && mark != (byte)'>')
            {
                if (mark == (byte)'<')
                    return false;
                _pos++;
            }

            return Take((byte)'>');
        }

        private bool Quoted(out int chars, out bool plain)
        {
            chars = 0;
            plain = true;
            if (!Try(out var quote))
                return false;
            _pos++;
            var count = 0;
            while (Try(out var mark))
            {
                if (mark == quote)
                {
                    _pos++;
                    chars = count;
                    return true;
                }

                if (mark is (byte)'<' or (byte)'&' or >= 0x80 or (byte)'\r' or (byte)'\n' or (byte)'\t')
                    plain = false;
                count++;
                _pos++;
            }

            return false;
        }

        private bool SkipQuoted()
        {
            if (!Try(out var quote) || quote is not ((byte)'"' or (byte)'\''))
                return false;
            _pos++;
            while (Try(out var mark))
            {
                _pos++;
                if (mark == quote)
                    return true;
                if (mark == (byte)'<')
                    return false;
            }

            return false;
        }

        private bool SkipXmlName() => XmlName(out _, out _);

        private bool XmlName(out long nameAt, out int nameLen)
        {
            nameAt = _pos;
            nameLen = 0;
            if (!Try(out var first) || !IsNameStart(first))
                return false;
            _pos++;
            nameLen = 1;
            while (Try(out var mark) && IsName(mark))
            {
                _pos++;
                nameLen++;
            }

            return true;
        }

        private bool Contains(long at, int length, byte value)
        {
            for (var i = 0; i < length; i++)
            {
                if (!At(at + i, out var mark))
                    return false;
                if (mark == value)
                    return true;
            }

            return false;
        }

        private bool Eq(long at, ReadOnlySpan<byte> text)
        {
            for (var i = 0; i < text.Length; i++)
            {
                if (!At(at + i, out var mark) || mark != text[i])
                    return false;
            }

            return true;
        }

        private bool Eq(long at, string text)
        {
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] > 127 || !At(at + i, out var mark) || mark != (byte)text[i])
                    return false;
            }

            return true;
        }

        public bool SkipSpace()
        {
            while (Try(out var mark) && mark is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
                _pos++;
            return true;
        }

        public bool Try(out byte value) => At(_pos, out value);

        public bool Take(byte expected)
        {
            if (!Try(out var mark) || mark != expected)
                return false;
            _pos++;
            return true;
        }

        private bool Look(int ahead, out byte value) => At(_pos + ahead, out value);

        private bool At(long pos, out byte value)
        {
            value = 0;
            if (pos < _at || pos >= _at + _count)
            {
                if ((ulong)pos >= (ulong)_source.Length)
                    return false;
                var got = _source.Read(pos, _window);
                if (got <= 0)
                    return false;
                _at = pos;
                _count = got;
            }

            value = _window[(int)(pos - _at)];
            return true;
        }

        private bool Fill(out ReadOnlySpan<byte> span)
        {
            if (!At(_pos, out _))
            {
                span = default;
                return false;
            }

            span = _window.AsSpan((int)(_pos - _at), (int)(_at + _count - _pos));
            return span.Length > 0;
        }

        private static bool IsDigit(byte value) => value is >= (byte)'0' and <= (byte)'9';

        private static bool IsNameStart(byte value) =>
            value is (>= (byte)'A' and <= (byte)'Z') or (>= (byte)'a' and <= (byte)'z') or (byte)'_' or (byte)':';

        private static bool IsName(byte value) => IsNameStart(value) || IsDigit(value) || value is (byte)'-' or (byte)'.';

        private static int Hex(byte value) => value switch
        {
            >= (byte)'0' and <= (byte)'9' => value - (byte)'0',
            >= (byte)'a' and <= (byte)'f' => value - (byte)'a' + 10,
            >= (byte)'A' and <= (byte)'F' => value - (byte)'A' + 10,
            _ => -1,
        };
    }
}
