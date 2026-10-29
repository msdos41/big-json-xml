using System.Diagnostics;
using System.Text;

namespace BigJsonXml.Tests;

/// <summary>
/// Pins column fit and the slice bound the half-time work must keep.
/// Outline and hits stay pinned in <see cref="OutlineLockTests"/>.
/// </summary>
public class HalfTimePinTests
{
    [Fact]
    public void A_long_list_fits_cells_from_its_far_end()
    {
        const int count = 4_000;
        const string widest = "widest-cell-here";
        var xml = new StringBuilder();
        xml.Append("<root>");
        for (var i = 0; i < count; i++)
        {
            var text = i >= count - (count / 10) ? widest : i == 20 ? "middle" : "short";
            xml.Append("<item>").Append(text).Append("</item>");
        }

        xml.Append("</root>");
        var session = SessionFixture.Scan(xml.ToString());
        var list = DocumentPath.Root.Field("item");
        session.Expand(list);

        session.Fit(WidthAxis.ListColumn, list, "#text");
        var header = Header(session, list, "#text");

        Assert.Equal(widest.Length * ViewerSession.Advance, header.Width);
        Assert.Equal(count, session.ReadList(list, 0, 1).EntryCount);
        Assert.Equal(widest, session.ReadList(list, count - 1, 1).Entries[0].Cells.Single(cell => cell.Name == "#text").Text);
    }

    [Fact]
    public void A_list_column_stops_growing_at_the_column_cap()
    {
        var capped = new string('a', ViewerSession.FitCharacterCap);
        var longer = new string('b', ViewerSession.FitCharacterCap + 300);
        var session = SessionFixture.Scan($"<root><item>{capped}</item><item>{longer}</item><item>z</item></root>");
        var list = DocumentPath.Root.Field("item");
        session.Expand(list);

        session.Fit(WidthAxis.ListColumn, list, "#text");
        var header = Header(session, list, "#text");

        Assert.Equal(ViewerSession.FitColumnCharacters * ViewerSession.Advance, header.Width);
    }

    [Fact]
    public void A_default_json_search_keeps_both_matches_in_one_value()
    {
        const string json = """{"items":[{"name":"RTM=123 and 123"}]}""";
        var session = SessionFixture.Scan(json);
        session.Search(new TextSearch { FindWhat = "123" });
        session.PumpAll();

        Assert.Equal(
            ["/items[1]/name@5+3", "/items[1]/name@13+3"],
            session.Hits.Select(hit => $"{session.FormatPath(hit.Path)}@{hit.CharacterOffset}+{hit.MatchLength}").ToArray());
    }

    [Fact]
    public void A_default_json_search_reads_later_array_items()
    {
        const string json = """
            {"items":[{"name":"RTM=123 and 123"},{"name":"E10 and 123","code":"aaa","tags":["ax"]}]}
            """;
        var session = SessionFixture.Scan(json);

        Search(session, "123",
            "/items[1]/name@5+3",
            "/items[1]/name@13+3",
            "/items[2]/name@9+3");
        Search(session, "name",
            "/items[1]/name@0+4",
            "/items[2]/name@0+4");
        Search(session, "aa",
            "/items[2]/code@1+2",
            "/items[2]/code@2+2");
        Search(session, "ax", "/items[2]/tags[1]/#value@1+2");
        Search(session, "tags", "/items[2]/tags@0+4");

        var nested = SessionFixture.Scan("[[1],[2]]");
        Search(nested, "2", "/#value[2]/#value[1]/#value@0+1");
    }

    [Fact]
    public void A_default_json_search_defers_a_fancy_later_item()
    {
        const string json = """{"items":[{"name":"abc"},{"name":"café and 123"}]}""";
        var session = SessionFixture.Scan(json);
        Search(session, "123", "/items[2]/name@10+3");
    }

    [Fact]
    public void A_default_json_search_drops_a_repeated_key_in_a_later_item()
    {
        const string json = """{"rows":[{"a":"1"},{"a":"1","a":"2"}]}""";
        var session = SessionFixture.Scan(json);
        Search(session, "2");
        Search(session, "1", "/rows[1]/a@1+1");
    }

    [Fact]
    public void A_default_json_search_reads_a_repeated_shape()
    {
        const string json = """
            {"items":[{"name":"a","profile":{"email":"a@b"},"tags":["aa"],"rows":[{"id":"q"}]},{"name":"b","profile":{"email":"E10x"},"tags":["aa","ax"],"rows":[{"id":"E10"}]},{"name":"c","profile":{"email":"E10y"},"tags":["aa","ax"],"rows":[{"id":"E10"}]}]}
            """;
        var session = SessionFixture.Scan(json);
        Search(session, "E10",
            "/items[2]/profile/email@1+3",
            "/items[2]/rows[1]/id@1+3",
            "/items[3]/profile/email@1+3",
            "/items[3]/rows[1]/id@1+3");
        Search(session, "ax",
            "/items[2]/tags[2]/#value@1+2",
            "/items[3]/tags[2]/#value@1+2");
        Search(session, "name",
            "/items[1]/name@0+4",
            "/items[2]/name@0+4",
            "/items[3]/name@0+4");

        var repeated = SessionFixture.Scan("""{"rows":[{"a":"1"},{"a":"1"},{"a":"1","a":"2"}]}""");
        Search(repeated, "2");
        Search(repeated, "1", "/rows[1]/a@1+1", "/rows[2]/a@1+1");

        var nested = SessionFixture.Scan("""
            {"rows":[{"profile":{"a":"1"}},{"profile":{"a":"1"}},{"profile":{"b":"1","b":"2"}}]}
            """);
        Search(nested, "2");
        Search(nested, "1", "/rows[1]/profile/a@1+1", "/rows[2]/profile/a@1+1");
    }

    [Fact]
    public void A_default_xml_search_keeps_a_later_empty_sibling()
    {
        const string xml = """
            <root>
            <row id="aa"/>
            <row id="ab"/>
            <row id="a/b"/>
            <row id="D70"/>
            <row id="ac"/>
            <row id="D70z"/>
            <row>D70</row>
            <group>
            <row id="q"/>
            <row id="r"/>
            <row id="s"/>
            <row id="D70"/>
            </group>
            <row note="D&amp;70"/>
            <D70 id="z"/>
            </root>
            """;
        var session = SessionFixture.Scan(xml);
        Search(session, "D70",
            "/root/row[4]/@id@0+3",
            "/root/row[6]/@id@0+3",
            "/root/row[7]/#text@0+3",
            "/root/group/row[4]/@id@0+3",
            "/root/D70@0+3");
        Search(session, "D&7", "/root/row[8]/@note@0+3");
        Search(session, "amp");

        const int before = 20_000;
        var wide = new StringBuilder();
        wide.Append("<root>");
        for (var i = 0; i < before; i++)
            wide.Append("<row id=\"aa\"/>");
        wide.Append("<row id=\"D70\"/>");
        wide.Append("<row id=\"aa\"/>");
        wide.Append("</root>");
        Search(SessionFixture.Scan(wide.ToString()), "D70", $"/root/row[{before + 1}]/@id@0+3");
    }

    [Fact]
    public void One_scan_slice_and_one_column_fit_slice_stay_short()
    {
        const int count = 20_000;
        var xml = new StringBuilder();
        xml.Append("<root>");
        for (var i = 0; i < count; i++)
            xml.Append("<item>short</item>");
        xml.Append("</root>");
        var source = new MemoryByteSource();
        source.Append(xml.ToString());
        source.Complete();
        var session = ViewerSession.Open(source);

        var scan = Stopwatch.StartNew();
        Assert.True(session.Pump(256));
        scan.Stop();
        Assert.True(scan.ElapsedMilliseconds < 100, $"One scan slice took {scan.ElapsedMilliseconds} ms.");

        session.PumpAll();
        var list = DocumentPath.Root.Field("item");
        session.Expand(list);
        session.BeginFit(WidthAxis.ListColumn, list, "#text");
        var fit = Stopwatch.StartNew();
        var step = session.AdvanceFit();
        fit.Stop();

        Assert.NotEqual(FitAdvance.Idle, step);
        Assert.True(fit.ElapsedMilliseconds < 100, $"One column-fit slice took {fit.ElapsedMilliseconds} ms.");
    }

    private static void Search(ViewerSession session, string find, params string[] hits)
    {
        session.Search(new TextSearch { FindWhat = find });
        session.PumpAll();
        Assert.Equal(
            hits,
            session.Hits.Select(hit => $"{session.FormatPath(hit.Path)}@{hit.CharacterOffset}+{hit.MatchLength}").ToArray());
    }

    private static SurfaceRow Header(ViewerSession session, DocumentPath list, string column)
    {
        session.SetViewport(0, 0, 800, 600);
        return session.ReadSurface().Rows.Single(row =>
            row.Kind == SurfaceKind.ColumnHeader && row.Name == column && row.WidthTable!.Equals(list));
    }
}
