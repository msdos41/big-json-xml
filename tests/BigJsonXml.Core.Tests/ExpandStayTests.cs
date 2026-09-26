using System.Text;

namespace BigJsonXml.Tests;

public class ExpandStayTests
{
    [Fact]
    public void An_opened_node_stays_and_its_table_starts_on_the_next_line()
    {
        var session = SessionFixture.Scan("""
            <root>
              <meta><id>1</id><name>Ada</name></meta>
              <item>
                <UnitName><en>Pressure</en><cs>Tlak</cs></UnitName>
                <Unit>bar</Unit>
              </item>
              <item>
                <UnitName><en>Speed</en><cs>Rychlost</cs></UnitName>
                <Unit>km/h</Unit>
              </item>
            </root>
            """);
        session.SetViewport(0, 0, 1100, 800);
        var meta = DocumentPath.Root.Field("meta");
        session.Expand(meta);
        var record = session.ReadSurface();
        var metaCell = record.Rows.Single(row => row.Kind == SurfaceKind.Field && row.Path.Equals(meta));
        var id = record.Rows.Single(row => row.Kind == SurfaceKind.Field && row.Name == "id");
        Assert.Equal("meta", metaCell.Value);
        Assert.True(metaCell.IsExpanded);
        Assert.True(id.Y >= metaCell.Y + ViewerSession.RowHeight);
        Assert.True(id.Y < metaCell.Y + metaCell.Height);

        var list = DocumentPath.Root.Field("item");
        session.Expand(list);
        var table = session.ReadSurface();
        var listCell = table.Rows.Single(row => row.Kind == SurfaceKind.Field && row.Path.Equals(list));
        var header = table.Rows.Single(row => row.Kind == SurfaceKind.ColumnHeader && row.WidthTable!.Equals(list) && row.Name == "UnitName");
        Assert.Equal("item[2]", listCell.Value);
        Assert.True(header.Y >= listCell.Y + ViewerSession.RowHeight);
        Assert.True(header.Y < listCell.Y + listCell.Height);
        Assert.True(header.X >= listCell.X + ViewerSession.ExpandGutter);

        var unitName = list.WithIndex(1).Field("UnitName");
        var unit = list.WithIndex(1).Field("Unit");
        session.Expand(unitName);
        var nested = session.ReadSurface();
        var nameCell = nested.Rows.Single(row => row.Kind == SurfaceKind.Entry && row.Path.Equals(unitName) && row.IsExpanded);
        var unitCell = nested.Rows.Single(row => row.Kind == SurfaceKind.Entry && row.Path.Equals(unit));
        var en = nested.Rows.Single(row => row.Kind == SurfaceKind.Field && row.Name == "en" && row.Value == "Pressure");
        Assert.Equal("UnitName", nameCell.Value);
        Assert.True(en.Y >= nameCell.Y + ViewerSession.RowHeight);
        Assert.True(en.X >= nameCell.X + ViewerSession.ExpandGutter);
        Assert.Equal(nameCell.Y, unitCell.Y);
        Assert.Equal(nameCell.Height, unitCell.Height);
        Assert.Contains(nested.Rows, row => row.Kind == SurfaceKind.EntryIndex && row.Path.Equals(list.WithIndex(1)) && row.Height == nameCell.Height);
    }

    [Fact]
    public void Expanding_a_long_feature_list_shows_the_node_without_rereading_it()
    {
        const int count = 2000;
        var body = new StringBuilder();
        body.Append("<CadDocument id=\"d\"><Features>");
        for (var i = 0; i < count; i++)
            body.Append(Feature(i));
        var source = new MemoryByteSource();
        var session = ViewerSession.Open(source);
        source.Append(Encoding.UTF8.GetBytes(body.ToString()));
        session.PumpAll();

        var features = DocumentPath.Root.Field("Features");
        session.SetViewport(0, 0, 900, ViewerSession.RowHeight * 8);
        var before = GC.GetAllocatedBytesForCurrentThread();
        session.Expand(features);
        var frame = session.ReadSurface();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        var cell = frame.Rows.Single(row => row.Kind == SurfaceKind.Field && row.Path.Equals(features));
        Assert.Equal($"Feature[{count}]", cell.Value);
        Assert.True(cell.IsExpanded);
        Assert.Contains(frame.Rows, row => row.Kind == SurfaceKind.ColumnHeader && row.Name == "@id" && row.Y >= cell.Y + ViewerSession.RowHeight);
        Assert.InRange(frame.Rows.Count(row => row.Kind == SurfaceKind.Entry), 1, 24);
        Assert.True(allocated < 1_000_000, $"expand allocated {allocated} bytes");

        session.Collapse(features);
        var reopenBefore = GC.GetAllocatedBytesForCurrentThread();
        session.Expand(features);
        var reopened = session.ReadSurface();
        var reopenedBytes = GC.GetAllocatedBytesForCurrentThread() - reopenBefore;
        Assert.Equal($"Feature[{count}]", reopened.Rows.Single(row => row.Kind == SurfaceKind.Field && row.Path.Equals(features)).Value);
        Assert.True(reopenedBytes < 1_000_000, $"reopen allocated {reopenedBytes} bytes");

        session.SetViewport(0, reopened.ContentHeight - (ViewerSession.RowHeight * 3), 900, ViewerSession.RowHeight * 3);
        var bottom = session.ReadSurface();
        Assert.Contains(bottom.Rows, row => row.Kind == SurfaceKind.Entry && row.Name == "@id" && row.Value == $"f{count - 1}");

        source.Append(Encoding.UTF8.GetBytes(Feature(count) + Feature(count + 1)));
        session.PumpAll();
        var grown = session.ReadSurface();
        Assert.Equal($"Feature[{count + 2}]", grown.Rows.Single(row => row.Kind == SurfaceKind.Field && row.Path.Equals(features)).Value);
        session.SetViewport(0, grown.ContentHeight - (ViewerSession.RowHeight * 3), 900, ViewerSession.RowHeight * 3);
        Assert.Contains(session.ReadSurface().Rows, row => row.Kind == SurfaceKind.Entry && row.Value == $"f{count + 1}");
    }

    private static string Feature(int index) =>
        $"<Feature id=\"f{index}\" type=\"line\" visible><Style stroke=\"1\"></Style><Geometry><Start x=\"0\" y=\"1\"></Start></Geometry></Feature>";
}
