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
    public void A_long_list_of_short_cells_fits_from_every_entry()
    {
        const int count = 4_000;
        const string widest = "widest-cell-here";
        var xml = new StringBuilder();
        xml.Append("<root>");
        for (var i = 0; i < count; i++)
        {
            var text = i == count - 1 ? widest : i == 20 ? "middle" : "short";
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
    public void A_list_column_stops_growing_at_five_hundred_characters()
    {
        var capped = new string('a', ViewerSession.FitCharacterCap);
        var longer = new string('b', ViewerSession.FitCharacterCap + 300);
        var session = SessionFixture.Scan($"<root><item>{capped}</item><item>{longer}</item><item>z</item></root>");
        var list = DocumentPath.Root.Field("item");
        session.Expand(list);

        session.Fit(WidthAxis.ListColumn, list, "#text");
        var header = Header(session, list, "#text");

        Assert.Equal(ViewerSession.FitCharacterCap * ViewerSession.Advance, header.Width);
        Assert.Equal(capped, session.ReadList(list, 0, 1).Entries[0].Cells.Single(cell => cell.Name == "#text").Text);
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

    private static SurfaceRow Header(ViewerSession session, DocumentPath list, string column)
    {
        session.SetViewport(0, 0, 800, 600);
        return session.ReadSurface().Rows.Single(row =>
            row.Kind == SurfaceKind.ColumnHeader && row.Name == column && row.WidthTable!.Equals(list));
    }
}
