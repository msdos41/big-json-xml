namespace BigJsonXml.Tests;

public class OpenRecordTableTests
{
    [Fact]
    public void Document_element_is_a_record_table()
    {
        var session = SessionFixture.Scan("<root id=\"1\"><name>Ada</name></root>");

        var fields = session.ReadRecord(DocumentPath.Root).Fields;

        Assert.Equal(["@id", "name"], fields.Select(field => field.Name).ToArray());
        Assert.Equal(FieldKind.Scalar, fields[0].Kind);
        Assert.Equal("1", fields[0].Text);
        Assert.Equal("Ada", fields[1].Text);
        Assert.False(fields[1].CanExpand);
        Assert.DoesNotContain(fields, field => field.Kind == FieldKind.List);
    }

    [Fact]
    public void Mixed_text_is_one_hash_text_field_at_the_first_piece()
    {
        var session = SessionFixture.Scan("<root id=\"1\"> hello <child>x</child> world </root>");

        var fields = session.ReadRecord(DocumentPath.Root).Fields;

        Assert.Equal(["@id", "#text", "child"], fields.Select(field => field.Name).ToArray());
        Assert.Equal(" hello  world ", fields[1].Text);
        Assert.Equal("x", fields[2].Text);
    }

    [Fact]
    public void Whitespace_only_text_is_omitted_including_preserve()
    {
        var session = SessionFixture.Scan("""
            <root xml:space="preserve">
              <child>  hi  </child>
            </root>
            """);

        var fields = session.ReadRecord(DocumentPath.Root).Fields;

        Assert.Equal(["@xml:space", "child"], fields.Select(field => field.Name).ToArray());
        Assert.Equal("preserve", fields[0].Text);
        Assert.Equal("  hi  ", fields[1].Text);
        Assert.False(fields[1].CanExpand);
    }

    [Fact]
    public void Comments_and_processing_instructions_are_absent()
    {
        var session = SessionFixture.Scan("<root><!-- secret --><child>a</child><?pi x?></root>");

        var fields = session.ReadRecord(DocumentPath.Root).Fields;

        Assert.Equal(["child"], fields.Select(field => field.Name).ToArray());
        Assert.Equal("a", fields[0].Text);
    }

    [Fact]
    public void Empty_element_is_an_empty_scalar()
    {
        var session = SessionFixture.Scan("<root><child/>  <blank>   </blank></root>");

        Assert.Equal("", session.Field("child").Text);
        Assert.Equal("", session.Field("blank").Text);
        Assert.False(session.Field("child").CanExpand);
    }

    [Fact]
    public void Internal_entities_are_expanded()
    {
        var session = SessionFixture.Scan("""
            <!DOCTYPE root [
              <!ENTITY who "Ada">
            ]>
            <root title="&who; &amp; co"><name>&#65;&#x64;&#97;</name></root>
            """);

        Assert.Equal("Ada & co", session.Field("@title").Text);
        Assert.Equal("Ada", session.Field("name").Text);
        Assert.Equal(ScanProgress.Completed, session.Progress);
    }

    [Fact]
    public void Bare_attribute_does_not_hide_later_elements()
    {
        var session = SessionFixture.Scan("""
            <CadDocument id="doc-main" name="Harness" units="mm">
              <Layers>
                <Layer id="0" name="0" color="#94a3b8" visible locked="false" undeletable></Layer>
                <Layer id="Frame" name="Frame" color="#64748b" visible locked></Layer>
              </Layers>
              <Features>
                <Feature id="line-1" type="line" visible></Feature>
                <Feature id="line-2" type="line" visible></Feature>
              </Features>
            </CadDocument>
            """);

        Assert.Equal(ScanProgress.Completed, session.Progress);
        Assert.Null(session.Error);
        Assert.Equal(
            ["@id", "@name", "@units", "Layers", "Features"],
            session.ReadRecord(DocumentPath.Root).Fields.Select(field => field.Name).ToArray());
        Assert.True(session.Field("Layers").CanExpand);
        Assert.Equal(FieldKind.Record, session.Field("Layers").Kind);
        Assert.True(session.Field("Features").CanExpand);

        var layers = DocumentPath.Root.Field("Layers");
        var layer = session.ReadRecord(layers).Fields.Single(field => field.Name == "Layer");
        Assert.Equal(FieldKind.List, layer.Kind);
        Assert.Equal(2, layer.ListCount);

        var list = layers.Field("Layer");
        session.Expand(list);
        var entries = session.ReadList(list, 0, 2).Entries;
        Assert.Equal("", entries[0].Cells.Single(cell => cell.Name == "@visible").Text);
        Assert.Equal("false", entries[0].Cells.Single(cell => cell.Name == "@locked").Text);
        Assert.Equal("", entries[0].Cells.Single(cell => cell.Name == "@undeletable").Text);
        Assert.Equal("", entries[1].Cells.Single(cell => cell.Name == "@locked").Text);
        Assert.Equal("", entries[1].Cells.Single(cell => cell.Name == "@undeletable").Text);
    }

    [Fact]
    public void Unquoted_attribute_value_still_stops_the_scan()
    {
        var session = SessionFixture.Scan("<root><child flag=yes></child><after>1</after></root>");

        Assert.Equal(ScanProgress.Stopped, session.Progress);
        Assert.NotNull(session.Error);
        Assert.Contains("quoted", session.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(session.ReadRecord(DocumentPath.Root).Fields, field => field.Name == "after");
    }

    [Fact]
    public void Cdata_is_literal_text()
    {
        var session = SessionFixture.Scan("<root><name><![CDATA[A & B]]></name></root>");

        Assert.Equal("A & B", session.Field("name").Text);
    }

    [Fact]
    public void Open_parent_is_a_field_while_its_end_tag_is_still_ahead()
    {
        var source = new MemoryByteSource();
        var session = ViewerSession.Open(source);
        source.Append("""
            <CadDocument id="doc-main" name="Harness" units="mm">
              <Layers>
                <Layer id="0" visible></Layer>
              </Layers>
              <Features>
                <Feature id="a" visible>
            """);
        session.PumpAll();

        Assert.Equal(ScanProgress.Scanning, session.Progress);
        Assert.Equal(
            ["@id", "@name", "@units", "Layers", "Features"],
            session.ReadRecord(DocumentPath.Root).Fields.Select(field => field.Name).ToArray());
        Assert.Equal(FieldKind.Record, session.Field("Features").Kind);
        Assert.True(session.Field("Features").CanExpand);
        session.SetViewport(0, 0, 800, 600);
        Assert.Contains(session.ReadSurface().Rows, row => row.Name == "Features" && row.CanExpand);

        var features = DocumentPath.Root.Field("Features");
        Assert.Equal(FieldKind.Record, session.ReadRecord(features).Fields.Single(field => field.Name == "Feature").Kind);

        source.Append("</Feature><Feature id=\"b\" visible></Feature>");
        session.PumpAll();

        Assert.Equal(ScanProgress.Scanning, session.Progress);
        var feature = session.ReadRecord(features).Fields.Single(field => field.Name == "Feature");
        Assert.Equal(FieldKind.List, feature.Kind);
        Assert.Equal(2, feature.ListCount);
        Assert.Contains(session.ReadRecord(DocumentPath.Root).Fields, field => field.Name == "Features");

        source.Append("</Features></CadDocument>");
        source.Complete();
        session.PumpAll();

        Assert.Equal(ScanProgress.Completed, session.Progress);
        Assert.Equal(2, session.ReadRecord(features).Fields.Single(field => field.Name == "Feature").ListCount);
    }

    [Fact]
    public void Presentation_is_available_before_the_end_of_the_file()
    {
        var source = new MemoryByteSource();
        var session = ViewerSession.Open(source);
        source.Append("<root><name>Ad");
        session.PumpAll();
        Assert.Equal(ScanProgress.Scanning, session.Progress);

        source.Append("a</name>");
        session.PumpAll();

        Assert.Equal("Ada", session.Field("name").Text);
        Assert.Equal(ScanProgress.Scanning, session.Progress);

        source.Append("</root>");
        source.Complete();
        session.PumpAll();
        Assert.Equal(ScanProgress.Completed, session.Progress);
    }

    [Fact]
    public void Session_does_not_return_the_document_as_a_tree_or_one_string()
    {
        var xml = "<root>" + string.Concat(Enumerable.Range(0, 40).Select(i => $"<item>{i}</item>")) + "</root>";
        var session = SessionFixture.Scan(xml);
        var fields = session.ReadRecord(DocumentPath.Root).Fields;

        Assert.Single(fields);
        Assert.Equal(FieldKind.List, fields[0].Kind);
        Assert.Equal(40, fields[0].ListCount);
        Assert.Null(fields[0].Text);
        Assert.DoesNotContain(
            typeof(ViewerSession).GetProperties(),
            property => property.PropertyType == typeof(string) && property.Name.Contains("Document", StringComparison.Ordinal));
    }

    [Fact]
    public void Surface_shows_the_same_record()
    {
        var session = SessionFixture.Scan("<root id=\"1\"><name>Ada</name></root>");
        session.SetViewport(0, 0, 800, 600);

        var frame = session.ReadSurface();

        Assert.Equal(1, frame.ScrollSurfaces);
        Assert.Contains(frame.Rows, row => row.Name == "@id" && row.Value == "1");
        Assert.Contains(frame.Rows, row => row.Name == "name" && row.Value == "Ada");
    }
}
