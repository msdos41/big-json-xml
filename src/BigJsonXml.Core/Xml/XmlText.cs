using System.Buffers;
using System.Text;
using BigJsonXml.Json;

namespace BigJsonXml.Xml;

internal sealed class XmlDocument
{
    public XmlDocument(IByteSource source) => Source = source;

    public IByteSource Source { get; }

    public Encoding Encoding { get; set; } = Encoding.UTF8;

    public List<EntityDef> Entities { get; } = [];
}

internal sealed class EntityDef
{
    public string Name = "";
    public bool External;
    public string Replacement = "";
    public bool Markup;
}

internal readonly struct TextPiece
{
    public int EntityId { get; init; }

    public long Start { get; init; }

    public long End { get; init; }

    public bool CData { get; init; }

    public bool Attribute { get; init; }

    public bool Json { get; init; }

    public static TextPiece Bytes(long start, long end, bool attribute = false, bool cdata = false) =>
        new() { EntityId = -1, Start = start, End = end, Attribute = attribute, CData = cdata };

    public static TextPiece JsonLiteral(long start, long end) =>
        new() { EntityId = -1, Start = start, End = end, Json = true };

    public static TextPiece Chars(int entityId, long start, long end, bool attribute = false) =>
        new() { EntityId = entityId, Start = start, End = end, Attribute = attribute };
}

internal static class CharDecoders
{
    public static bool TryDecode(
        Encoding encoding,
        IByteSource source,
        long position,
        long limit,
        out char value,
        out int bytes,
        out bool needMore,
        out string? error)
    {
        value = default;
        bytes = 0;
        needMore = false;
        error = null;
        if (position >= limit)
            return false;

        var available = source.Length - position;
        if (available <= 0)
        {
            needMore = !source.IsComplete;
            if (source.IsComplete)
                error = "The document ends in the middle of a character.";
            return false;
        }

        Span<byte> buffer = stackalloc byte[4];
        var count = source.Read(position, buffer);
        if (encoding.CodePage == 65001)
            return DecodeUtf8(buffer[..count], source.IsComplete, available, out value, out bytes, out needMore, out error);
        if (encoding.CodePage is 1200 or 1201)
            return DecodeUtf16(encoding.CodePage == 1201, buffer[..count], source.IsComplete, available, out value, out bytes, out needMore, out error);

        Span<char> chars = stackalloc char[1];
        encoding.GetChars(buffer[..1], chars);
        value = chars[0];
        bytes = 1;
        return true;
    }

    private static bool DecodeUtf8(ReadOnlySpan<byte> data, bool complete, long available, out char value, out int bytes, out bool needMore, out string? error)
    {
        value = default;
        bytes = 0;
        needMore = false;
        error = null;
        var first = data[0];
        if (first < 0x80)
        {
            value = (char)first;
            bytes = 1;
            return true;
        }

        var needed = first switch
        {
            >= 0xC2 and <= 0xDF => 2,
            >= 0xE0 and <= 0xEF => 3,
            >= 0xF0 and <= 0xF4 => 4,
            _ => 0,
        };
        if (needed == 0)
        {
            error = "The document is not valid UTF-8.";
            return false;
        }

        if (available < needed)
        {
            if (!complete)
            {
                needMore = true;
                return false;
            }

            error = "The document ends in the middle of a character.";
            return false;
        }

        for (var i = 1; i < needed; i++)
        {
            if ((data[i] & 0xC0) != 0x80)
            {
                error = "The document is not valid UTF-8.";
                return false;
            }
        }

        int code = needed switch
        {
            2 => ((first & 0x1F) << 6) | (data[1] & 0x3F),
            3 => ((first & 0x0F) << 12) | ((data[1] & 0x3F) << 6) | (data[2] & 0x3F),
            _ => ((first & 0x07) << 18) | ((data[1] & 0x3F) << 12) | ((data[2] & 0x3F) << 6) | (data[3] & 0x3F),
        };
        if (code > 0x10FFFF || code is >= 0xD800 and <= 0xDFFF)
        {
            error = "The document contains a character XML does not allow.";
            return false;
        }

        if (code <= 0xFFFF)
        {
            value = (char)code;
            bytes = needed;
            return true;
        }

        // Supplementary characters are surfaced as a high surrogate; the low surrogate
        // is delivered by a follow-up call that consumes no more bytes.
        value = (char)(0xD800 + ((code - 0x10000) >> 10));
        bytes = needed;
        return true;
    }

    public static bool TryDecodeLowSurrogate(Encoding encoding, IByteSource source, long position, out char value)
    {
        value = default;
        if (encoding.CodePage != 65001)
            return false;
        Span<byte> buffer = stackalloc byte[4];
        var count = source.Read(position, buffer);
        if (count == 0 || buffer[0] < 0xF0 || buffer[0] > 0xF4)
            return false;
        var code = ((buffer[0] & 0x07) << 18) | ((buffer[1] & 0x3F) << 12) | ((buffer[2] & 0x3F) << 6) | (buffer[3] & 0x3F);
        if (code < 0x10000)
            return false;
        value = (char)(0xDC00 + ((code - 0x10000) & 0x3FF));
        return true;
    }

    private static bool DecodeUtf16(bool bigEndian, ReadOnlySpan<byte> data, bool complete, long available, out char value, out int bytes, out bool needMore, out string? error)
    {
        value = default;
        bytes = 0;
        needMore = false;
        error = null;
        if (available < 2)
        {
            if (!complete)
            {
                needMore = true;
                return false;
            }

            error = "The document ends in the middle of a character.";
            return false;
        }

        value = bigEndian
            ? (char)((data[0] << 8) | data[1])
            : (char)(data[0] | (data[1] << 8));
        bytes = 2;
        return true;
    }
}

internal sealed class ExpandedCursor
{
    private readonly XmlDocument _document;
    private readonly IReadOnlyList<TextPiece> _pieces;
    private int _piece;
    private long _pos;
    private bool _attribute;
    private bool _cdata;
    private bool _active;
    private char _pending;
    private bool _hasPending;
    private readonly Stack<Frame> _stack = new();
    private readonly Stack<string> _names = new();
    private JsonChars? _json;

    public ExpandedCursor(XmlDocument document, IReadOnlyList<TextPiece> pieces)
    {
        _document = document;
        _pieces = pieces;
    }

    public bool TryRead(out char value)
    {
        while (true)
        {
            if (_json is not null)
            {
                if (_json.TryRead(out value))
                    return true;
                _json = null;
            }

            if (_json is null && !_active && _stack.Count == 0 && !_hasPending
                && _piece < _pieces.Count && _pieces[_piece].Json)
            {
                var piece = _pieces[_piece];
                _piece++;
                _json = new JsonChars(_document.Source, piece.Start, piece.End);
                continue;
            }

            if (!TryRaw(out var raw))
            {
                value = default;
                return false;
            }

            if (raw == '\r')
            {
                if (NextIsAdjacent('\n') && !TryRaw(out _))
                    throw new InvalidOperationException("The document ends in the middle of a character.");

                value = _attribute ? ' ' : '\n';
                return true;
            }

            if (_attribute && raw is '\n' or '\t')
            {
                value = ' ';
                return true;
            }

            if (raw == '&' && !_cdata)
            {
                if (!TryExpandReference(out value))
                    return false;
                if (value == '\0')
                    continue;
                return true;
            }

            value = raw;
            return true;
        }
    }

    private bool NextIsAdjacent(char expected)
    {
        if (_hasPending)
            return false;
        if (_stack.Count > 0)
        {
            var frame = _stack.Peek();
            return frame.Position < frame.Text.Length && frame.Text[frame.Position] == expected;
        }

        if (!_active || _piece >= _pieces.Count)
            return false;
        var current = _pieces[_piece];
        if (_pos >= current.End)
            return false;
        if (current.EntityId >= 0)
            return _document.Entities[current.EntityId].Replacement[(int)_pos] == expected;
        if (!CharDecoders.TryDecode(_document.Encoding, _document.Source, _pos, current.End, out var value, out _, out _, out _))
            return false;
        return value == expected;
    }

    private void Unread(char value)
    {
        _pending = value;
        _hasPending = true;
    }

    private bool TryRaw(out char value)
    {
        if (_hasPending)
        {
            _hasPending = false;
            value = _pending;
            return true;
        }

        while (_stack.Count > 0)
        {
            var frame = _stack.Peek();
            if (frame.Position >= frame.Text.Length)
            {
                _stack.Pop();
                _names.Pop();
                continue;
            }

            value = frame.Text[frame.Position++];
            return true;
        }

        while (true)
        {
            if (!_active)
            {
                if (_piece >= _pieces.Count)
                {
                    value = default;
                    return false;
                }

                var piece = _pieces[_piece];
                _attribute = piece.Attribute;
                _cdata = piece.CData;
                _pos = piece.Start;
                _active = true;
            }

            var current = _pieces[_piece];
            if (_pos >= current.End)
            {
                _piece++;
                _active = false;
                continue;
            }

            if (current.EntityId >= 0)
            {
                value = _document.Entities[current.EntityId].Replacement[(int)_pos];
                _pos++;
                return true;
            }

            if (!CharDecoders.TryDecode(_document.Encoding, _document.Source, _pos, current.End, out value, out var bytes, out _, out var error))
            {
                if (error is not null)
                    throw new InvalidOperationException(error);
                value = default;
                return false;
            }

            var start = _pos;
            _pos += bytes;
            if (char.IsHighSurrogate(value) && CharDecoders.TryDecodeLowSurrogate(_document.Encoding, _document.Source, start, out var low))
            {
                _pending = low;
                _hasPending = true;
            }

            return true;
        }
    }

    private bool TryExpandReference(out char value)
    {
        value = '\0';
        if (!TryRaw(out var mark))
            throw new InvalidOperationException("An entity reference is incomplete.");

        if (mark == '#')
        {
            value = ReadCharReference();
            return true;
        }

        var name = new StringBuilder();
        name.Append(mark);
        while (TryRaw(out var ch) && ch != ';')
            name.Append(ch);
        var key = name.ToString();
        if (Builtin(key, out value))
            return true;

        var entity = Find(key) ?? throw new InvalidOperationException($"The entity '{key}' is not defined.");
        if (entity.External)
            throw new InvalidOperationException("External entities are not allowed.");
        if (_names.Contains(entity.Name) || _names.Count > 32)
            throw new InvalidOperationException("The entity reference is circular.");
        _names.Push(entity.Name);
        _stack.Push(new Frame(entity.Replacement));
        return true;
    }

    private char ReadCharReference()
    {
        if (!TryRaw(out var first))
            throw new InvalidOperationException("A character reference is incomplete.");
        var hex = first is 'x' or 'X';
        var digits = new StringBuilder();
        if (!hex)
            digits.Append(first);
        while (TryRaw(out var ch) && ch != ';')
            digits.Append(ch);
        var text = digits.ToString();
        if (text.Length == 0 || !int.TryParse(text, hex ? System.Globalization.NumberStyles.HexNumber : System.Globalization.NumberStyles.None, null, out var code))
            throw new InvalidOperationException("A character reference is not valid.");
        if (code is 0 or > 0x10FFFF or >= 0xD800 and <= 0xDFFF)
            throw new InvalidOperationException("A character reference names a character XML does not allow.");
        if (code <= 0xFFFF)
            return (char)code;
        Unread((char)(0xDC00 + ((code - 0x10000) & 0x3FF)));
        return (char)(0xD800 + ((code - 0x10000) >> 10));
    }

    private EntityDef? Find(string name)
    {
        foreach (var entity in _document.Entities)
        {
            if (entity.Name == name)
                return entity;
        }

        return null;
    }

    internal static bool Builtin(string name, out char value)
    {
        switch (name)
        {
            case "amp": value = '&'; return true;
            case "lt": value = '<'; return true;
            case "gt": value = '>'; return true;
            case "quot": value = '"'; return true;
            case "apos": value = '\''; return true;
            default: value = default; return false;
        }
    }

    private sealed class Frame(string text)
    {
        public string Text { get; } = text;
        public int Position { get; set; }
    }
}

internal static class ExpandedText
{
    public static bool IsWhitespaceOnly(XmlDocument document, IReadOnlyList<TextPiece> pieces)
    {
        var cursor = new ExpandedCursor(document, pieces);
        while (cursor.TryRead(out var ch))
        {
            if (ch is not (' ' or '\t' or '\n' or '\r'))
                return false;
        }

        return true;
    }

    public static string Slice(XmlDocument document, IReadOnlyList<TextPiece> pieces, int start, int count, out bool hasMore)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));
        var cursor = new ExpandedCursor(document, pieces);
        var builder = new StringBuilder(Math.Min(count, 256));
        var index = 0;
        hasMore = false;
        while (cursor.TryRead(out var ch))
        {
            if (index >= start && builder.Length < count)
                builder.Append(ch);
            else if (index >= start)
            {
                hasMore = true;
                break;
            }

            index++;
        }

        return builder.ToString();
    }

    public static int Count(XmlDocument document, IReadOnlyList<TextPiece> pieces)
    {
        var cursor = new ExpandedCursor(document, pieces);
        var count = 0;
        while (cursor.TryRead(out _))
            count++;
        return count;
    }

    public static bool EqualsExact(XmlDocument document, IReadOnlyList<TextPiece> pieces, string expected) =>
        EqualsValue(document, pieces, expected, matchCase: true);

    public static void Match(XmlDocument document, ReadOnlySpan<TextPiece> pieces, TextSearch search, List<int> offsets)
    {
        offsets.Clear();
        var needle = search.FindWhat;
        if (needle.Length == 0 || pieces.IsEmpty)
            return;
        if (pieces.Length == 1 && TryAscii(document, pieces[0], search, offsets))
            return;
        offsets.Clear();
        var copy = new TextPiece[pieces.Length];
        pieces.CopyTo(copy);
        if (search.EntireCell)
        {
            if (EqualsValue(document, copy, needle, search.MatchCase))
                offsets.Add(0);
            return;
        }

        if (search.StartsWith)
        {
            if (StartsWith(document, copy, needle, search.MatchCase))
                offsets.Add(0);
            return;
        }

        Find(document, copy, needle, search.MatchCase, offsets);
    }

    public static bool EqualsValue(XmlDocument document, IReadOnlyList<TextPiece> pieces, string expected, bool matchCase)
    {
        if (expected.Length == 0)
            return false;
        var cursor = new ExpandedCursor(document, pieces);
        var window = new char[expected.Length];
        var filled = 0;
        while (filled < expected.Length && cursor.TryRead(out var ch))
            window[filled++] = ch;
        if (filled != expected.Length || !Same(window, expected, matchCase))
            return false;
        return !cursor.TryRead(out _);
    }

    public static bool StartsWith(XmlDocument document, IReadOnlyList<TextPiece> pieces, string expected, bool matchCase)
    {
        if (expected.Length == 0)
            return false;
        var cursor = new ExpandedCursor(document, pieces);
        var window = new char[expected.Length];
        var filled = 0;
        while (filled < expected.Length && cursor.TryRead(out var ch))
            window[filled++] = ch;
        return filled == expected.Length && Same(window, expected, matchCase);
    }

    public static void Find(XmlDocument document, IReadOnlyList<TextPiece> pieces, string query, bool matchCase, List<int> offsets)
    {
        if (query.Length == 0)
            return;
        var cursor = new ExpandedCursor(document, pieces);
        var window = new char[query.Length];
        var filled = 0;
        var index = 0;
        while (cursor.TryRead(out var ch))
        {
            if (filled < query.Length)
            {
                window[filled++] = ch;
            }
            else
            {
                Array.Copy(window, 1, window, 0, query.Length - 1);
                window[^1] = ch;
            }

            index++;
            if (filled == query.Length && Same(window, query, matchCase))
                offsets.Add(index - query.Length);
        }
    }

    private static bool Same(ReadOnlySpan<char> window, string query, bool matchCase)
    {
        if (matchCase)
            return window.SequenceEqual(query);
        return window.Equals(query, StringComparison.OrdinalIgnoreCase);
    }

    // One raw ASCII piece is one byte per shown character. Anything else stays on the cursor.
    private static bool TryAscii(XmlDocument document, TextPiece piece, TextSearch search, List<int> offsets)
    {
        if (piece.EntityId >= 0 || piece.End < piece.Start || piece.End - piece.Start > int.MaxValue)
            return false;
        if (!piece.Json && document.Encoding.CodePage != 65001)
            return false;
        var needle = search.FindWhat;
        if (!IsAscii(needle))
            return false;

        var jsonString = false;
        if (piece.Json && piece.End > piece.Start)
        {
            Span<byte> one = stackalloc byte[1];
            if (document.Source.Read(piece.Start, one) != 1)
                return false;
            jsonString = one[0] == (byte)'"';
        }

        var length = piece.End - piece.Start;
        if (length < needle.Length)
            return piece.Json || piece.CData || !ContainsByte(document.Source, piece.Start, piece.End, (byte)'&');
        if (search.EntireCell)
            return MatchEntire(document.Source, piece, jsonString, needle, search.MatchCase, offsets);
        if (search.StartsWith)
            return MatchPrefix(document.Source, piece, jsonString, needle, search.MatchCase, offsets);
        return MatchSpan(document.Source, piece, jsonString, needle, search.MatchCase, offsets);
    }

    private static bool MatchPrefix(IByteSource source, TextPiece piece, bool jsonString, string needle, bool matchCase, List<int> offsets)
    {
        Span<byte> stack = stackalloc byte[256];
        byte[]? rented = null;
        var buffer = needle.Length <= stack.Length ? stack[..needle.Length] : default;
        if (needle.Length > stack.Length)
        {
            rented = ArrayPool<byte>.Shared.Rent(needle.Length);
            buffer = rented.AsSpan(0, needle.Length);
        }

        try
        {
            if (ReadFull(source, piece.Start, buffer) != needle.Length || !AllOk(buffer, piece, jsonString))
                return false;
            if (EqualAscii(buffer, needle, matchCase))
                offsets.Add(0);
            return true;
        }
        finally
        {
            if (rented is not null)
                ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private static bool MatchEntire(IByteSource source, TextPiece piece, bool jsonString, string needle, bool matchCase, List<int> offsets)
    {
        if (piece.End - piece.Start != needle.Length)
            return AllOk(source, piece, jsonString);
        return MatchPrefix(source, piece, jsonString, needle, matchCase, offsets);
    }

    private static bool MatchSpan(IByteSource source, TextPiece piece, bool jsonString, string needle, bool matchCase, List<int> offsets)
    {
        var n = needle.Length;
        var overlap = n - 1;
        Span<byte> stackPattern = stackalloc byte[128];
        byte[]? rentedNeedle = null;
        var pattern = n <= stackPattern.Length ? stackPattern[..n] : default;
        if (n > stackPattern.Length)
        {
            rentedNeedle = ArrayPool<byte>.Shared.Rent(n);
            pattern = rentedNeedle.AsSpan(0, n);
        }

        var rented = ArrayPool<byte>.Shared.Rent(64 * 1024 + overlap);
        try
        {
            FillPattern(needle, matchCase, pattern);
            var carry = 0;
            var pos = piece.Start;
            while (pos < piece.End)
            {
                var want = (int)Math.Min(64 * 1024, piece.End - pos);
                var read = ReadFull(source, pos, rented.AsSpan(carry, want));
                if (read != want)
                    return false;
                var fresh = rented.AsSpan(carry, read);
                if (!AllOk(fresh, piece, jsonString))
                    return false;
                if (!matchCase)
                    Fold(fresh);
                var window = rented.AsSpan(0, carry + read);
                var origin = (int)(pos - piece.Start - carry);
                AddHits(window, pattern, origin, offsets);
                if (pos + read >= piece.End)
                    break;
                var keep = Math.Min(overlap, window.Length);
                MoveTail(rented, window.Length, keep);
                carry = keep;
                pos += read;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
            if (rentedNeedle is not null)
                ArrayPool<byte>.Shared.Return(rentedNeedle);
        }

        return true;
    }

    private static void AddHits(ReadOnlySpan<byte> hay, ReadOnlySpan<byte> needle, int origin, List<int> offsets)
    {
        var from = 0;
        while (from <= hay.Length - needle.Length)
        {
            var rel = hay[from..].IndexOf(needle);
            if (rel < 0)
                return;
            var at = from + rel;
            offsets.Add(origin + at);
            from = at + 1;
        }
    }

    private static bool AllOk(IByteSource source, TextPiece piece, bool jsonString)
    {
        var rented = ArrayPool<byte>.Shared.Rent(64 * 1024);
        try
        {
            var pos = piece.Start;
            while (pos < piece.End)
            {
                var want = (int)Math.Min(rented.Length, piece.End - pos);
                var read = ReadFull(source, pos, rented.AsSpan(0, want));
                if (read != want || !AllOk(rented.AsSpan(0, read), piece, jsonString))
                    return false;
                pos += read;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }

        return true;
    }

    private static bool AllOk(ReadOnlySpan<byte> bytes, TextPiece piece, bool jsonString)
    {
        foreach (var b in bytes)
        {
            if (!ByteOk(b, piece, jsonString))
                return false;
        }

        return true;
    }

    private static bool ByteOk(byte value, TextPiece piece, bool jsonString)
    {
        if (value >= 0x80)
            return false;
        if (piece.Json)
            return !jsonString || value != (byte)'\\';
        if (value == (byte)'\r')
            return false;
        if (!piece.CData && value == (byte)'&')
            return false;
        if (piece.Attribute && value is (byte)'\n' or (byte)'\t')
            return false;
        return true;
    }

    private static bool ContainsByte(IByteSource source, long start, long end, byte value)
    {
        Span<byte> buf = stackalloc byte[256];
        var pos = start;
        while (pos < end)
        {
            var want = (int)Math.Min(buf.Length, end - pos);
            var read = ReadFull(source, pos, buf[..want]);
            if (read != want)
                return true;
            if (buf[..read].IndexOf(value) >= 0)
                return true;
            pos += read;
        }

        return false;
    }

    private static void MoveTail(Span<byte> buffer, int length, int keep)
    {
        if (keep <= 0)
            return;
        var from = length - keep;
        if (from >= keep)
        {
            buffer.Slice(from, keep).CopyTo(buffer);
            return;
        }

        for (var i = keep - 1; i >= 0; i--)
            buffer[i] = buffer[from + i];
    }

    private static int ReadFull(IByteSource source, long offset, Span<byte> dest)
    {
        var got = 0;
        while (got < dest.Length)
        {
            var n = source.Read(offset + got, dest[got..]);
            if (n <= 0)
                break;
            got += n;
        }

        return got;
    }

    private static void FillPattern(string needle, bool matchCase, Span<byte> pattern)
    {
        for (var i = 0; i < needle.Length; i++)
        {
            var value = (byte)needle[i];
            pattern[i] = matchCase ? value : Fold(value);
        }
    }

    private static void Fold(Span<byte> bytes)
    {
        for (var i = 0; i < bytes.Length; i++)
            bytes[i] = Fold(bytes[i]);
    }

    private static byte Fold(byte value) => (uint)(value - (byte)'A') <= 25 ? (byte)(value + 32) : value;

    private static bool EqualAscii(ReadOnlySpan<byte> bytes, string needle, bool matchCase)
    {
        for (var i = 0; i < bytes.Length; i++)
        {
            var left = bytes[i];
            var right = (byte)needle[i];
            if (left == right)
                continue;
            if (matchCase || Fold(left) != Fold(right))
                return false;
        }

        return true;
    }

    private static bool IsAscii(string text)
    {
        foreach (var ch in text)
        {
            if (ch >= 128)
                return false;
        }

        return true;
    }
}
