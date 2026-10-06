namespace BigJsonXml.Tests;

/// <summary>
/// Pins the outline and the hits of generated documents the viewer already accepts.
/// Later speed work has to keep these results.
/// </summary>
public class OutlineLockTests
{
    [Fact]
    public void Generated_xml_keeps_its_outline_names_kinds_and_entry_places()
    {
        var session = SessionFixture.Scan(SampleXml);
        Assert.Equal(ScanProgress.Completed, session.Progress);

        var root = session.ReadRecord(DocumentPath.Root).Fields;
        Assert.Equal(["@xmlns:ex", "ex:title", "label", "group"], root.Select(field => field.Name).ToArray());
        Assert.Equal(FieldKind.Scalar, root[0].Kind);
        Assert.Equal("urn:example", root[0].Text);
        Assert.Equal(FieldKind.Record, root[1].Kind);
        Assert.Equal(FieldKind.Scalar, root[2].Kind);
        Assert.Equal("RTM=123", root[2].Text);
        Assert.Equal(FieldKind.List, root[3].Kind);
        Assert.Equal(2, root[3].ListCount);
        Assert.Equal("group[2]", root[3].ListLabel);

        session.Expand(DocumentPath.Root.Field("ex:title"));
        var title = session.ReadRecord(DocumentPath.Root.Field("ex:title")).Fields;
        Assert.Equal(["@id", "#text"], title.Select(field => field.Name).ToArray());
        Assert.Equal("T1", title[0].Text);
        Assert.Equal("Hello caf\u00e9", title[1].Text);

        var groups = DocumentPath.Root.Field("group");
        session.Expand(groups);
        var table = session.ReadList(groups, 0, 2);
        Assert.Equal(["@name", "code", "note", "nest"], table.Columns);
        Assert.Equal("Alpha", Cell(table, 0, "@name").Text);
        Assert.Equal(FieldKind.List, Cell(table, 0, "code").Kind);
        Assert.Equal(2, Cell(table, 0, "code").ListCount);
        Assert.Equal(FieldKind.Record, Cell(table, 0, "note").Kind);
        Assert.Equal(FieldKind.Record, Cell(table, 0, "nest").Kind);
        Assert.Equal("Beta", Cell(table, 1, "@name").Text);
        Assert.Equal(FieldKind.Scalar, Cell(table, 1, "code").Kind);
        Assert.Equal("StartHere", Cell(table, 1, "code").Text);
        Assert.Equal("", Cell(table, 1, "note").Text);
        Assert.Equal("", Cell(table, 1, "nest").Text);

        var codes = groups.WithIndex(1).Field("code");
        session.Expand(codes);
        session.Collapse(codes);
        session.Expand(codes);
        var codeRows = session.ReadList(codes, 0, 2);
        Assert.Equal(["#text"], codeRows.Columns);
        Assert.Equal("RTM=123 and 123", Cell(codeRows, 0, "#text").Text);
        Assert.Equal("World", Cell(codeRows, 1, "#text").Text);

        var nest = groups.WithIndex(1).Field("nest");
        session.Expand(nest);
        var nestFields = session.ReadRecord(nest).Fields;
        Assert.Equal(["code"], nestFields.Select(field => field.Name).ToArray());
        Assert.Equal(FieldKind.List, nestFields[0].Kind);
        Assert.Equal(2, nestFields[0].ListCount);

        var inner = nest.Field("code");
        session.Expand(inner);
        session.Collapse(inner);
        session.Expand(inner);
        var innerRows = session.ReadList(inner, 0, 2);
        Assert.Equal(["#text", "@id", "child"], innerRows.Columns);
        Assert.Equal("nested Montr\u00e9al", Cell(innerRows, 0, "#text").Text);
        Assert.Equal("", Cell(innerRows, 0, "@id").Text);
        Assert.Equal("", Cell(innerRows, 0, "child").Text);
        Assert.Equal(" tail", Cell(innerRows, 1, "#text").Text);
        Assert.Equal("c4", Cell(innerRows, 1, "@id").Text);
        Assert.Equal("mix", Cell(innerRows, 1, "child").Text);

        session.Expand(groups.WithIndex(1).Field("note"));
        var note = session.ReadRecord(groups.WithIndex(1).Field("note")).Fields;
        Assert.Equal(["@id", "#text"], note.Select(field => field.Name).ToArray());
        Assert.Equal("n1", note[0].Text);
        Assert.Equal("plain", note[1].Text);
    }

    [Fact]
    public void Generated_xml_keeps_every_text_search_condition()
    {
        var session = SessionFixture.Scan(SampleXml);

        AssertHits(session, new TextSearch { FindWhat = "" });
        AssertHits(session, new TextSearch { FindWhat = "hidden" });
        AssertHits(session, new TextSearch { FindWhat = "*" });
        AssertHits(session, new TextSearch { FindWhat = "world" });
        AssertHits(
            session,
            new TextSearch { FindWhat = "123", ElementName = false, AttributeName = false },
            "/doc/label@4+3",
            "/doc/group[1]/code[1]/#text@4+3",
            "/doc/group[1]/code[1]/#text@12+3");
        AssertHits(
            session,
            new TextSearch { FindWhat = "World" },
            "/doc/group[1]/code[2]/#text@0+5");
        AssertHits(
            session,
            new TextSearch { FindWhat = "world", MatchCase = false },
            "/doc/group[1]/code[2]/#text@0+5");
        AssertHits(
            session,
            new TextSearch { FindWhat = "RTM", StartsWith = true, ElementName = false, AttributeName = false },
            "/doc/label@0+3",
            "/doc/group[1]/code[1]/#text@0+3");
        AssertHits(session, new TextSearch { FindWhat = "123", StartsWith = true });
        AssertHits(
            session,
            new TextSearch { FindWhat = "World", EntireCell = true },
            "/doc/group[1]/code[2]/#text@0+5");
        AssertHits(session, new TextSearch { FindWhat = "123", EntireCell = true });
        AssertHits(
            session,
            new TextSearch { FindWhat = "World", EntireCell = true, StartsWith = true },
            "/doc/group[1]/code[2]/#text@0+5");
        AssertHits(session, new TextSearch { FindWhat = "R", EntireCell = true, StartsWith = true });
        AssertHits(
            session,
            new TextSearch { FindWhat = "code", Value = false, AttributeName = false },
            "/doc/group[1]/code[1]@0+4",
            "/doc/group[1]/code[2]@0+4",
            "/doc/group[1]/nest/code[1]@0+4",
            "/doc/group[1]/nest/code[2]@0+4",
            "/doc/group[2]/code@0+4");
        AssertHits(
            session,
            new TextSearch { FindWhat = "id", ElementName = false, Value = false },
            "/doc/ex:title/@id@1+2",
            "/doc/group[1]/note/@id@1+2",
            "/doc/group[1]/nest/code[2]/@id@1+2");
        AssertHits(
            session,
            new TextSearch { FindWhat = "@id", EntireCell = true, ElementName = false, Value = false },
            "/doc/ex:title/@id@0+3",
            "/doc/group[1]/note/@id@0+3",
            "/doc/group[1]/nest/code[2]/@id@0+3");
        AssertHits(
            session,
            new TextSearch { FindWhat = "123", ElementName = false, AttributeName = false, FieldName = "label" },
            "/doc/label@4+3");
        AssertHits(session, new TextSearch { FindWhat = "123", ElementName = false, AttributeName = false, FieldName = "code" });
        AssertHits(
            session,
            new TextSearch { FindWhat = "tail", ElementName = false, AttributeName = false, FieldName = "#text" },
            "/doc/group[1]/nest/code[2]/#text@1+4");
        AssertHits(session, new TextSearch { FindWhat = "tail", ElementName = false, AttributeName = false, FieldName = "code" });
        AssertHits(session, new TextSearch { FindWhat = "plain", ElementName = false, AttributeName = false, FieldName = "note" });
        AssertHits(
            session,
            new TextSearch { FindWhat = "plain", EntireCell = true, ElementName = false, AttributeName = false, FieldName = "#text" },
            "/doc/group[1]/note/#text@0+5");
        AssertHits(session, new TextSearch { FindWhat = "123", ElementName = false, AttributeName = false, ParentElementName = "group" });
        AssertHits(
            session,
            new TextSearch { FindWhat = "StartHere", ElementName = false, AttributeName = false, ParentElementName = "group" },
            "/doc/group[2]/code@0+9");
        AssertHits(
            session,
            new TextSearch { FindWhat = "123", ElementName = false, AttributeName = false, ParentElementName = "doc" },
            "/doc/label@4+3");
        AssertHits(
            session,
            new TextSearch { FindWhat = "123", ElementName = false, AttributeName = false, GrandfatherElementName = "group" },
            "/doc/group[1]/code[1]/#text@4+3",
            "/doc/group[1]/code[1]/#text@12+3");
        AssertHits(session, new TextSearch { FindWhat = "123", ElementName = false, AttributeName = false, GrandfatherElementName = "doc" });
        AssertHits(
            session,
            new TextSearch { FindWhat = "StartHere", ElementName = false, AttributeName = false, GrandfatherElementName = "doc" },
            "/doc/group[2]/code@0+9");
        AssertHits(
            session,
            new TextSearch { FindWhat = "StartHere", ElementName = false, AttributeName = false, ParentAttributeName = "@name" },
            "/doc/group[2]/code@0+9");
        AssertHits(session, new TextSearch { FindWhat = "StartHere", ElementName = false, AttributeName = false, ParentAttributeName = "name" });
        AssertHits(
            session,
            new TextSearch { FindWhat = "T1", ElementName = false, AttributeName = false, GrandfatherElementName = "doc" },
            "/doc/ex:title/@id@0+2");
        AssertHits(
            session,
            new TextSearch { FindWhat = "caf\u00e9", ElementName = false, AttributeName = false },
            "/doc/ex:title/#text@6+4");
        AssertHits(
            session,
            new TextSearch { FindWhat = "Montr\u00e9al", ElementName = false, AttributeName = false },
            "/doc/group[1]/nest/code[1]/#text@7+8");
        AssertHits(
            session,
            new TextSearch { FindWhat = "ex:title", Value = false, AttributeName = false },
            "/doc/ex:title@0+8");
        AssertHits(
            session,
            new TextSearch { FindWhat = "plain", ElementName = false, AttributeName = false, ParentAttributeName = "@id" },
            "/doc/group[1]/note/#text@0+5");
    }

    [Fact]
    public void Xml_hits_stream_in_document_order_before_the_scan_finishes()
    {
        var cut = SampleXml.IndexOf("<group name=\"Beta\"", StringComparison.Ordinal);
        var source = new MemoryByteSource();
        source.Append(SampleXml[..cut]);
        var session = ViewerSession.Open(source);
        session.Search(new TextSearch { FindWhat = "123", ElementName = false, AttributeName = false });
        session.PumpAll();

        Assert.Equal(ScanProgress.Scanning, session.Progress);
        AssertHitsAlready(
            session,
            "/doc/label@4+3",
            "/doc/group/code[1]/#text@4+3",
            "/doc/group/code[1]/#text@12+3");

        source.Append(SampleXml[cut..]);
        source.Complete();
        session.PumpAll();
        Assert.Equal(ScanProgress.Completed, session.Progress);
        AssertHitsAlready(
            session,
            "/doc/label@4+3",
            "/doc/group[1]/code[1]/#text@4+3",
            "/doc/group[1]/code[1]/#text@12+3");
    }

    [Fact]
    public void Generated_json_keeps_its_outline_literals_and_entry_places()
    {
        var session = SessionFixture.Scan(SampleJson);
        Assert.Equal(ScanProgress.Completed, session.Progress);

        var root = session.ReadRecord(DocumentPath.Root).Fields;
        Assert.Equal(["title", "count", "ok", "gap", "box", "items"], root.Select(field => field.Name).ToArray());
        Assert.Equal("\"Hello caf\u00e9\"", root[0].Text);
        Assert.Equal("2", root[1].Text);
        Assert.Equal("true", root[2].Text);
        Assert.Equal("null", root[3].Text);
        Assert.Equal(FieldKind.Record, root[4].Kind);
        Assert.Equal("{}", root[4].Text);
        Assert.Empty(session.ReadRecord(DocumentPath.Root.Field("box")).Fields);
        Assert.Equal(FieldKind.List, root[5].Kind);
        Assert.Equal(3, root[5].ListCount);
        Assert.Equal("items[3]", root[5].ListLabel);

        var items = DocumentPath.Root.Field("items");
        session.Expand(items);
        session.Collapse(items);
        session.Expand(items);
        var rows = session.ReadList(items, 0, 3);
        Assert.Equal(["id", "name", "note", "tags"], rows.Columns);
        Assert.Equal("\"a1\"", Cell(rows, 0, "id").Text);
        Assert.Equal("\"RTM=123 and 123\"", Cell(rows, 0, "name").Text);
        Assert.Equal("", Cell(rows, 0, "note").Text);
        Assert.Equal("", Cell(rows, 0, "tags").Text);
        Assert.Equal("\"a2\"", Cell(rows, 1, "id").Text);
        Assert.Equal("\"World\"", Cell(rows, 1, "name").Text);
        Assert.Equal("", Cell(rows, 2, "id").Text);
        Assert.Equal("\"plain\"", Cell(rows, 2, "note").Text);
        Assert.Equal(FieldKind.List, Cell(rows, 2, "tags").Kind);
        Assert.Equal(1, Cell(rows, 2, "tags").ListCount);
        Assert.Equal("tags[1]", Cell(rows, 2, "tags").ListLabel);

        var tags = items.WithIndex(3).Field("tags");
        session.Expand(tags);
        var tagRows = session.ReadList(tags, 0, 1);
        Assert.Equal(["#value"], tagRows.Columns);
        Assert.Equal("\"x\"", Cell(tagRows, 0, "#value").Text);
    }

    [Fact]
    public void Generated_json_keeps_every_text_search_condition()
    {
        var session = SessionFixture.Scan(SampleJson);

        AssertHits(session, new TextSearch { FindWhat = "" });
        AssertHits(session, new TextSearch { FindWhat = "{", ElementName = false, AttributeName = false });
        AssertHits(session, new TextSearch { FindWhat = "items[3]", ElementName = false, AttributeName = false });
        AssertHits(session, new TextSearch { FindWhat = "@id", ElementName = false, Value = false });
        AssertHits(session, new TextSearch { FindWhat = "plain", ElementName = false, AttributeName = false, ParentAttributeName = "@id" });
        AssertHits(
            session,
            new TextSearch { FindWhat = "123", ElementName = false, AttributeName = false },
            "/items[1]/name@5+3",
            "/items[1]/name@13+3");
        AssertHits(
            session,
            new TextSearch { FindWhat = "world", MatchCase = false, ElementName = false, AttributeName = false },
            "/items[2]/name@1+5");
        AssertHits(session, new TextSearch { FindWhat = "world", ElementName = false, AttributeName = false });
        AssertHits(session, new TextSearch { FindWhat = "RTM", StartsWith = true, ElementName = false, AttributeName = false });
        AssertHits(
            session,
            new TextSearch { FindWhat = "\"RTM", StartsWith = true, ElementName = false, AttributeName = false },
            "/items[1]/name@0+4");
        AssertHits(session, new TextSearch { FindWhat = "World", EntireCell = true, ElementName = false, AttributeName = false });
        AssertHits(
            session,
            new TextSearch { FindWhat = "\"World\"", EntireCell = true, ElementName = false, AttributeName = false },
            "/items[2]/name@0+7");
        AssertHits(
            session,
            new TextSearch { FindWhat = "\"a1\"", EntireCell = true, ElementName = false, AttributeName = false },
            "/items[1]/id@0+4");
        AssertHits(
            session,
            new TextSearch { FindWhat = "true", EntireCell = true, ElementName = false, AttributeName = false },
            "/ok@0+4");
        AssertHits(
            session,
            new TextSearch { FindWhat = "null", EntireCell = true, ElementName = false, AttributeName = false },
            "/gap@0+4");
        AssertHits(
            session,
            new TextSearch { FindWhat = "id", Value = false, AttributeName = false },
            "/items[1]/id@0+2",
            "/items[2]/id@0+2");
        AssertHits(
            session,
            new TextSearch { FindWhat = "123", ElementName = false, AttributeName = false, FieldName = "name", ParentElementName = "items" },
            "/items[1]/name@5+3",
            "/items[1]/name@13+3");
        AssertHits(session, new TextSearch { FindWhat = "123", ElementName = false, AttributeName = false, FieldName = "id" });
        AssertHits(
            session,
            new TextSearch { FindWhat = "\"x\"", EntireCell = true, ElementName = false, AttributeName = false, ParentElementName = "tags" },
            "/items[3]/tags[1]/#value@0+3");
        AssertHits(
            session,
            new TextSearch { FindWhat = "\"x\"", EntireCell = true, ElementName = false, AttributeName = false, GrandfatherElementName = "items" },
            "/items[3]/tags[1]/#value@0+3");
        AssertHits(session, new TextSearch { FindWhat = "\"x\"", EntireCell = true, ElementName = false, AttributeName = false, GrandfatherElementName = "tags" });
        AssertHits(
            session,
            new TextSearch { FindWhat = "caf\u00e9", ElementName = false, AttributeName = false },
            "/title@7+4");
        AssertHits(
            session,
            new TextSearch { FindWhat = "items", Value = false, AttributeName = false },
            "/items@0+5");
    }

    [Fact]
    public void Json_that_is_empty_blank_repeated_or_a_second_value_does_not_become_another_document()
    {
        Assert.Equal(ScanProgress.Stopped, SessionFixture.Scan("").Progress);
        Assert.Equal(ScanProgress.Stopped, SessionFixture.Scan(" \n\t").Progress);

        var second = SessionFixture.Scan("{\"a\":1}{\"b\":2}");
        Assert.Equal(ScanProgress.Stopped, second.Progress);
        Assert.Equal("A JSON document is one value.", second.Error!.Message);
        Assert.Throws<InvalidOperationException>(() => second.ReadRecord(DocumentPath.Root));

        var repeated = SessionFixture.Scan("{\"a\":1,\"b\":2,\"a\":3,\"c\":4}");
        Assert.Equal(ScanProgress.Stopped, repeated.Progress);
        Assert.Equal("A repeated key stops the scan.", repeated.Error!.Message);
        Assert.Equal(["a", "b"], repeated.ReadRecord(DocumentPath.Root).Fields.Select(field => field.Name).ToArray());
        repeated.Search(new TextSearch { FindWhat = "4", EntireCell = true, ElementName = false, AttributeName = false });
        repeated.PumpAll();
        Assert.Empty(repeated.Hits);
    }

    private static FieldView Cell(ListView view, int row, string name) =>
        view.Entries[row].Cells.Single(cell => cell.Name == name);

    private static void AssertHits(ViewerSession session, TextSearch search, params string[] hits)
    {
        session.Search(search);
        session.PumpAll();
        AssertHitsAlready(session, hits);
    }

    private static void AssertHitsAlready(ViewerSession session, params string[] hits)
    {
        var actual = session.Hits
            .Select(hit => $"{session.FormatPath(hit.Path)}@{hit.CharacterOffset}+{hit.MatchLength}")
            .ToArray();
        Assert.Equal(hits, actual);
    }

    private const string SampleXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <!DOCTYPE doc [
          <!ENTITY city "Montr&#xE9;al">
        ]>
        <doc xmlns:ex="urn:example">
          <!-- hidden comment -->
          <?target skip?>
          <ex:title id="T1">Hello café</ex:title>
          <label>RTM=123</label>
          <group name="Alpha">
            <code>RTM=123 and 123</code>
            <code>World</code>
            <note id="n1">plain</note>
            <nest>
              <code>nested &city;</code>
              <code id="c4"><child>mix</child> tail</code>
            </nest>
          </group>
          <group name="Beta">
            <code>StartHere</code>
          </group>
        </doc>
        """;

    private const string SampleJson = """
        {
          "title": "Hello café",
          "count": 2,
          "ok": true,
          "gap": null,
          "box": {},
          "items": [
            {"id": "a1", "name": "RTM=123 and 123"},
            {"id": "a2", "name": "World"},
            {"note": "plain", "tags": ["x"]}
          ]
        }
        """;
}
