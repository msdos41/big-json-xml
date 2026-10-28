using System.Diagnostics;

namespace BigJsonXml.Json;

internal sealed partial class JsonParser
{
    // How many items run between two clock reads.
    private const int ScalarRunCheck = 1024;

    // Later items of an array that are plain scalars, one window at a time. Each item's start
    // goes straight to the list's offsets; no event or column work is done per item.
    // The run stops before anything else: an object, an array, an escaped or non-ASCII string,
    // an item cut by the end of the bytes read so far, or a byte that is not valid JSON.
    // The item reader then takes that item and reports any error at the same place it always did.
    internal int TakeScalarRun(EntryOffsets starts, long deadline)
    {
        if (_failed || _done || _inString || _seeking || _top < 0 || _phase[_top] != PhArrNext)
            return 0;
        if (_lineDirty || _holdLine)
            return 0;

        var taken = 0;
        var line = _line;
        var column = _column;
        var cr = _cr;
        while (true)
        {
            if (!Window(out var span, out _))
                break;
            var origin = _pos;
            var i = 0;
            var stopped = false;
            while (true)
            {
                if ((taken & (ScalarRunCheck - 1)) == ScalarRunCheck - 1 && deadline != 0 && Stopwatch.GetTimestamp() >= deadline)
                {
                    stopped = true;
                    break;
                }

                // One item: optional gap, a comma, optional gap, a scalar, and a byte that may follow it.
                var at = i;
                var itemLine = line;
                var itemColumn = column;
                var itemCr = cr;
                if (!SkipRunGap(span, ref at, ref line, ref column, ref cr) || span[at] != (byte)',')
                {
                    line = itemLine;
                    column = itemColumn;
                    cr = itemCr;
                    stopped = at < span.Length;
                    break;
                }

                at++;
                column++;
                cr = false;
                if (!SkipRunGap(span, ref at, ref line, ref column, ref cr))
                {
                    line = itemLine;
                    column = itemColumn;
                    cr = itemCr;
                    break;
                }

                var start = at;
                var end = ScalarEnd(span, start);
                if (end <= 0)
                {
                    line = itemLine;
                    column = itemColumn;
                    cr = itemCr;
                    stopped = end == 0;
                    break;
                }

                starts.Add(origin + start);
                taken++;
                column += end - start;
                cr = false;
                i = end;
            }

            _pos = origin + i;
            if (stopped || _pos >= _source.Length)
                break;

            // The next item straddles the window. Read again from its comma.
            if (i == 0)
            {
                if (origin + span.Length >= _source.Length)
                    break;
                _windowAt = -1;
                _windowCount = 0;
                if (!Window(out var fresh, out _) || fresh.Length <= span.Length)
                    break;
                continue;
            }

            _windowAt = -1;
            _windowCount = 0;
        }

        _line = line;
        _column = column;
        _cr = cr;
        _lineAt = _pos;
        return taken;
    }

    // Skips spaces and breaks, counting lines. False when the span ends first.
    private static bool SkipRunGap(ReadOnlySpan<byte> span, ref int at, ref int line, ref int column, ref bool cr)
    {
        while (at < span.Length)
        {
            var value = span[at];
            if (value == (byte)' ' || value == (byte)'\t')
            {
                column++;
                cr = false;
            }
            else if (value == (byte)'\n')
            {
                if (!cr)
                {
                    line++;
                    column = 1;
                }

                cr = false;
            }
            else if (value == (byte)'\r')
            {
                line++;
                column = 1;
                cr = true;
            }
            else
            {
                return true;
            }

            at++;
        }

        return false;
    }

    // The end of a plain scalar at start, when the byte after it is in the span and may follow an array item.
    // 0 means the item is not one the run takes. -1 means the span ends first.
    private static int ScalarEnd(ReadOnlySpan<byte> span, int start)
    {
        var i = start;
        var head = span[i];
        if (head == (byte)'"')
        {
            var rest = span[(i + 1)..];
            var stop = rest.IndexOfAny(StringStops);
            if (stop < 0)
                return -1;
            if (rest[stop] != (byte)'"')
                return 0;
            i += stop + 2;
        }
        else if (head == (byte)'t')
        {
            if (!Word(span, ref i, "true"u8, out var cut))
                return cut ? -1 : 0;
        }
        else if (head == (byte)'f')
        {
            if (!Word(span, ref i, "false"u8, out var cut))
                return cut ? -1 : 0;
        }
        else if (head == (byte)'n')
        {
            if (!Word(span, ref i, "null"u8, out var cut))
                return cut ? -1 : 0;
        }
        else if (head == (byte)'-' || IsDigit(head))
        {
            var number = NumberEnd(span, i);
            if (number <= 0)
                return number;
            i = number;
        }
        else
        {
            return 0;
        }

        if (i >= span.Length)
            return -1;
        return span[i] is (byte)',' or (byte)']' or (byte)' ' or (byte)'\t' or (byte)'\n' or (byte)'\r' ? i : 0;
    }

    private static bool Word(ReadOnlySpan<byte> span, ref int i, ReadOnlySpan<byte> word, out bool cut)
    {
        cut = false;
        if (span.Length - i < word.Length)
        {
            cut = word.StartsWith(span[i..]);
            return false;
        }

        if (!span.Slice(i, word.Length).SequenceEqual(word))
            return false;
        i += word.Length;
        return true;
    }

    private static int NumberEnd(ReadOnlySpan<byte> span, int i)
    {
        if (span[i] == (byte)'-')
        {
            i++;
            if (i >= span.Length)
                return -1;
        }

        if (span[i] == (byte)'0')
        {
            i++;
        }
        else if (IsDigit(span[i]))
        {
            i = Digits(span, i);
        }
        else
        {
            return 0;
        }

        if (i >= span.Length)
            return -1;
        if (span[i] == (byte)'.')
        {
            var after = Digits(span, i + 1);
            if (after >= span.Length)
                return -1;
            if (after == i + 1)
                return 0;
            i = after;
        }

        if (span[i] is (byte)'e' or (byte)'E')
        {
            i++;
            if (i >= span.Length)
                return -1;
            if (span[i] is (byte)'+' or (byte)'-')
            {
                i++;
                if (i >= span.Length)
                    return -1;
            }

            var after = Digits(span, i);
            if (after >= span.Length)
                return -1;
            if (after == i)
                return 0;
            i = after;
        }

        return i;
    }

    private static int Digits(ReadOnlySpan<byte> span, int i)
    {
        if (i >= span.Length)
            return i;
        var run = span[i..].IndexOfAnyExcept(DigitBytes);
        return run < 0 ? span.Length : i + run;
    }
}
