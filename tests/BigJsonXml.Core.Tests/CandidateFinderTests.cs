namespace BigJsonXml.Tests;

public class CandidateFinderTests
{
    private static MemoryByteSource Filled(int length, params (long At, string Text)[] marks)
    {
        var bytes = new byte[length];
        Array.Fill(bytes, (byte)'a');
        foreach (var (at, text) in marks)
        {
            for (var i = 0; i < text.Length; i++)
                bytes[at + i] = (byte)text[i];
        }

        var source = new MemoryByteSource();
        source.Append(bytes);
        source.Complete();
        return source;
    }

    [Fact]
    public void Candidates_cut_by_a_block_edge_are_still_found_in_order()
    {
        const int block = 1 << 20;
        var source = Filled(3 * block + 17, (block - 2, "xyz"), (2 * block - 1, "<"), (3 * block + 14, "xyz"));
        var finder = new CandidateFinder(source, [("xyz"u8.ToArray(), -1), ("x<"u8.ToArray(), '/'), ("<"u8.ToArray(), '/')]);

        Assert.Equal(block - 2, finder.Next(0));
        Assert.Equal(2 * block - 1, finder.Next(block - 1));
        Assert.Equal(3 * block + 14, finder.Next(2 * block));
        Assert.Equal(source.Length, finder.Next(3 * block + 15));
    }

    [Fact]
    public void A_refused_byte_after_a_pattern_skips_that_place()
    {
        var source = Filled(100, (10, "</"), (20, "<b"));
        var finder = new CandidateFinder(source, [("<"u8.ToArray(), '/')]);

        Assert.Equal(20, finder.Next(0));
        Assert.Equal(100, finder.Next(21));
    }

    [Fact]
    public void Entry_offsets_find_the_last_start_at_or_before_a_byte()
    {
        var offsets = new EntryOffsets();
        for (var i = 0; i < 1000; i++)
            offsets.Add(10 + i * 7L);

        Assert.Equal(-1, offsets.LastAtOrBefore(9));
        Assert.Equal(0, offsets.LastAtOrBefore(10));
        Assert.Equal(0, offsets.LastAtOrBefore(16));
        Assert.Equal(1, offsets.LastAtOrBefore(17));
        Assert.Equal(999, offsets.LastAtOrBefore(long.MaxValue));
    }

    [Fact]
    public void A_folded_search_finds_the_first_place_in_either_case()
    {
        var random = new Random(53);
        var alphabet = "aAbB<>-_@`[{zZ09"u8.ToArray();
        for (var round = 0; round < 2000; round++)
        {
            var hay = new byte[random.Next(0, 80)];
            for (var i = 0; i < hay.Length; i++)
                hay[i] = alphabet[random.Next(alphabet.Length)];
            var needle = new byte[random.Next(1, 5)];
            for (var i = 0; i < needle.Length; i++)
                needle[i] = alphabet[random.Next(alphabet.Length)];
            var lower = needle.Select(b => b is >= (byte)'A' and <= (byte)'Z' ? (byte)(b + 32) : b).ToArray();
            var expected = -1;
            for (var at = 0; at + needle.Length <= hay.Length && expected < 0; at++)
            {
                var text = System.Text.Encoding.ASCII.GetString(hay, at, needle.Length);
                if (text.Equals(System.Text.Encoding.ASCII.GetString(needle), StringComparison.OrdinalIgnoreCase))
                    expected = at;
            }

            Assert.Equal(expected, CandidateFinder.IndexOfFolded(hay, lower));
        }
    }

    [Fact]
    public void A_folded_pattern_is_found_across_a_block_edge()
    {
        const int block = 1 << 20;
        var source = Filled(2 * block, (block - 2, "XyZ"), (block + 40, "</"), (block + 60, "x<"));
        var finder = new CandidateFinder(source, [new CandidatePattern("xyz"u8.ToArray(), Fold: true), new CandidatePattern("X<"u8.ToArray(), '/', Fold: true)]);

        Assert.Equal(block - 2, finder.Next(0));
        Assert.Equal(block + 60, finder.Next(block - 1));
    }
}
