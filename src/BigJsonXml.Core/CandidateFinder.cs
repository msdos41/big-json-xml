using System.Numerics;
using System.Runtime.Intrinsics;

namespace BigJsonXml;

/// <summary>
/// One byte sequence a hit's raw bytes must contain. <paramref name="RefuseAfter"/> is a byte that may
/// not follow it, or -1. With <paramref name="Fold"/>, ASCII letters match in either case.
/// </summary>
internal readonly record struct CandidatePattern(byte[] Bytes, int RefuseAfter = -1, bool Fold = false);

/// <summary>
/// Finds the next byte where a text-search hit could begin, without parsing. Each pattern is a
/// byte sequence that a hit's raw bytes must contain; a pattern may refuse one byte right after it.
/// The file is read in large blocks and searched with vectorized span searches. A folded pattern
/// compares its first and last bytes sixteen at a time and checks the rest at each place both match.
/// </summary>
internal sealed class CandidateFinder
{
    private const int BlockSize = 1 << 20;

    private readonly IByteSource _source;
    private readonly byte[][] _patterns;
    private readonly int[] _refuseAfter;
    private readonly bool[] _fold;
    private readonly long[] _next;
    private readonly byte[] _block = new byte[BlockSize];
    private readonly int _overlap;
    private long _blockAt = -1;
    private int _blockCount;

    public CandidateFinder(IByteSource source, IReadOnlyList<(byte[] Bytes, int RefuseAfter)> patterns)
        : this(source, patterns.Select(pattern => new CandidatePattern(pattern.Bytes, pattern.RefuseAfter)).ToList())
    {
    }

    public CandidateFinder(IByteSource source, IReadOnlyList<CandidatePattern> patterns)
    {
        _source = source;
        _patterns = new byte[patterns.Count][];
        _refuseAfter = new int[patterns.Count];
        _fold = new bool[patterns.Count];
        _next = new long[patterns.Count];
        var longest = 1;
        for (var i = 0; i < patterns.Count; i++)
        {
            var bytes = patterns[i].Bytes;
            // A pattern without letters reads the same in both cases, so the plain search serves it.
            var fold = patterns[i].Fold && bytes.Any(IsLetter);
            _patterns[i] = fold ? bytes.Select(Lower).ToArray() : bytes;
            _refuseAfter[i] = patterns[i].RefuseAfter;
            _fold[i] = fold;
            longest = Math.Max(longest, bytes.Length + 1);
        }

        _overlap = longest;
    }

    // Bytes read so far, for tests and measurements.
    internal long BytesRead { get; private set; }

    /// <summary>The first candidate at or after <paramref name="from"/>, or the source length when there is none.</summary>
    public long Next(long from) => Next(from, long.MaxValue);

    /// <summary>
    /// The first candidate at or after <paramref name="from"/>. Stops looking at <paramref name="limit"/>
    /// and returns it, or the source length when that comes first.
    /// </summary>
    public long Next(long from, long limit)
    {
        var length = Math.Min(_source.Length, limit);
        while (true)
        {
            if (from >= length)
                return length;
            var size = _source.Length;
            if (_blockAt < 0 || from < _blockAt || from >= ValidEnd(size))
                Load(from, size);

            var best = long.MaxValue;
            for (var i = 0; i < _patterns.Length; i++)
            {
                if (_next[i] < from)
                    _next[i] = Find(i, from);
                if (_next[i] < best)
                    best = _next[i];
            }

            var valid = ValidEnd(size);
            // Past the valid end another pattern may still begin earlier, cut by the block's end.
            if (best < valid)
                return Math.Min(best, length);
            if (_blockAt + _blockCount >= size)
                return Math.Min(best, length);
            from = valid;
        }
    }

    // A pattern cut by the block's end is searched again from the next block.
    private long ValidEnd(long length)
    {
        var end = _blockAt + _blockCount;
        if (end >= length)
            return length;
        var valid = end - _overlap;
        return valid > _blockAt ? valid : end;
    }

    private void Load(long from, long length)
    {
        var want = (int)Math.Min(_block.Length, length - from);
        var got = 0;
        while (got < want)
        {
            var n = _source.Read(from + got, _block.AsSpan(got, want - got));
            if (n <= 0)
                break;
            got += n;
        }

        BytesRead += got;
        _blockAt = from;
        _blockCount = got;
        Array.Fill(_next, -1L);
    }

    private long Find(int index, long from)
    {
        var pattern = _patterns[index];
        var refuse = _refuseAfter[index];
        var span = _block.AsSpan(0, _blockCount);
        var at = (int)(from - _blockAt);
        while (at < span.Length)
        {
            var found = _fold[index] ? IndexOfFolded(span[at..], pattern) : span[at..].IndexOf(pattern);
            if (found < 0)
                return long.MaxValue;
            var start = at + found;
            var after = start + pattern.Length;
            if (refuse < 0 || after >= span.Length || span[after] != refuse)
                return _blockAt + start;
            at = start + 1;
        }

        return long.MaxValue;
    }

    private static bool IsLetter(byte value) => (uint)((value | 0x20) - 'a') <= 'z' - 'a';

    private static byte Lower(byte value) => IsLetter(value) ? (byte)(value | 0x20) : value;

    /// <summary>The first place <paramref name="lower"/> matches with ASCII letters in either case, or -1.</summary>
    internal static int IndexOfFolded(ReadOnlySpan<byte> hay, ReadOnlySpan<byte> lower)
    {
        var last = lower.Length - 1;
        if (last < 0)
            return 0;
        var end = hay.Length - last;
        if (end <= 0)
            return -1;
        var i = 0;
        if (Vector128.IsHardwareAccelerated && end >= Vector128<byte>.Count)
        {
            // A letter is compared with the 0x20 bit set; any other byte is compared as it is.
            var firstMask = Vector128.Create(IsLetter(lower[0]) ? (byte)0x20 : (byte)0);
            var firstValue = Vector128.Create(lower[0]);
            var lastMask = Vector128.Create(IsLetter(lower[last]) ? (byte)0x20 : (byte)0);
            var lastValue = Vector128.Create(lower[last]);
            ref var start = ref System.Runtime.InteropServices.MemoryMarshal.GetReference(hay);
            for (; i + Vector128<byte>.Count <= end; i += Vector128<byte>.Count)
            {
                var head = Vector128.LoadUnsafe(ref start, (nuint)i);
                var tail = Vector128.LoadUnsafe(ref start, (nuint)(i + last));
                var both = Vector128.Equals(head | firstMask, firstValue) & Vector128.Equals(tail | lastMask, lastValue);
                var bits = both.ExtractMostSignificantBits();
                while (bits != 0)
                {
                    var at = i + BitOperations.TrailingZeroCount(bits);
                    if (SameFolded(hay.Slice(at, lower.Length), lower))
                        return at;
                    bits &= bits - 1;
                }
            }
        }

        for (; i < end; i++)
        {
            if (Lower(hay[i]) == lower[0] && SameFolded(hay.Slice(i, lower.Length), lower))
                return i;
        }

        return -1;
    }

    private static bool SameFolded(ReadOnlySpan<byte> bytes, ReadOnlySpan<byte> lower)
    {
        for (var i = 0; i < lower.Length; i++)
        {
            if (Lower(bytes[i]) != lower[i])
                return false;
        }

        return true;
    }
}
