namespace BigJsonXml.Tests;

public class ExpandTests
{
    [Fact]
    public void A_nested_record_expands_inside_the_cell_and_collapses()
    {
        var session = SessionFixture.Scan("""
            <root>
              <meta><id>1</id><name>Ada</name></meta>
              <note><id>2</id></note>
              <plain>hi</plain>
            </root>
            """);

        Assert.True(session.Field("meta").CanExpand);
        Assert.True(session.Field("note").CanExpand);
        Assert.False(session.Field("plain").CanExpand);

        var meta = DocumentPath.Root.Field("meta");
        var note = DocumentPath.Root.Field("note");
        session.Expand(meta);
        session.Expand(note);

        var metaFields = session.ReadRecord(meta).Fields;
        Assert.Equal(["id", "name"], metaFields.Select(field => field.Name).ToArray());
        Assert.Equal("Ada", metaFields[1].Text);
        Assert.Equal(["id"], session.ReadRecord(note).Fields.Select(field => field.Name).ToArray());

        session.SetViewport(0, 0, 900, 800);
        var open = session.ReadSurface();
        Assert.Equal(1, open.ScrollSurfaces);
        Assert.Contains(open.Rows, row => row.Name == "name" && row.Value == "Ada");
        Assert.True(open.ContentHeight > ViewerSession.RowHeight * 3);

        session.Collapse(meta);
        var closed = session.ReadSurface();
        Assert.DoesNotContain(closed.Rows, row => row.Name == "name");
        Assert.Contains(closed.Rows, row => row.Name == "id" && row.Value == "2");
        Assert.True(closed.ContentHeight < open.ContentHeight);
    }

    [Fact]
    public void Expanding_a_list_appends_columns_and_reads_a_slice()
    {
        var session = SessionFixture.Scan("""
            <root>
              <item id="1"><a>1</a></item>
              <item><b>2</b><a>3</a></item>
              <item id="4"><a>5</a><c>6</c></item>
            </root>
            """);
        var list = DocumentPath.Root.Field("item");
        session.Expand(list);

        var window = session.ReadList(list, 0, 2);

        Assert.Equal(["@id", "a", "b", "c"], window.Columns.ToArray());
        Assert.Equal(3, window.EntryCount);
        Assert.Equal("1", window.Entries[0].Cells[0].Text);
        Assert.Equal("1", window.Entries[0].Cells[1].Text);
        Assert.Equal("", window.Entries[0].Cells[2].Text);
        Assert.Equal("3", window.Entries[1].Cells[1].Text);
        Assert.Equal("2", window.Entries[1].Cells[2].Text);
        Assert.Equal(FieldKind.Scalar, window.Entries[0].Cells[1].Kind);
    }

    [Fact]
    public void A_nested_record_inside_an_entry_can_expand()
    {
        var session = SessionFixture.Scan("""
            <root>
              <item><meta><x>1</x></meta></item>
              <item><meta><x>2</x></meta></item>
            </root>
            """);
        var list = DocumentPath.Root.Field("item");
        session.Expand(list);
        var meta = list.WithIndex(1).Field("meta");
        var cells = session.ReadList(list, 0, 1).Entries[0].Cells;
        Assert.Equal(FieldKind.Record, cells.Single(cell => cell.Name == "meta").Kind);
        Assert.True(cells.Single(cell => cell.Name == "meta").CanExpand);

        session.Expand(meta);
        Assert.Equal("1", session.ReadRecord(meta).Fields.Single(field => field.Name == "x").Text);

        session.Collapse(list);
        Assert.Equal("item[2]", session.Field("item").ListLabel);
        Assert.False(session.Field("item").IsExpanded);

        session.Expand(list);
        session.Expand(meta);
        session.SetViewport(0, 0, 900, 800);
        Assert.Contains(session.ReadSurface().Rows, row => row.Name == "x" && row.Value == "1");
    }

    [Fact]
    public void Only_the_visible_slice_of_a_long_list_is_on_the_surface()
    {
        var xml = "<root>" + string.Concat(Enumerable.Range(0, 500).Select(i => $"<item>{i}</item>")) + "</root>";
        var session = SessionFixture.Scan(xml);
        var list = DocumentPath.Root.Field("item");
        session.Expand(list);
        session.SetViewport(0, 0, 800, ViewerSession.RowHeight * 4);

        var rows = session.ReadSurface().Rows;

        Assert.InRange(rows.Count(row => row.Kind == SurfaceKind.Entry), 1, 8);
        Assert.Equal(1, session.ReadSurface().ScrollSurfaces);
        var slice = session.ReadList(list, 480, 5);
        Assert.Equal(5, slice.Entries.Count);
        Assert.Equal("480", slice.Entries[0].Cells.Single(cell => cell.Name == "#text").Text);
    }

    [Fact]
    public void Refreshing_a_closed_record_does_not_reread_its_list()
    {
        var items = string.Concat(Enumerable.Range(0, 2000).Select(i =>
            $"<Feature id=\"{i}\" visible><Style stroke=\"1\"></Style></Feature>"));
        var session = SessionFixture.Scan(
            $"<CadDocument id=\"d\" name=\"n\" units=\"mm\"><Features>{items}</Features></CadDocument>");
        var features = DocumentPath.Root.Field("Features");
        session.Expand(features);
        session.Expand(features.Field("Feature"));
        session.SetViewport(0, 0, 800, ViewerSession.RowHeight * 8);
        _ = session.ReadSurface();

        var watch = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < 20; i++)
            _ = session.ReadSurface();
        watch.Stop();

        var rows = session.ReadSurface().Rows;
        Assert.InRange(rows.Count(row => row.Kind == SurfaceKind.Entry), 1, 20);
        Assert.Equal(2000, session.ReadRecord(features).Fields.Single(field => field.Name == "Feature").ListCount);
        Assert.True(watch.ElapsedMilliseconds < 500, $"refresh took {watch.ElapsedMilliseconds} ms");
    }
}
