namespace BigJsonXml.Tests;

public class ListFieldTests
{
    [Fact]
    public void Repeated_siblings_are_one_list_even_with_a_gap()
    {
        var session = SessionFixture.Scan("""
            <root>
              <item>1</item>
              <note>gap</note>
              <item>2</item>
              <only>z</only>
            </root>
            """);

        Assert.Equal(FieldKind.List, session.Field("item").Kind);
        Assert.Equal("item[2]", session.Field("item").ListLabel);
        Assert.Equal(2, session.Field("item").ListCount);
        Assert.Equal(FieldKind.Scalar, session.Field("note").Kind);
        Assert.Equal(FieldKind.Scalar, session.Field("only").Kind);
        Assert.True(session.Field("item").CanExpand);
        Assert.False(session.Field("only").CanExpand);
    }

    [Fact]
    public void A_name_promotes_on_the_second_sibling_and_stays_a_list()
    {
        var source = new MemoryByteSource();
        var session = ViewerSession.Open(source);
        source.Append("<root><item>1</item>");
        session.PumpAll();

        Assert.Equal(FieldKind.Scalar, session.Field("item").Kind);
        Assert.Equal("1", session.Field("item").Text);

        source.Append("<note>x</note><item>2</item>");
        session.PumpAll();

        Assert.Equal(FieldKind.List, session.Field("item").Kind);
        Assert.Equal("item[2]", session.Field("item").ListLabel);
        Assert.Equal(FieldKind.Scalar, session.Field("note").Kind);

        source.Append("<tail>z</tail><item>3</item></root>");
        source.Complete();
        session.PumpAll();

        Assert.Equal(FieldKind.List, session.Field("item").Kind);
        Assert.Equal(3, session.Field("item").ListCount);
        Assert.Equal("item[3]", session.Field("item").ListLabel);
        Assert.Equal(ScanProgress.Completed, session.Progress);
    }

    [Fact]
    public void Surface_shows_the_list_label()
    {
        var session = SessionFixture.Scan("<root><item>1</item><item>2</item></root>");
        session.SetViewport(0, 0, 800, 400);

        Assert.Contains(session.ReadSurface().Rows, row => row.Name == "item" && row.Value == "item[2]");
    }
}
