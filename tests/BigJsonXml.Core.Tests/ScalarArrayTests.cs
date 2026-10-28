using System.Text;

namespace BigJsonXml.Tests;

public class ScalarArrayTests
{
    private const int Count = 30_000;

    [Fact]
    public void Every_entry_of_a_long_scalar_array_reads_back_its_literal()
    {
        var (json, literals) = ScalarArray(Count);
        var session = Open(json);

        AssertEntries(session, literals);
    }

    [Fact]
    public void A_scalar_array_fed_in_pieces_reads_the_same()
    {
        var (json, literals) = ScalarArray(Count);
        var bytes = Encoding.UTF8.GetBytes(json);
        var source = new MemoryByteSource();
        var session = ViewerSession.Open(source);
        for (var at = 0; at < bytes.Length; at += 4_093)
        {
            source.Append(bytes.AsSpan(at, Math.Min(4_093, bytes.Length - at)));
            while (session.Pump(64))
            {
            }
        }

        source.Complete();
        session.PumpAll();

        AssertEntries(session, literals);
    }

    [Fact]
    public void A_scalar_array_under_a_key_keeps_the_fields_after_it()
    {
        var (items, literals) = ScalarArray(Count);
        var session = Open("{\"a\":" + items + ",\"b\":1}");

        Assert.Equal(ScanProgress.Completed, session.Progress);
        Assert.Equal("1", session.Field("b").Text);
        var list = DocumentPath.Root.Field("a");
        Assert.Equal(Count, session.Field("a").ListCount);
        session.Expand(list);
        var view = session.ReadList(list, Count - 2, 2);
        Assert.Equal(literals[^2], view.Entries[0].Cells[0].Text);
        Assert.Equal(literals[^1], view.Entries[1].Cells[0].Text);
    }

    [Fact]
    public void Objects_between_scalars_add_their_keys_beside_the_value_column()
    {
        var json = new StringBuilder("[");
        for (var i = 0; i < 3_000; i++)
        {
            if (i > 0)
                json.Append(',');
            json.Append(i % 1_000 == 500 ? "{\"k\":" + i + "}" : i.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        json.Append(']');
        var session = Open(json.ToString());
        var list = DocumentPath.Root.Field("#value");
        session.Expand(list);
        var view = session.ReadList(list, 499, 3);

        Assert.Equal(["#value", "k"], view.Columns);
        Assert.Equal("499", view.Entries[0].Cells[0].Text);
        Assert.Equal("", view.Entries[1].Cells[0].Text);
        Assert.Equal("500", view.Entries[1].Cells[1].Text);
        Assert.Equal("501", view.Entries[2].Cells[0].Text);
        Assert.Equal(3_000, session.Field("#value").ListCount);
    }

    [Fact]
    public void Search_names_the_entry_of_a_scalar_hit()
    {
        var (json, _) = ScalarArray(Count);
        json = json.Replace("\"s24999\"", "\"needle\"", StringComparison.Ordinal);
        var session = Open(json);

        session.Search(new TextSearch { FindWhat = "needle" });
        session.PumpAll();

        var hit = Assert.Single(session.Hits);
        Assert.Equal("/#value[25000]/#value", session.FormatPath(hit.Path));
        session.Reveal(hit);
        Assert.Equal("\"needle\"", session.ReadScalar(hit.Path).Text);
    }

    [Theory]
    [InlineData("01", 5_000, 2, 4)]
    [InlineData("1.", 5_000, 2, 5)]
    [InlineData("tru", 5_000, 2, 6)]
    [InlineData("x", 5_000, 2, 3)]
    [InlineData("1 2", 5_001, 2, 5)]
    public void A_bad_item_after_a_long_run_stops_on_its_line_and_column(string bad, int finished, int line, int column)
    {
        var json = "[" + string.Concat(Enumerable.Repeat("1,", 5_000)) + "\n  " + bad + "]";
        var session = Open(json);

        Assert.Equal(ScanProgress.Stopped, session.Progress);
        Assert.Equal(finished, session.Field("#value").ListCount);
        Assert.Equal(line, session.Error!.Line);
        Assert.Equal(column, session.Error.Column);
    }

    [Fact]
    public void Scanning_a_scalar_array_does_not_allocate_per_entry()
    {
        const int entries = 1_000_000;
        var bytes = new byte[1 + (entries * 2)];
        bytes[0] = (byte)'[';
        for (var i = 0; i < entries; i++)
        {
            bytes[1 + (i * 2)] = (byte)('0' + (i % 10));
            bytes[2 + (i * 2)] = (byte)',';
        }

        bytes[^1] = (byte)']';
        var source = new MemoryByteSource();
        source.Append(bytes);
        source.Complete();
        var session = ViewerSession.Open(source);

        var before = GC.GetAllocatedBytesForCurrentThread();
        session.PumpAll();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(ScanProgress.Completed, session.Progress);
        Assert.Equal(entries, session.Field("#value").ListCount);
        Assert.True(allocated < entries / 2, $"scan allocated {allocated} bytes");
    }

    [Fact]
    public void Scanning_a_long_xml_list_does_not_allocate_per_entry()
    {
        const int entries = 500_000;
        var xml = new StringBuilder("<r>", 8 * entries + 16);
        for (var i = 0; i < entries; i++)
            xml.Append("<a>").Append((char)('0' + (i % 10))).Append("</a>");
        xml.Append("</r>");
        var source = new MemoryByteSource();
        source.Append(xml.ToString());
        source.Complete();
        var session = ViewerSession.Open(source);

        var before = GC.GetAllocatedBytesForCurrentThread();
        session.PumpAll();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(ScanProgress.Completed, session.Progress);
        Assert.Equal(entries, session.Field("a").ListCount);

        // The reader's own buffers are a fixed few hundred kilobytes. Eight-byte offsets alone were 4 MB.
        Assert.True(allocated < entries * 2, $"scan allocated {allocated} bytes");

        var list = DocumentPath.Root.Field("a");
        session.Expand(list);
        var view = session.ReadList(list, 444_443, 2);
        Assert.Equal("3", view.Entries[0].Cells[0].Text);
        Assert.Equal("4", view.Entries[1].Cells[0].Text);
    }

    private static void AssertEntries(ViewerSession session, List<string> literals)
    {
        Assert.Equal(ScanProgress.Completed, session.Progress);
        var list = DocumentPath.Root.Field("#value");
        Assert.Equal(literals.Count, session.Field("#value").ListCount);
        session.Expand(list);
        foreach (var start in new[] { 0, 1, 255, 256, 257, 4_095, 12_345, literals.Count - 40 })
        {
            var view = session.ReadList(list, start, 40);
            Assert.Equal(["#value"], view.Columns);
            for (var i = 0; i < view.Entries.Count; i++)
            {
                Assert.Equal(start + i + 1, view.Entries[i].OneBasedIndex);
                Assert.Equal(literals[start + i], view.Entries[i].Cells[0].Text);
            }
        }
    }

    // Numbers, strings, and literals of uneven length, with spaces and breaks between some items.
    private static (string Json, List<string> Literals) ScalarArray(int count)
    {
        var json = new StringBuilder("[");
        var literals = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            var literal = (i % 7) switch
            {
                0 => i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                1 => "-" + i + ".5e-3",
                2 => "\"s" + i + "\"",
                3 => "true",
                4 => "null",
                5 => "false",
                _ => "0",
            };
            if (i > 0)
                json.Append(i % 11 == 0 ? ",\n  " : i % 13 == 0 ? " , " : ",");
            json.Append(literal);
            literals.Add(literal);
        }

        json.Append("\n]");
        return (json.ToString(), literals);
    }

    private static ViewerSession Open(string json)
    {
        var source = new MemoryByteSource();
        source.Append(json);
        source.Complete();
        var session = ViewerSession.Open(source);
        session.PumpAll();
        return session;
    }
}
