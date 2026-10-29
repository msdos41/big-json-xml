using System.Numerics;
using System.Runtime.CompilerServices;

namespace BigJsonXml;

/// <summary>
/// File offsets of one list's entries, in the order the scan found them. Each block of
/// <see cref="BlockSize"/> offsets keeps its first offset, a straight line through the block,
/// and every entry's distance from that line packed in as few bits as the block needs.
/// Evenly spaced entries cost no bits. Any offset reads back in constant time.
/// </summary>
internal sealed class EntryOffsets
{
    private const int BlockShift = 8;
    private const int BlockSize = 1 << BlockShift;
    private const int BlockMask = BlockSize - 1;
    private const int HeaderPageShift = 10;
    private const int HeaderPageSize = 1 << HeaderPageShift;
    private const int BitPageWords = 8192;

    private readonly List<Header[]> _headers = [];
    private readonly List<ulong[]> _bits = [];
    private long[] _tail = new long[4];
    private int _tailCount;
    private int _blocks;
    private int _wordsInLastPage = BitPageWords;

    public int Count => (_blocks << BlockShift) + _tailCount;

    public long Last => Count == 0
        ? throw new InvalidOperationException("The list has no entries.")
        : this[Count - 1];

    public long this[int index]
    {
        get
        {
            if ((uint)index >= (uint)Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            var block = index >> BlockShift;
            var at = index & BlockMask;
            if (block == _blocks)
                return _tail[at];
            ref readonly var header = ref _headers[block >> HeaderPageShift][block & (HeaderPageSize - 1)];
            var value = header.Anchor + (at * header.Step) + header.Base;
            if (header.Width == 0)
                return value;
            return value + (long)ReadBits(header.Word, at * header.Width, header.Width);
        }
    }

    // Bytes held by this list's offsets, for memory pins.
    internal long RetainedBytes =>
        (_headers.Count * (long)HeaderPageSize * Unsafe.SizeOf<Header>())
        + (_bits.Count * (long)BitPageWords * sizeof(ulong))
        + (_tail.Length * sizeof(long));

    // The last entry that starts at or before the offset, or -1 when every entry starts after it.
    public int LastAtOrBefore(long offset)
    {
        var low = 0;
        var high = Count - 1;
        var found = -1;
        while (low <= high)
        {
            var mid = low + ((high - low) >> 1);
            if (this[mid] <= offset)
            {
                found = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return found;
    }

    public void Add(long offset)
    {
        if (_tailCount == _tail.Length)
            Array.Resize(ref _tail, _tail.Length * 2);
        _tail[_tailCount++] = offset;
        if (_tailCount == BlockSize)
            Seal();
    }

    private void Seal()
    {
        var anchor = _tail[0];
        var step = (_tail[BlockSize - 1] - anchor) / (BlockSize - 1);
        var low = long.MaxValue;
        var high = long.MinValue;
        for (var i = 0; i < BlockSize; i++)
        {
            var off = _tail[i] - anchor - (i * step);
            if (off < low)
                low = off;
            if (off > high)
                high = off;
        }

        var width = 64 - BitOperations.LeadingZeroCount((ulong)(high - low));
        var word = 0;
        if (width > 0)
        {
            var words = ((BlockSize * width) + 63) / 64;
            if (_wordsInLastPage + words > BitPageWords)
            {
                _bits.Add(new ulong[BitPageWords]);
                _wordsInLastPage = 0;
            }

            word = ((_bits.Count - 1) * BitPageWords) + _wordsInLastPage;
            var page = _bits[^1];
            for (var i = 0; i < BlockSize; i++)
                WriteBits(page, _wordsInLastPage, i * width, width, (ulong)(_tail[i] - anchor - (i * step) - low));
            _wordsInLastPage += words;
        }

        if ((_blocks & (HeaderPageSize - 1)) == 0)
            _headers.Add(new Header[HeaderPageSize]);
        _headers[^1][_blocks & (HeaderPageSize - 1)] = new Header
        {
            Anchor = anchor,
            Step = step,
            Base = low,
            Word = word,
            Width = (byte)width,
        };
        _blocks++;
        _tailCount = 0;
    }

    private static void WriteBits(ulong[] page, int firstWord, int bit, int width, ulong value)
    {
        var at = firstWord + (bit >> 6);
        var shift = bit & 63;
        page[at] |= value << shift;
        if (shift + width > 64)
            page[at + 1] |= value >> (64 - shift);
    }

    private ulong ReadBits(int word, int bit, int width)
    {
        var page = _bits[word / BitPageWords];
        var at = (word % BitPageWords) + (bit >> 6);
        var shift = bit & 63;
        var value = page[at] >> shift;
        if (shift + width > 64)
            value |= page[at + 1] << (64 - shift);
        return width == 64 ? value : value & ((1UL << width) - 1);
    }

    private struct Header
    {
        public long Anchor;
        public long Step;
        public long Base;
        public int Word;
        public byte Width;
    }
}
