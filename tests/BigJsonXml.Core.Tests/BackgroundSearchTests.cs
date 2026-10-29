using System.Diagnostics;
using System.Text;

namespace BigJsonXml.Tests;

public class BackgroundSearchTests
{
    // Same bytes, but no second reader, so search stays on the pump as before.
    private sealed class PumpOnly(MemoryByteSource inner) : IByteSource
    {
        public long Length => inner.Length;

        public bool IsComplete => inner.IsComplete;

        public int Read(long offset, Span<byte> buffer) => inner.Read(offset, buffer);
    }

    private static MemoryByteSource Bytes(string text)
    {
        var source = new MemoryByteSource();
        source.Append(Encoding.UTF8.GetBytes(text));
        source.Complete();
        return source;
    }

    private static ViewerSession Scan(IByteSource source)
    {
        var session = ViewerSession.Open(source);
        session.PumpAll();
        Assert.Equal(ScanProgress.Completed, session.Progress);
        return session;
    }

    private static List<string> Find(ViewerSession session, TextSearch search)
    {
        session.Search(search);
        session.PumpAll();
        Assert.False(session.SearchInBackground);
        return session.Hits.Select(hit => $"{session.FormatPath(hit.Path)} @{hit.CharacterOffset}+{hit.MatchLength}").ToList();
    }

    private static void SameHits(string text, params string[] needles)
    {
        using var pump = Scan(new PumpOnly(Bytes(text)));
        using var background = Scan(Bytes(text));
        background.InlineSearchTicks = 0;
        foreach (var needle in needles)
        {
            foreach (var search in new[]
            {
                new TextSearch { FindWhat = needle },
                new TextSearch { FindWhat = needle, MatchCase = false },
                new TextSearch { FindWhat = needle.ToUpperInvariant(), MatchCase = false },
                new TextSearch { FindWhat = needle, ElementName = false },
                new TextSearch { FindWhat = needle, ElementName = false, MatchCase = false },
                new TextSearch { FindWhat = needle, MatchCase = false, StartsWith = true },
                new TextSearch { FindWhat = needle, MatchCase = false, EntireCell = true },
            })
            {
                var expected = Find(pump, search);
                var actual = Find(background, search);
                Assert.Equal(expected, actual);
            }
        }
    }

    private static string XmlList(int count, Func<int, string> entry)
    {
        var builder = new StringBuilder("<root>\n");
        for (var i = 1; i <= count; i++)
            builder.Append(entry(i)).Append('\n');
        builder.Append("</root>\n");
        return builder.ToString();
    }

    private static string JsonArray(int count, Func<int, string> item)
    {
        var builder = new StringBuilder("[\n");
        for (var i = 1; i <= count; i++)
        {
            builder.Append(item(i));
            builder.Append(i < count ? ",\n" : "\n");
        }

        builder.Append("]\n");
        return builder.ToString();
    }

    [Fact]
    public void A_search_after_the_scan_finds_the_same_xml_hits_in_the_same_order()
    {
        var text = XmlList(3000, i => i switch
        {
            7 => "<item id=\"7\"><name>first</name><v>needle</v></item>",
            1500 => "<item id=\"1500\"><name>nee<b>dle</b></name><v>x</v></item>",
            1800 => "<item id=\"1800\"><name>nee<!-- c -->dle</name><v>x</v></item>",
            2100 => "<item id=\"2100\"><name>nee<![CDATA[dle]]></name><v>x</v></item>",
            2400 => "<item id=\"2400\"><name>n&#101;edle</name><v>x</v></item>",
            2700 => "<item id=\"2700\" note=\"a needle\"><name>y</name><v>x</v></item>",
            2999 => "<item id=\"2999\"><name>last needle</name><v>x</v></item>",
            2200 => "<item id=\"2200\"><name>NeEdLe</name><v>x</v></item>",
            2300 => "<item id=\"2300\"><name>NEE<b>DLE</b></name><v>x</v></item>",
            2500 => "<item id=\"2500\"><name>N&#69;EDLE</name><v>x</v></item>",
            2600 => "<item id=\"2600\" NOTE=\"A NEEDLE\"><NAME>y</NAME><v>x</v></item>",
            2800 => "<item id=\"2800\"><name>NEE<![CDATA[DLE]]></name><v>x</v></item>",
            // A run where every entry holds a hit: looking ahead pauses there and resumes after it.
            >= 100 and < 1100 => $"<item id=\"{i}\"><name>needle {i}</name><v>x</v></item>",
            _ => $"<item id=\"{i}\"><name>n{i}</name><v>x</v></item>",
        });

        SameHits(text, "needle", "dle", "item", "2999", "zzz", "id", "<", "@id", "Name", "nOtE", "CdAtA");
    }

    [Fact]
    public void A_search_after_the_scan_finds_the_same_json_hits_in_the_same_order()
    {
        var array = JsonArray(3000, i => i switch
        {
            10 => "{\"id\": 10, \"name\": \"needle\"}",
            1500 => "{\"id\": 1500, \"name\": \"nee\\u0064le\"}",
            2000 => "{\"id\": 2000, \"needle\": [1, 2]}",
            2999 => "{\"id\": 2999, \"name\": \"last needle\"}",
            2200 => "{\"id\": 2200, \"name\": \"NeEdLe\"}",
            2300 => "{\"id\": 2300, \"name\": \"NEE\\u0044LE\"}",
            2400 => "{\"id\": 2400, \"NEEDLE\": true}",
            _ => $"{{\"id\": {i}, \"name\": \"n{i}\"}}",
        });
        SameHits(array, "needle", "2999", "id", "zzz", "value", "\\", "VALUE", "Name", "TRUE");

        var keyed = "{\"meta\": {\"n\": 1}, \"rows\": " + array + ", \"tail\": \"needle\"}";
        SameHits(keyed, "needle", "rows", "2000", "zzz");

        var scalars = JsonArray(5000, i => i switch
        {
            4000 => "\"needle\"",
            4500 => "\"NEEDLE\"",
            4600 => "false",
            _ => (i % 10).ToString(),
        });
        SameHits(scalars, "needle", "7", "zzz", "FALSE");
    }

    private static string NestedXml(int count, Func<int, string> order)
    {
        var builder = new StringBuilder("<db>\n <meta><version>3</version></meta>\n <customers>\n");
        for (var i = 1; i <= count; i++)
            builder.Append($"  <customer id=\"c{i}\"><name>Customer {i}</name><city>{(i % 3 == 0 ? "New York" : "Lima")}</city></customer>\n");
        builder.Append(" </customers>\n <orders>\n");
        for (var i = 1; i <= count; i++)
            builder.Append("  ").Append(order(i)).Append('\n');
        builder.Append(" </orders>\n <audit><entry at=\"1\">first</entry><entry at=\"2\">second</entry><entry at=\"3\">third</entry></audit>\n</db>\n");
        return builder.ToString();
    }

    private static string NestedJson(int count, Func<int, string> order)
    {
        var builder = new StringBuilder("{\"meta\": {\"version\": 3}, \"data\": {\"customers\": [\n");
        for (var i = 1; i <= count; i++)
            builder.Append(i == 1 ? "" : ",\n").Append($"{{\"id\": \"c{i}\", \"city\": \"{(i % 3 == 0 ? "New York" : "Lima")}\"}}");
        builder.Append("\n], \"orders\": [\n");
        for (var i = 1; i <= count; i++)
            builder.Append(i == 1 ? "" : ",\n").Append(order(i));
        builder.Append("\n]}, \"tail\": [1, 2, 3]}\n");
        return builder.ToString();
    }

    [Fact]
    public void Lists_below_the_root_and_beside_other_lists_give_the_same_xml_hits()
    {
        var text = NestedXml(2000, i => i switch
        {
            500 => "<order id=\"o500\" note=\"New\tYork\"><lines><line sku=\"a\"/><line sku=\"b\"/></lines><note>x</note></order>",
            900 => "<order id=\"o900\"><lines><line sku=\"上海\"/></lines><note>New&#32;York &#19978;海</note></order>",
            1200 => "<order id=\"o1200\"><lines/><note>New <b>York</b></note></order>",
            1500 => "<order id=\"o1500\"><lines/><note><![CDATA[NEW YORK]]> 上海</note></order>",
            1999 => "<order id=\"o1999\"><lines/><note>last New York</note></order>",
            _ => $"<order id=\"o{i}\"><lines><line sku=\"s{i}\"/></lines><note>plain {i}</note></order>",
        });

        SameHits(text, "New York", "上海", "海", "o1999", "@id", "@sku", "line", "zzz", "w Y", "first", "#text", "Customer 7");
    }

    [Fact]
    public void Lists_below_the_root_and_beside_other_lists_give_the_same_json_hits()
    {
        var text = NestedJson(2000, i => i switch
        {
            500 => "{\"id\": \"o500\", \"note\": \"New\\u0020York\", \"lines\": [1, 2]}",
            900 => "{\"id\": \"o900\", \"note\": \"上海 New York\", \"lines\": []}",
            1200 => "{\"id\": \"o1200\", \"New York\": true, \"lines\": []}",
            1999 => "{\"id\": \"o1999\", \"note\": \"last NEW YORK\", \"lines\": [3]}",
            _ => $"{{\"id\": \"o{i}\", \"note\": \"plain {i}\", \"lines\": [{i}]}}",
        });

        SameHits(text, "New York", "上海", "o1999", "lines", "zzz", "w Y", "value", "3");
    }

    [Fact]
    public void A_search_jumps_through_lists_below_the_root_and_hands_over_hits_before_they_close()
    {
        var xml = Scan(Bytes(NestedXml(5000, i => i == 2500 ? "<order id=\"o\"><note>New York 上海</note></order>" : $"<order id=\"o{i}\"><note>{i}</note></order>")));
        xml.InlineSearchTicks = 0;
        Assert.Equal(["/db/orders/order[2500]/note @9+2"], Find(xml, new TextSearch { FindWhat = "上海" }));
        Assert.True(xml.SearchSkipped > 4000, $"skipped {xml.SearchSkipped}");
        Assert.Equal(["/db/orders/order[2500]/note @4+6"], Find(xml, new TextSearch { FindWhat = "YORK 上", MatchCase = false, ElementName = false }));
        Assert.True(xml.SearchSkipped > 4000, $"skipped {xml.SearchSkipped}");

        var json = Scan(Bytes(NestedJson(5000, i => i == 2500 ? "{\"note\": \"New York 上海\"}" : $"{{\"note\": {i}}}")));
        json.InlineSearchTicks = 0;
        Assert.Equal(["/data/orders[2500]/note @10+2"], Find(json, new TextSearch { FindWhat = "上海" }));
        Assert.True(json.SearchSkipped > 4000, $"skipped {json.SearchSkipped}");
    }

    [Fact]
    public void A_hit_inside_an_open_record_is_shown_before_the_record_closes()
    {
        // Ignoring case leaves no ASCII part of this needle to look for, so every order is read.
        var xml = Scan(Bytes(NestedXml(150_000, i => i == 10 ? "<order id=\"o\"><note>上海</note></order>" : $"<order id=\"o{i}\"><note>{i}</note></order>")));
        xml.InlineSearchTicks = 0;
        xml.Search(new TextSearch { FindWhat = "上海", MatchCase = false });
        var running = false;
        var waited = Stopwatch.StartNew();
        while (xml.Hits.Count == 0 && waited.ElapsedMilliseconds < 20_000)
        {
            xml.WaitForSearch(1);
            running = xml.SearchInBackground;
            xml.Pump(512);
        }

        Assert.True(running, "the hit should arrive while </orders> is still ahead");
        Assert.Equal("/db/orders/order[10]/note", xml.FormatPath(Assert.Single(xml.Hits).Path));
        xml.PumpAll();
        Assert.Equal("/db/orders/order[10]/note", xml.FormatPath(Assert.Single(xml.Hits).Path));
    }

    [Theory]
    [InlineData("New York", false, false, "York")]
    [InlineData("New York", false, true, "New York")]
    [InlineData("@id", false, false, "id")]
    [InlineData("上海 x", false, false, "上海")]
    [InlineData("上海 xy", true, false, "xy")]
    [InlineData("上海", true, false, "")]
    [InlineData("a\tbc", false, true, "a\tbc")]
    [InlineData("a\tbc", true, true, "bc")]
    public void The_probe_is_the_longest_part_every_hit_holds(string needle, bool fold, bool json, string probe) =>
        Assert.Equal(probe, ViewerSession.Probe(needle, fold, json));

    [Fact]
    public void A_sparse_search_jumps_over_entries_without_a_candidate()
    {
        var xml = Scan(Bytes(XmlList(5000, i => i == 4000 ? "<item><v>needle</v></item>" : $"<item><v>{i}</v></item>")));
        xml.InlineSearchTicks = 0;
        Assert.Equal(["/root/item[4000]/v @0+6"], Find(xml, new TextSearch { FindWhat = "needle" }));
        Assert.True(xml.SearchSkipped > 4000, $"skipped {xml.SearchSkipped}");
        Assert.Empty(Find(xml, new TextSearch { FindWhat = "zzz" }));
        Assert.True(xml.SearchSkipped > 4900, $"skipped {xml.SearchSkipped}");

        var json = Scan(Bytes(JsonArray(5000, i => i == 4000 ? "{\"v\": \"needle\"}" : $"{{\"v\": {i}}}")));
        json.InlineSearchTicks = 0;
        Assert.Equal(["/#value[4000]/v @1+6"], Find(json, new TextSearch { FindWhat = "needle" }));
        Assert.True(json.SearchSkipped > 3900, $"skipped {json.SearchSkipped}");
    }

    [Fact]
    public void A_search_that_ignores_case_also_jumps_over_entries()
    {
        var xml = Scan(Bytes(XmlList(5000, i => i == 4000 ? "<item><v>NeEdLe</v></item>" : $"<item><v>{i}</v></item>")));
        xml.InlineSearchTicks = 0;
        Assert.Equal(["/root/item[4000]/v @0+6"], Find(xml, new TextSearch { FindWhat = "needle", MatchCase = false }));
        Assert.True(xml.SearchSkipped > 4000, $"skipped {xml.SearchSkipped}");

        var json = Scan(Bytes(JsonArray(5000, i => i == 4000 ? "{\"v\": \"NEEDLE\"}" : $"{{\"v\": {i}}}")));
        json.InlineSearchTicks = 0;
        Assert.Equal(["/#value[4000]/v @1+6"], Find(json, new TextSearch { FindWhat = "needle", MatchCase = false }));
        Assert.True(json.SearchSkipped > 3900, $"skipped {json.SearchSkipped}");
    }

    [Fact]
    public void Pump_returns_at_once_while_the_search_runs_on_its_own_thread()
    {
        var session = Scan(Bytes(JsonArray(400_000, i => $"{{\"id\": {i}, \"name\": \"n{i}\"}}")));
        session.InlineSearchTicks = 0;
        session.Search(new TextSearch { FindWhat = "n", MatchCase = false });
        var slowest = 0L;
        while (session.SearchInBackground)
        {
            var started = Stopwatch.GetTimestamp();
            session.Pump(512);
            slowest = Math.Max(slowest, Stopwatch.GetTimestamp() - started);
            session.WaitForSearch(5);
        }

        session.Pump(512);
        Assert.False(session.SearchInBackground);
        Assert.Equal(800_000, session.Hits.Count);
        Assert.True(slowest < Stopwatch.Frequency / 10, $"slowest pump {slowest * 1000.0 / Stopwatch.Frequency} ms");
    }

    [Fact]
    public void Cancel_stops_the_search_thread_and_keeps_the_hits_already_shown()
    {
        var session = Scan(Bytes(JsonArray(400_000, i => $"{{\"id\": {i}}}")));
        session.InlineSearchTicks = 0;
        session.Search(new TextSearch { FindWhat = "id" });
        while (session.Hits.Count == 0)
        {
            session.WaitForSearch(5);
            session.Pump(512);
        }

        session.CancelSearch();
        Assert.False(session.SearchInBackground);
        var shown = session.Hits.Count;
        Thread.Sleep(50);
        Assert.False(session.Pump(512));
        Assert.Equal(shown, session.Hits.Count);
        Assert.True(shown < 400_000);
    }

    [Fact]
    public void A_new_search_replaces_the_running_one()
    {
        var session = Scan(Bytes(JsonArray(200_000, i => $"{{\"id\": {i}}}")));
        session.InlineSearchTicks = 0;
        session.Search(new TextSearch { FindWhat = "id" });
        Assert.Equal(["/#value[123456]/id @0+6"], Find(session, new TextSearch { FindWhat = "123456" }));
    }

    [Fact]
    public void A_search_started_while_scanning_stays_on_the_pump()
    {
        var source = new MemoryByteSource();
        source.Append(Encoding.UTF8.GetBytes("<root><item>a</item><item>needle</item>"));
        var session = ViewerSession.Open(source);
        session.Pump(64);
        session.Search(new TextSearch { FindWhat = "needle" });
        Assert.False(session.SearchInBackground);
        source.Append(Encoding.UTF8.GetBytes("<item>needle</item></root>"));
        source.Complete();
        session.PumpAll();
        Assert.Equal(2, session.Hits.Count);
    }
}
