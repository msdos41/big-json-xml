namespace BigJsonXml.Tests;

public class EntryOffsetsTests
{
    [Fact]
    public void Every_offset_reads_back_while_and_after_it_is_added()
    {
        var offsets = new EntryOffsets();
        var expected = new List<long>();
        var random = new Random(7);
        long at = 3;
        for (var i = 0; i < 5_000; i++)
        {
            at += i % 97 == 0 ? random.Next(1, 1_000_000) : random.Next(1, 300);
            offsets.Add(at);
            expected.Add(at);
            Assert.Equal(at, offsets[i]);
            Assert.Equal(i + 1, offsets.Count);
        }

        for (var i = 0; i < expected.Count; i++)
            Assert.Equal(expected[i], offsets[i]);
        Assert.Equal(expected[^1], offsets.Last);
    }

    [Fact]
    public void Offsets_past_four_gigabytes_and_huge_gaps_keep_their_value()
    {
        var offsets = new EntryOffsets();
        var expected = new List<long>();
        long at = 5L << 32;
        for (var i = 0; i < 1_000; i++)
        {
            at += i % 300 == 299 ? 3L << 40 : 11 + (i % 3);
            offsets.Add(at);
            expected.Add(at);
        }

        for (var i = 0; i < expected.Count; i++)
            Assert.Equal(expected[i], offsets[i]);
    }

    [Fact]
    public void An_index_outside_the_list_is_refused()
    {
        var offsets = new EntryOffsets();
        offsets.Add(10);
        Assert.Throws<ArgumentOutOfRangeException>(() => offsets[1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => offsets[-1]);
        Assert.Throws<InvalidOperationException>(() => new EntryOffsets().Last);
    }

    [Fact]
    public void Evenly_spaced_entries_cost_well_under_one_byte_each()
    {
        var offsets = new EntryOffsets();
        const int count = 1_000_000;
        for (var i = 0; i < count; i++)
            offsets.Add(1 + (8L * i));

        Assert.Equal(count, offsets.Count);
        Assert.Equal(1 + (8L * 777_777), offsets[777_777]);
        Assert.True(offsets.RetainedBytes < count / 4, $"retained {offsets.RetainedBytes} bytes");
    }

    [Fact]
    public void Uneven_entries_cost_a_few_bytes_each_not_eight()
    {
        var offsets = new EntryOffsets();
        var random = new Random(11);
        const int count = 200_000;
        long at = 0;
        for (var i = 0; i < count; i++)
        {
            at += random.Next(150, 450);
            offsets.Add(at);
        }

        Assert.True(offsets.RetainedBytes < count * 3L, $"retained {offsets.RetainedBytes} bytes");
    }

    [Fact]
    public void A_short_list_stays_small()
    {
        var offsets = new EntryOffsets();
        for (var i = 0; i < 3; i++)
            offsets.Add(100 + i);
        Assert.True(offsets.RetainedBytes <= 128, $"retained {offsets.RetainedBytes} bytes");
    }
}
