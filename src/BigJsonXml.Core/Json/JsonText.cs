namespace BigJsonXml.Json;

internal static class JsonUtf8
{
    public static bool TryDecode(ReadOnlySpan<byte> data, bool complete, out int codePoint, out int bytes, out bool needMore)
    {
        codePoint = 0;
        bytes = 0;
        needMore = false;
        if (data.IsEmpty)
        {
            needMore = !complete;
            return false;
        }

        var first = data[0];
        if (first < 0x80)
        {
            codePoint = first;
            bytes = 1;
            return true;
        }

        int need;
        int min;
        if (first is >= 0xC2 and <= 0xDF)
        {
            need = 2;
            min = 0x80;
        }
        else if (first is >= 0xE0 and <= 0xEF)
        {
            need = 3;
            min = 0x800;
        }
        else if (first is >= 0xF0 and <= 0xF4)
        {
            need = 4;
            min = 0x10000;
        }
        else
        {
            return false;
        }

        if (data.Length < need)
        {
            needMore = !complete;
            return false;
        }

        var point = first & (need == 2 ? 0x1F : need == 3 ? 0x0F : 0x07);
        for (var i = 1; i < need; i++)
        {
            var next = data[i];
            if ((next & 0xC0) != 0x80)
                return false;
            point = (point << 6) | (next & 0x3F);
        }

        if (point < min || point > 0x10FFFF || point is >= 0xD800 and <= 0xDFFF)
            return false;
        if (first == 0xE0 && data[1] < 0xA0)
            return false;
        if (first == 0xED && data[1] >= 0xA0)
            return false;
        if (first == 0xF0 && data[1] < 0x90)
            return false;
        if (first == 0xF4 && data[1] >= 0x90)
            return false;
        codePoint = point;
        bytes = need;
        return true;
    }
}

/// <summary>
/// One JSON scalar as the literal a cell shows. Strings are decoded and quoted again,
/// and only the characters asked for are produced.
/// </summary>
internal sealed class JsonChars
{
    private readonly IByteSource _source;
    private readonly long _end;
    private long _pos;
    private Mode _mode;
    private char _held;
    private bool _hasHeld;
    private int _hex;
    private int _hexLeft;
    private int _high;
    private bool _wantLow;

    private enum Mode { Quote, Body, Escape, Hex, Close, Raw, Done }

    public JsonChars(IByteSource source, long start, long end)
    {
        _source = source;
        _end = end;
        _pos = start;
        _mode = start < end && ReadByte(start) == (byte)'"' ? Mode.Quote : Mode.Raw;
    }

    public bool TryRead(out char value)
    {
        while (true)
        {
            if (_hasHeld)
            {
                _hasHeld = false;
                value = _held;
                return true;
            }

            switch (_mode)
            {
                case Mode.Done:
                    value = default;
                    return false;
                case Mode.Quote:
                    _pos++;
                    _mode = Mode.Body;
                    value = '"';
                    return true;
                case Mode.Close:
                    _mode = Mode.Done;
                    value = '"';
                    return true;
                case Mode.Raw:
                    if (_pos >= _end)
                    {
                        value = default;
                        return false;
                    }

                    value = (char)ReadByte(_pos);
                    _pos++;
                    return true;
                case Mode.Escape:
                    if (!Take(out var escaped))
                    {
                        value = default;
                        return false;
                    }

                    if (escaped == (byte)'u')
                    {
                        _mode = Mode.Hex;
                        _hex = 0;
                        _hexLeft = 4;
                        continue;
                    }

                    _mode = Mode.Body;
                    if (!Mapped(escaped, out var unit))
                    {
                        value = default;
                        return false;
                    }

                    if (Give(unit, out value))
                        return true;
                    continue;
                case Mode.Hex:
                    if (!Take(out var digit))
                    {
                        value = default;
                        return false;
                    }

                    var nibble = Hex(digit);
                    if (nibble < 0)
                    {
                        value = default;
                        return false;
                    }

                    _hex = (_hex << 4) | nibble;
                    _hexLeft--;
                    if (_hexLeft > 0)
                        continue;
                    _mode = Mode.Body;
                    if (Give(_hex, out value))
                        return true;
                    continue;
                default:
                    if (_pos >= _end)
                    {
                        value = default;
                        return false;
                    }

                    var raw = ReadByte(_pos);
                    if (raw == (byte)'"')
                    {
                        _pos++;
                        _mode = Mode.Close;
                        continue;
                    }

                    if (raw == (byte)'\\')
                    {
                        _pos++;
                        _mode = Mode.Escape;
                        continue;
                    }

                    if (raw < 0x80)
                    {
                        _pos++;
                        value = (char)raw;
                        return true;
                    }

                    if (!TryCode(out var point, out var size))
                    {
                        value = default;
                        return false;
                    }

                    _pos += size;
                    if (point <= 0xFFFF)
                    {
                        value = (char)point;
                        return true;
                    }

                    var rest = point - 0x10000;
                    _held = (char)(0xDC00 + (rest & 0x3FF));
                    _hasHeld = true;
                    value = (char)(0xD800 + (rest >> 10));
                    return true;
            }
        }
    }

    private bool Give(int unit, out char value)
    {
        value = default;
        if (_wantLow)
        {
            _wantLow = false;
            if (unit is < 0xDC00 or > 0xDFFF)
                return false;
            Hold((char)unit);
            value = (char)_high;
            return true;
        }

        if (unit is >= 0xD800 and <= 0xDBFF)
        {
            _wantLow = true;
            _high = unit;
            return false;
        }

        if (unit is '"' or '\\')
        {
            Hold((char)unit);
            value = '\\';
            return true;
        }

        value = (char)unit;
        return true;
    }

    private void Hold(char value)
    {
        _held = value;
        _hasHeld = true;
    }

    private bool Take(out byte value)
    {
        if (_pos >= _end)
        {
            value = 0;
            return false;
        }

        value = ReadByte(_pos);
        _pos++;
        return true;
    }

    private bool TryCode(out int point, out int size)
    {
        Span<byte> data = stackalloc byte[4];
        var count = (int)Math.Min(4, _end - _pos);
        for (var i = 0; i < count; i++)
            data[i] = ReadByte(_pos + i);
        return JsonUtf8.TryDecode(data[..count], true, out point, out size, out _);
    }

    private byte ReadByte(long offset)
    {
        Span<byte> one = stackalloc byte[1];
        return _source.Read(offset, one) == 1 ? one[0] : (byte)0;
    }

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
}
