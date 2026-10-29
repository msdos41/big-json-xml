using System.Text;

namespace BigJsonXml;

/// <summary>
/// Finds the start tags of the root list's entries with a vectorized byte search instead of parsing.
/// The result is used only when it finds exactly as many tags as the scan counted entries: every
/// entry tag is found, so the same count means no tag of that name hides in a comment, a CDATA
/// section or a deeper element.
/// </summary>
internal static class EntryTagScan
{
    private const int BlockSize = 1 << 20;

    public static EntryOffsets? Build(IByteSource source, SkipList list, Func<bool> cancelled)
    {
        var pattern = Encoding.UTF8.GetBytes("<" + list.Name);
        var offsets = new EntryOffsets();
        var block = new byte[BlockSize];
        var at = list.ContentStart;
        var end = Math.Min(list.ContentEnd, source.Length);
        while (at < end)
        {
            if (cancelled())
                return null;
            var want = (int)Math.Min(block.Length, end - at);
            var got = 0;
            while (got < want)
            {
                var n = source.Read(at + got, block.AsSpan(got, want - got));
                if (n <= 0)
                    return null;
                got += n;
            }

            var last = at + got >= end;
            // A tag and the byte after it must lie inside this block. A cut tag is read again next block.
            var limit = last ? got : got - pattern.Length;
            if (limit <= 0)
                return null;
            var span = block.AsSpan(0, got);
            var i = 0;
            while (i < limit)
            {
                var found = span[i..].IndexOf(pattern);
                if (found < 0 || i + found >= limit)
                    break;
                var start = i + found;
                var after = start + pattern.Length;
                if (after < got && IsNameEnd(span[after]))
                {
                    if (offsets.Count >= list.Expected)
                        return null;
                    offsets.Add(at + start);
                }

                i = start + 1;
            }

            at += limit;
        }

        return offsets.Count == list.Expected ? offsets : null;
    }

    private static bool IsNameEnd(byte b) => b is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or (byte)'/' or (byte)'>';
}
