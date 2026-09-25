namespace BigJsonXml.Tests;

public class NamespaceAndSearchTests
{
    [Fact]
    public void Prefixed_names_keep_the_first_spelling_and_share_one_field()
    {
        var session = SessionFixture.Scan("""
            <root xmlns:a="urn:ex" xmlns:b="urn:ex">
              <a:item>1</a:item>
              <b:item>2</b:item>
            </root>
            """);

        var list = session.Field("a:item");
        Assert.Equal(FieldKind.List, list.Kind);
        Assert.Equal(2, list.ListCount);
        Assert.DoesNotContain(session.ReadRecord(DocumentPath.Root).Fields, field => field.Name == "b:item");
    }

    [Fact]
    public void Default_namespace_and_attribute_stay_distinct()
    {
        var session = SessionFixture.Scan("""
            <root xmlns="urn:ex" xmlns:a="urn:ex">
              <item>one</item>
              <a:id>e</a:id>
              <box a:id="v"/>
            </root>
            """);

        Assert.Equal(FieldKind.Scalar, session.Field("item").Kind);
        Assert.Equal("one", session.Field("item").Text);
        Assert.Equal("e", session.Field("a:id").Text);
        session.Expand(DocumentPath.Root.Field("box"));
        var inside = session.ReadRecord(DocumentPath.Root.Field("box")).Fields;
        Assert.Contains(inside, field => field.Name == "@a:id" && field.Text == "v");
        Assert.DoesNotContain(inside, field => field.Name == "a:id");
    }

    [Fact]
    public void Text_search_is_case_sensitive_and_skips_names()
    {
        var session = SessionFixture.Scan("<root><item id=\"Ada\">ada</item><item id=\"ADA\">Ada</item></root>");
        session.Search("Ada");
        session.PumpAll();

        Assert.Equal(2, session.Hits.Count);
        Assert.Equal("item[1] / @id", session.Hits[0].Path.ToString());
        Assert.Equal("item[2] / #text", session.Hits[1].Path.ToString());
        Assert.Equal(0, session.Hits[1].CharacterOffset);
    }

    [Fact]
    public void Field_search_is_exact_or_contains_and_keeps_trailing_spaces()
    {
        var session = SessionFixture.Scan("""
            <root>
              <box id="2"/>
              <box id="20"/>
              <note>hello world</note>
              <pad>a </pad>
            </root>
            """);

        session.Search("@missing = 2");
        session.PumpAll();
        Assert.Empty(session.Hits);

        session.Search("@id = 2");
        session.PumpAll();
        Assert.Equal("box[1] / @id", Assert.Single(session.Hits).Path.ToString());

        session.Search("@id = 20");
        session.PumpAll();
        Assert.Equal("box[2] / @id", Assert.Single(session.Hits).Path.ToString());

        session.Search("box = 2");
        session.PumpAll();
        Assert.Empty(session.Hits);

        session.Search("note ~ world");
        session.PumpAll();
        Assert.Equal(6, Assert.Single(session.Hits).CharacterOffset);

        session.Search("note ~ World");
        session.PumpAll();
        Assert.Empty(session.Hits);

        session.Search("pad = a ");
        session.PumpAll();
        Assert.Single(session.Hits);

        session.Search("pad = a");
        session.PumpAll();
        Assert.Empty(session.Hits);
    }

    [Fact]
    public void Field_names_use_the_displayed_spelling_and_namespace_identity()
    {
        var session = SessionFixture.Scan("""
            <root xmlns:a="urn:ex" xmlns:b="urn:ex">
              <a:item>one</a:item>
              <b:item>two</b:item>
              <b:note>two</b:note>
              <box id="2" extra="20"/>
            </root>
            """);

        session.Search("#text = two");
        session.PumpAll();
        Assert.Equal("a:item[2] / #text", Assert.Single(session.Hits).Path.ToString());

        session.Search("#text = one");
        session.PumpAll();
        Assert.Equal("a:item[1] / #text", Assert.Single(session.Hits).Path.ToString());

        session.Search("a:note = two");
        session.PumpAll();
        Assert.Equal("b:note", Assert.Single(session.Hits).Path.ToString());

        session.Search("@id = 2");
        session.PumpAll();
        Assert.Equal("box / @id", Assert.Single(session.Hits).Path.ToString());

        session.Search("2");
        session.PumpAll();
        Assert.Equal(2, session.Hits.Count);
    }

    [Fact]
    public void Default_namespace_matches_an_unprefixed_field_name()
    {
        var session = SessionFixture.Scan("""<root xmlns="urn:ex"><item>one</item></root>""");
        session.Search("item = one");
        session.PumpAll();

        Assert.Equal("item", Assert.Single(session.Hits).Path.ToString());
    }

    [Fact]
    public void Search_reports_hits_while_scanning_and_ignores_collapsed_labels()
    {
        var source = new MemoryByteSource();
        var session = ViewerSession.Open(source);
        source.Append("<root><item>findme</item><item>later</item>");
        session.PumpAll();
        session.Search("findme");
        session.PumpAll();

        Assert.Single(session.Hits);
        Assert.Equal(ScanProgress.Scanning, session.Progress);

        source.Append("<item>findme</item>");
        session.PumpAll();
        Assert.Equal(2, session.Hits.Count);
        Assert.Equal("item[1] / #text", session.Hits[0].Path.ToString());
        Assert.Equal("item[3] / #text", session.Hits[1].Path.ToString());

        session.Search("item[3]");
        session.PumpAll();
        Assert.Empty(session.Hits);

        source.Append("</root>");
        source.Complete();
        session.PumpAll();
    }

    [Fact]
    public void Revealing_a_hit_opens_the_path_and_centers_a_long_scalar()
    {
        var body = new string('a', 150) + "NEEDLE" + new string('b', 200);
        var items = string.Concat(Enumerable.Range(0, 40).Select(i => i == 30 ? $"<item>{body}</item>" : "<item>x</item>"));
        var session = SessionFixture.Scan($"<root>{items}</root>");
        session.SetViewport(0, 0, 800, ViewerSession.RowHeight * 5);
        session.Search("NEEDLE");
        session.PumpAll();

        var hit = Assert.Single(session.Hits);
        Assert.False(session.Field("item").IsExpanded);
        session.Reveal(hit);

        Assert.True(session.Field("item").IsExpanded);
        Assert.Equal(hit.Path, session.Marked);
        Assert.False(session.ReadScalar(hit.Path).IsFullValueExpanded);
        var window = session.ReadScalar(hit.Path);
        Assert.Contains("NEEDLE", window.Text, StringComparison.Ordinal);
        Assert.True(window.WindowStart > 0);
        Assert.Equal(200, window.Text!.Length);
        Assert.True(session.ViewportY > ViewerSession.RowHeight * 10);

        session.SetViewport(session.ViewportX, session.ViewportY, 800, ViewerSession.RowHeight * 8);
        Assert.Contains(session.ReadSurface().Rows, row => row.IsMarked);
    }

    [Fact]
    public void Clearing_a_hit_drops_the_mark_and_restores_the_scalar_window()
    {
        var body = new string('a', 150) + "NEEDLE" + new string('b', 200);
        var items = string.Concat(Enumerable.Range(0, 4).Select(i => i == 2 ? $"<item>{body}</item>" : "<item>x</item>"));
        var session = SessionFixture.Scan($"<root>{items}</root>");
        session.SetViewport(0, 0, 900, ViewerSession.RowHeight * 12);
        session.Search("NEEDLE");
        session.PumpAll();

        var hit = Assert.Single(session.Hits);
        session.Reveal(hit);
        Assert.True(session.Field("item").IsExpanded);
        Assert.True(session.ReadScalar(hit.Path).WindowStart > 0);
        Assert.Contains(session.ReadSurface().Rows, row => row.IsMarked);

        session.ClearMark();

        Assert.Null(session.Marked);
        Assert.Equal(0, session.ReadScalar(hit.Path).WindowStart);
        Assert.StartsWith(new string('a', 20), session.ReadScalar(hit.Path).Text, StringComparison.Ordinal);
        Assert.DoesNotContain(session.ReadSurface().Rows, row => row.IsMarked);
        Assert.True(session.Field("item").IsExpanded);
        Assert.Equal(hit, Assert.Single(session.Hits));

        session.Reveal(hit);
        session.Search("absent");
        session.PumpAll();
        Assert.Null(session.Marked);
        Assert.Equal(0, session.ReadScalar(hit.Path).WindowStart);
        Assert.Empty(session.Hits);
        Assert.DoesNotContain(session.ReadSurface().Rows, row => row.IsMarked);
    }
}
