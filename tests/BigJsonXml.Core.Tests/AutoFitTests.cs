using System.Text;

namespace BigJsonXml.Tests;

/// <summary>
/// A drawn table fits its columns once, from a small sample, after the scan (ADR 0057).
/// </summary>
public class AutoFitTests
{
    [Fact]
    public void A_drawn_table_fits_every_column_once()
    {
        var session = Items(100, i => $"<item id=\"{i}\"><name>{(i == 7 ? new string('x', 300) : "name-" + i)}</name><city>Shanghai</city><z>1</z></item>");
        var list = DocumentPath.Root.Field("item");
        session.AutoFitColumns = true;
        session.Expand(list);
        Assert.Equal(ViewerSession.DefaultWidth, Header(session, list, "name").Width);

        Assert.True(session.AdvanceAutoFit(10_000));

        Assert.Equal(ViewerSession.MinWidth, Header(session, list, "@id").Width);
        Assert.Equal("name-99".Length * ViewerSession.Advance, Header(session, list, "name").Width);
        Assert.Equal("Shanghai".Length * ViewerSession.Advance, Header(session, list, "city").Width);
        Assert.False(session.AutoFitPending);
        Assert.False(session.AdvanceAutoFit(10_000));
    }

    [Fact]
    public void A_column_set_by_hand_keeps_its_width()
    {
        var session = Items(20, i => $"<item><a>aaaaaaaaaaaa</a><b>bbbbbbbbbbbb</b><z>1</z></item>");
        var list = DocumentPath.Root.Field("item");
        session.AutoFitColumns = true;
        session.Expand(list);
        Show(session);
        session.SetColumnWidth(list, "a", 300);

        Assert.True(session.AdvanceAutoFit(10_000));

        Assert.Equal(300, Header(session, list, "a").Width);
        Assert.Equal(12 * ViewerSession.Advance, Header(session, list, "b").Width);
    }

    [Fact]
    public void An_opened_inner_table_fits_after_the_outer_one()
    {
        var session = Items(5, i => $"<item><id>{i}</id><lines><line><sku>SKU-000{i}</sku><qty>1</qty></line><line><sku>S</sku><qty>2</qty></line></lines></item>");
        var list = DocumentPath.Root.Field("item");
        session.AutoFitColumns = true;
        session.Expand(list);
        Show(session);
        session.AdvanceAutoFit(10_000);
        var lines = list.WithIndex(1).Field("lines");
        session.Expand(lines);
        Show(session);
        var inner = InnerList(session, lines);

        Assert.True(session.AdvanceAutoFit(10_000));

        Assert.Equal("SKU-0001".Length * ViewerSession.Advance, Header(session, inner, "sku").Width);
    }

    [Fact]
    public void Nothing_fits_while_the_scan_runs_or_when_switched_off()
    {
        var source = new MemoryByteSource();
        source.Append("<root><box><item><a>aaaaaaaaaa</a><z>1</z></item><item><a>a</a><z>1</z></item></box><more>");
        var session = ViewerSession.Open(source);
        session.AutoFitColumns = true;
        for (var i = 0; i < 20; i++)
            session.Pump(512);
        Assert.Equal(ScanProgress.Scanning, session.Progress);
        var list = DocumentPath.Root.Field("box").Field("item");
        session.Expand(DocumentPath.Root.Field("box"));
        session.Expand(list);
        var shown = Show(session);
        Assert.True(session.AutoFitPending, string.Join(" | ", shown.Rows.Select(r => $"{r.Kind}:{r.Name}")));
        Assert.False(session.AdvanceAutoFit(10_000));
        source.Append("</more></root>");
        source.Complete();
        session.PumpAll();
        Assert.True(session.AdvanceAutoFit(10_000));
        Assert.Equal(10 * ViewerSession.Advance, Header(session, list, "a").Width);

        var off = Items(3, _ => "<item><a>aaaaaaaaaa</a><z>1</z></item>");
        var items = DocumentPath.Root.Field("item");
        off.Expand(items);
        Show(off);
        Assert.False(off.AutoFitPending);
        Assert.False(off.AdvanceAutoFit(10_000));
        Assert.Equal(ViewerSession.DefaultWidth, Header(off, items, "a").Width);
    }

    [Theory]
    [InlineData(4, ViewerSession.AutoFitMaxEntries)]
    [InlineData(400, ViewerSession.AutoFitCells / 400)]
    [InlineData(4000, ViewerSession.AutoFitMinEntries)]
    public void The_sample_is_bounded_by_cells(int columns, int entries)
    {
        var row = new StringBuilder("<item>");
        for (var c = 0; c < columns; c++)
            row.Append("<c").Append(c).Append(">v</c").Append(c).Append('>');
        row.Append("</item>");
        var text = row.ToString();
        var session = Items(20_000 / Math.Max(1, columns / 4), _ => text);
        var list = DocumentPath.Root.Field("item");
        session.AutoFitColumns = true;
        session.Expand(list);
        Show(session);

        Assert.True(session.AdvanceAutoFit(60_000));

        Assert.Equal(Math.Min(entries, 20_000 / Math.Max(1, columns / 4)), session.LastAutoFitReads);
    }

    private static ViewerSession Items(int count, Func<int, string> item)
    {
        var xml = new StringBuilder("<root>");
        for (var i = 1; i <= count; i++)
            xml.Append(item(i));
        xml.Append("</root>");
        return SessionFixture.Scan(xml.ToString());
    }

    private static DocumentPath InnerList(ViewerSession session, DocumentPath lines)
    {
        var frame = Show(session);
        return frame.Rows.First(row => row.Kind == SurfaceKind.ColumnHeader && row.Name == "sku").WidthTable!;
    }

    private static SurfaceFrame Show(ViewerSession session)
    {
        session.SetViewport(0, 0, 20_000, 2_000);
        return session.ReadSurface();
    }

    private static SurfaceRow Header(ViewerSession session, DocumentPath list, string column) =>
        Show(session).Rows.First(row => row.Kind == SurfaceKind.ColumnHeader && row.Name == column && row.WidthTable!.Equals(list));
}
