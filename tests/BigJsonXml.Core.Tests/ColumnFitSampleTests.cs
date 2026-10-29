using System.Text;

namespace BigJsonXml.Tests;

/// <summary>
/// A list column fits from a bounded sample of its entries. A few long cells do not
/// widen it, and no fitted column grows past the column cap (ADR 0056).
/// </summary>
public class ColumnFitSampleTests
{
    private static readonly double Cap = ViewerSession.FitColumnCharacters * ViewerSession.Advance;

    [Fact]
    public void A_rare_long_cell_does_not_widen_the_column()
    {
        var xml = new StringBuilder("<root>");
        for (var i = 0; i < 100; i++)
            xml.Append("<item><t>").Append(i == 50 ? new string('w', 300) : "short").Append("</t><z>1</z></item>");
        xml.Append("</root>");
        var session = SessionFixture.Scan(xml.ToString());
        var list = DocumentPath.Root.Field("item");

        session.Fit(WidthAxis.ListColumn, list, "t");

        Assert.Equal("short".Length * ViewerSession.Advance, Header(session, list, "t").Width);
        var cell = Cell(session, list, "t", 51);
        Assert.True(cell.IsCutOff);
    }

    [Fact]
    public void A_common_long_value_still_widens_the_column()
    {
        var common = new string('c', 40);
        var xml = new StringBuilder("<root>");
        for (var i = 0; i < 100; i++)
            xml.Append("<item>").Append(i % 10 == 3 ? common : "short").Append("</item>");
        xml.Append("</root>");
        var session = SessionFixture.Scan(xml.ToString());
        var list = DocumentPath.Root.Field("item");

        session.Fit(WidthAxis.ListColumn, list, "#text");

        Assert.Equal(common.Length * ViewerSession.Advance, Header(session, list, "#text").Width);
    }

    [Fact]
    public void A_short_list_still_fits_its_longest_cell()
    {
        var longest = new string('L', 45);
        var session = SessionFixture.Scan($"<root><item>a</item><item>{longest}</item><item>b</item></root>");
        var list = DocumentPath.Root.Field("item");

        session.Fit(WidthAxis.ListColumn, list, "#text");

        Assert.Equal(longest.Length * ViewerSession.Advance, Header(session, list, "#text").Width);
    }

    [Fact]
    public void Long_cells_stop_at_the_column_cap()
    {
        var xml = new StringBuilder("<root>");
        for (var i = 0; i < 50; i++)
            xml.Append("<item>").Append(new string('x', 100 + i)).Append("</item>");
        xml.Append("</root>");
        var session = SessionFixture.Scan(xml.ToString());
        var list = DocumentPath.Root.Field("item");

        session.Fit(WidthAxis.ListColumn, list, "#text");

        Assert.Equal(Cap, Header(session, list, "#text").Width);
        Assert.True(Cell(session, list, "#text", 1).IsCutOff);
    }

    [Fact]
    public void The_header_fits_and_an_absurd_header_stops_at_the_cap()
    {
        const string name = "a_fairly_long_header_name";
        var absurd = new string('h', 200);
        var session = SessionFixture.Scan(
            $"<root><item><{name}>1</{name}><{absurd}>2</{absurd}></item><item><{name}>3</{name}><{absurd}>4</{absurd}></item></root>");
        var list = DocumentPath.Root.Field("item");

        session.Fit(WidthAxis.ListColumn, list, name);
        session.Fit(WidthAxis.ListColumn, list, absurd);

        Assert.Equal(name.Length * ViewerSession.Advance, Header(session, list, name).Width);
        Assert.Equal(Cap, Header(session, list, absurd).Width);
    }

    [Fact]
    public void A_sparse_column_fits_the_cells_it_has()
    {
        var value = new string('s', 30);
        var xml = new StringBuilder("<root>");
        for (var i = 0; i < 200; i++)
            xml.Append(i % 25 == 0 ? $"<item><k>{value}</k><n>1</n></item>" : "<item><n>1</n></item>");
        xml.Append("</root>");
        var session = SessionFixture.Scan(xml.ToString());
        var list = DocumentPath.Root.Field("item");

        session.Fit(WidthAxis.ListColumn, list, "k");

        Assert.Equal(value.Length * ViewerSession.Advance, Header(session, list, "k").Width);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_huge_list_reads_a_bounded_sample(bool json)
    {
        const int count = 200_000;
        var text = new StringBuilder(json ? "[" : "<root>");
        for (var i = 0; i < count; i++)
        {
            if (json)
                text.Append(i == 0 ? "" : ",").Append("{\"v\":\"value").Append(i % 7).Append("\",\"z\":1}");
            else
                text.Append("<item><v>value").Append(i % 7).Append("</v><z>1</z></item>");
        }

        text.Append(json ? "]" : "</root>");
        var session = SessionFixture.Scan(text.ToString());
        var list = json ? DocumentPath.Root.Field("#value") : DocumentPath.Root.Field("item");

        session.Fit(WidthAxis.ListColumn, list, "v");

        Assert.InRange(session.LastFitReads, 1, ViewerSession.FitSampleEntries);
        var shown = json ? "\"value0\"" : "value0";
        Assert.Equal(shown.Length * ViewerSession.Advance, Header(session, list, "v").Width);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Non_ascii_cells_are_measured_from_the_bytes(bool json)
    {
        const string chinese = "中文数据表格测试中文数据表格测试";
        const string mixed = "ab中😀";
        var text = new StringBuilder(json ? "[" : "<root>");
        for (var i = 0; i < 40; i++)
        {
            var value = i % 2 == 0 ? chinese : mixed;
            if (json)
                text.Append(i == 0 ? "" : ",").Append("{\"v\":\"").Append(value).Append("\",\"w\":\"").Append(mixed).Append("\"}");
            else
                text.Append("<item w=\"").Append(mixed).Append("\"><v>").Append(value).Append("</v></item>");
        }

        text.Append(json ? "]" : "</root>");
        var session = SessionFixture.Scan(text.ToString());
        var list = json ? DocumentPath.Root.Field("#value") : DocumentPath.Root.Field("item");
        var quotes = json ? 2 : 0;

        session.Fit(WidthAxis.ListColumn, list, "v");
        Assert.Equal(0, session.LastFitSlowReads);
        Assert.Equal((chinese.Length + quotes) * ViewerSession.Advance, Header(session, list, "v").Width);

        var attribute = json ? "w" : "@w";
        session.Fit(WidthAxis.ListColumn, list, attribute);
        Assert.Equal(0, session.LastFitSlowReads);
        Assert.Equal(Math.Max(ViewerSession.MinWidth, (mixed.Length + quotes) * ViewerSession.Advance), Header(session, list, attribute).Width);
    }

    private static SurfaceRow Header(ViewerSession session, DocumentPath list, string column)
    {
        session.Expand(list);
        session.SetViewport(0, 0, 20_000, 600);
        return session.ReadSurface().Rows.Single(row =>
            row.Kind == SurfaceKind.ColumnHeader && row.Name == column && row.WidthTable!.Equals(list));
    }

    private static SurfaceRow Cell(ViewerSession session, DocumentPath list, string column, int entry)
    {
        session.Expand(list);
        session.SetViewport(0, 0, 20_000, 4_000);
        var frame = session.ReadSurface();
        var index = frame.Rows.Single(row => row.Kind == SurfaceKind.EntryIndex && row.Name == entry.ToString(System.Globalization.CultureInfo.InvariantCulture) && row.Path.Equals(list.WithIndex(entry)));
        return frame.Rows.Single(row => row.Kind == SurfaceKind.Entry && row.Name == column && row.Y == index.Y);
    }
}
