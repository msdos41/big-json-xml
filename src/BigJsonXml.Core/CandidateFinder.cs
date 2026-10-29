namespace BigJsonXml;

/// <summary>
/// Finds the next byte where a text-search hit could begin, without parsing. Each pattern is a
/// byte sequence that a hit's raw bytes must contain; a pattern may refuse one byte right after it.
/// The file is read in large blocks and searched with vectorized span searches.
/// </summary>
internal sealed class CandidateFinder
{
    private const int BlockSize = 1 << 20;

    private readonly IByteSource _source;
    private readonly byte[][] _patterns;
    private readonly int[] _refuseAfter;
    private readonly long[] _next;
    private readonly byte[] _block = new byte[BlockSize];
    private readonly int _overlap;
    private long _blockAt = -1;
    private int _blockCount;

    public CandidateFinder(IByteSource source, IReadOnlyList<(byte[] Bytes, int RefuseAfter)> patterns)
    {
        _source = source;
        _patterns = new byte[patterns.Count][];
        _refuseAfter = new int[patterns.Count];
        _next = new long[patterns.Count];
        var longest = 1;
        for (var i = 0; i < patterns.Count; i++)
        {
            _patterns[i] = patterns[i].Bytes;
            _refuseAfter[i] = patterns[i].RefuseAfter;
            longest = Math.Max(longest, patterns[i].Bytes.Length + 1);
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
            var found = span[at..].IndexOf(pattern);
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
}
