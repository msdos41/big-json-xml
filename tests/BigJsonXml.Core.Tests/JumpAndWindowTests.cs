namespace BigJsonXml.Tests;

public class JumpAndWindowTests
{
    [Fact]
    public void An_expanded_list_grows_while_it_is_scanned_and_can_be_reopened()
    {
        var source = new MemoryByteSource();
        var session = ViewerSession.Open(source);
        source.Append("<root><item>0</item><item>1</item>");
        session.PumpAll();
        var list = DocumentPath.Root.Field("item");
        session.Expand(list);
        Assert.Equal(2, session.ReadList(list, 0, 2).EntryCount);
        Assert.Equal("0", TextAt(session, list, 1));

        source.Append("<item>2</item><item>3</item>");
        session.PumpAll();
        Assert.Equal(4, session.ReadList(list, 0, 1).EntryCount);
        Assert.Equal("0", TextAt(session, list, 1));
        Assert.Equal("3", TextAt(session, list, 4));

        session.Collapse(list);
        session.Expand(list);
        Assert.Equal("3", TextAt(session, list, 4));
        source.Append("</root>");
        source.Complete();
        session.PumpAll();
        Assert.Equal(ScanProgress.Completed, session.Progress);
    }

    [Fact]
    public void Reading_the_end_of_a_long_list_does_not_allocate_an_object_per_earlier_entry()
    {
        const int count = 20_000;
        var xml = "<root>" + string.Concat(Enumerable.Range(0, count).Select(i => i == count - 1 ? "<i>end</i>" : "<i>v</i>")) + "</root>";
        var session = SessionFixture.Scan(xml);
        var list = DocumentPath.Root.Field("i");
        session.Expand(list);
        _ = session.ReadList(list, 0, 1);

        var before = GC.GetAllocatedBytesForCurrentThread();
        var slice = session.ReadList(list, count - 5, 5);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal("end", slice.Entries[^1].Cells.Single(cell => cell.Name == "#text").Text);
        Assert.True(allocated < 400_000, $"Reading the tail allocated {allocated} bytes.");
    }

    [Fact]
    public void A_scalar_window_follows_the_cell_width_until_the_full_value_is_opened()
    {
        var body = new string('a', 180) + "NEEDLE" + new string('b', 80);
        var session = SessionFixture.Scan($"<root><note>{body}</note></root>");
        var note = DocumentPath.Root.Field("note");
        var field = session.ReadScalar(note);

        Assert.Equal(25, field.Text!.Length);
        Assert.True(field.IsTruncated);
        Assert.True(field.CanExpandFullValue);
        Assert.False(field.IsFullValueExpanded);
        Assert.StartsWith(new string('a', 25), field.Text, StringComparison.Ordinal);
        Assert.Equal(body[200..250], session.ReadFullValueSlice(note, 200, 50));

        var huge = new string('z', 100_000);
        var hugeSession = SessionFixture.Scan($"<root><note>{huge}</note></root>");
        var before = GC.GetAllocatedBytesForCurrentThread();
        var slice = hugeSession.ReadFullValueSlice(DocumentPath.Root.Field("note"), 80_000, 20);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(20, slice.Length);
        Assert.Equal(new string('z', 20), slice);
        Assert.True(allocated < 50_000, $"Slice allocated {allocated} bytes.");

        session.ExpandFullValue(note);
        Assert.True(session.ReadScalar(note).IsFullValueExpanded);
        session.SetViewport(0, 0, 800, 600);
        var openHeight = session.ReadSurface().ContentHeight;
        Assert.True(openHeight > ViewerSession.RowHeight * 2);

        session.CollapseFullValue(note);
        var closed = session.ReadScalar(note);
        Assert.False(closed.IsFullValueExpanded);
        Assert.Equal(0, closed.WindowStart);
        Assert.Equal(25, closed.Text!.Length);
        Assert.True(session.ReadSurface().ContentHeight < openHeight);
    }

    [Fact]
    public void Expanding_a_list_does_not_expand_full_values_inside_it()
    {
        var body = new string('x', 240);
        var session = SessionFixture.Scan($"<root><item><t>{body}</t></item><item><t>{body}</t></item></root>");
        var list = DocumentPath.Root.Field("item");
        session.Expand(list);

        var cell = session.ReadList(list, 0, 1).Entries[0].Cells.Single(item => item.Name == "t");

        Assert.False(cell.IsFullValueExpanded);
        Assert.True(cell.CanExpandFullValue);
        Assert.Equal(25, cell.Text!.Length);
    }

    [Fact]
    public void A_short_scalar_has_no_full_value_control()
    {
        var session = SessionFixture.Scan("<root><note>short</note></root>");
        var field = session.ReadScalar(DocumentPath.Root.Field("note"));

        Assert.Equal("short", field.Text);
        Assert.False(field.CanExpandFullValue);
        Assert.False(field.IsTruncated);
    }

    private static string TextAt(ViewerSession session, DocumentPath list, int oneBased)
    {
        var entry = session.ReadList(list, oneBased - 1, 1).Entries[0];
        return entry.Cells.Single(cell => cell.Name == "#text").Text ?? "";
    }
}
