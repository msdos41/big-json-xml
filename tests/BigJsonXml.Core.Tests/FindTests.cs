namespace BigJsonXml.Tests;

public class FindTests
{
    [Fact]
    public void Find_what_matches_values_in_order_and_marks_the_stretch()
    {
        var session = SessionFixture.Scan("""
            <root>
              <item id="Ada">ada</item>
              <item id="ADA">Ada</item>
              <note>RTM=123 and 123</note>
              <star>a*b</star>
              <eq>left a = b right</eq>
            </root>
            """);

        session.Search(new TextSearch { FindWhat = "" });
        session.PumpAll();
        Assert.Empty(session.Hits);

        session.Search(new TextSearch { FindWhat = "Ada" });
        session.PumpAll();
        Assert.Equal(2, session.Hits.Count);
        Assert.Equal("/root/item[1]/@id", session.FormatPath(session.Hits[0].Path));
        Assert.Equal("/root/item[2]/#text", session.FormatPath(session.Hits[1].Path));
        var id = session.ReadHitText(session.Hits[0], 40);
        Assert.Equal("Ada", id.Text);
        Assert.Equal(0, id.MarkStart);
        Assert.Equal(3, id.MarkLength);
        var text = session.ReadHitText(session.Hits[1], 40);
        Assert.Equal("Ada", text.Text);
        Assert.Equal(0, text.MarkStart);
        Assert.Equal(3, text.MarkLength);

        session.Search(new TextSearch { FindWhat = "123" });
        session.PumpAll();
        Assert.Equal(2, session.Hits.Count);
        Assert.Equal("/root/note", session.FormatPath(session.Hits[0].Path));
        Assert.Equal(4, session.Hits[0].CharacterOffset);
        Assert.Equal(12, session.Hits[1].CharacterOffset);
        var first = session.ReadHitText(session.Hits[0], 40);
        Assert.Equal("RTM=123 and 123", first.Text);
        Assert.Equal(4, first.MarkStart);
        Assert.Equal(3, first.MarkLength);
        var second = session.ReadHitText(session.Hits[1], 40);
        Assert.Equal(12, second.MarkStart);
        Assert.Equal(3, second.MarkLength);

        session.Search(new TextSearch { FindWhat = "*" });
        session.PumpAll();
        var star = Assert.Single(session.Hits);
        Assert.Equal("/root/star", session.FormatPath(star.Path));
        Assert.Equal(1, star.CharacterOffset);
        var starText = session.ReadHitText(star, 40);
        Assert.Equal("*", starText.Text.Substring(starText.MarkStart, starText.MarkLength));

        session.Search(new TextSearch { FindWhat = "a = b" });
        session.PumpAll();
        var eq = Assert.Single(session.Hits);
        Assert.Equal("/root/eq", session.FormatPath(eq.Path));
        Assert.Equal(5, eq.CharacterOffset);
    }

    [Fact]
    public void A_long_value_shows_a_window_around_the_match()
    {
        var body = new string('a', 80) + "NEEDLE" + new string('b', 80);
        var session = SessionFixture.Scan($"<root><note>{body}</note></root>");
        session.Search(new TextSearch { FindWhat = "NEEDLE" });
        session.PumpAll();
        var hit = Assert.Single(session.Hits);

        var whole = session.ReadHitText(hit, 200);
        Assert.Equal(body, whole.Text);
        Assert.Equal(80, whole.MarkStart);
        Assert.Equal(6, whole.MarkLength);

        var window = session.ReadHitText(hit, 10);
        Assert.Equal(10, window.Text.Length);
        Assert.Equal("NEEDLE", window.Text.Substring(window.MarkStart, window.MarkLength));
    }

    [Fact]
    public void Match_case_start_with_and_entire_cell_follow_one_switch()
    {
        var session = SessionFixture.Scan("""
            <root>
              <note>hello world</note>
              <pad>a </pad>
              <star>a*b</star>
              <code>RTM=123</code>
            </root>
            """);

        session.Search(new TextSearch { FindWhat = "World" });
        session.PumpAll();
        Assert.Empty(session.Hits);

        session.Search(new TextSearch { FindWhat = "World", MatchCase = false });
        session.PumpAll();
        Assert.Equal(6, Assert.Single(session.Hits).CharacterOffset);

        session.Search(new TextSearch { FindWhat = "HELLO", StartsWith = true, MatchCase = false });
        session.PumpAll();
        Assert.Equal("/root/note", session.FormatPath(Assert.Single(session.Hits).Path));

        session.Search(new TextSearch { FindWhat = "world", StartsWith = true });
        session.PumpAll();
        Assert.Empty(session.Hits);

        session.Search(new TextSearch { FindWhat = "123", EntireCell = true });
        session.PumpAll();
        Assert.Empty(session.Hits);

        session.Search(new TextSearch { FindWhat = "RTM=123", EntireCell = true });
        session.PumpAll();
        Assert.Equal("/root/code", session.FormatPath(Assert.Single(session.Hits).Path));

        session.Search(new TextSearch { FindWhat = "hello", StartsWith = true, EntireCell = true });
        session.PumpAll();
        Assert.Empty(session.Hits);

        session.Search(new TextSearch { FindWhat = "HELLO WORLD", EntireCell = true, MatchCase = false });
        session.PumpAll();
        Assert.Single(session.Hits);

        session.Search(new TextSearch { FindWhat = "a ", EntireCell = true });
        session.PumpAll();
        Assert.Equal("/root/pad", session.FormatPath(Assert.Single(session.Hits).Path));

        session.Search(new TextSearch { FindWhat = "a", EntireCell = true });
        session.PumpAll();
        Assert.Empty(session.Hits);

        session.Search(new TextSearch { FindWhat = "a*b", EntireCell = true });
        session.PumpAll();
        Assert.Equal("/root/star", session.FormatPath(Assert.Single(session.Hits).Path));

        session.Search(new TextSearch { FindWhat = "*", EntireCell = true });
        session.PumpAll();
        Assert.Empty(session.Hits);
    }

    [Fact]
    public void A_match_past_the_visible_window_still_counts_without_one_string()
    {
        var body = new string('a', 80) + "NEEDLE" + new string('b', 80);
        var session = SessionFixture.Scan($"<root><note>{body}</note></root>");
        var note = DocumentPath.Root.Field("note");
        Assert.DoesNotContain("NEEDLE", session.ReadScalar(note).Text, StringComparison.Ordinal);

        session.Search(new TextSearch { FindWhat = "NEEDLE" });
        session.PumpAll();
        Assert.Single(session.Hits);

        session.Search(new TextSearch { FindWhat = "NEEDLE", EntireCell = true });
        session.PumpAll();
        Assert.Empty(session.Hits);

        session.Search(new TextSearch { FindWhat = body, EntireCell = true });
        session.PumpAll();
        Assert.Single(session.Hits);

        var huge = new string('q', 1_500_000) + "Zz";
        var wide = SessionFixture.Scan($"<root><note>{huge}</note></root>");
        var before = GC.GetAllocatedBytesForCurrentThread();
        wide.Search(new TextSearch { FindWhat = "zz", MatchCase = false });
        wide.PumpAll();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(1_500_000, Assert.Single(wide.Hits).CharacterOffset);
        Assert.True(allocated < 1_000_000, $"allocated {allocated} bytes");

        before = GC.GetAllocatedBytesForCurrentThread();
        wide.Search(new TextSearch { FindWhat = "nope", EntireCell = true });
        wide.PumpAll();
        allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Empty(wide.Hits);
        Assert.True(allocated < 1_000_000, $"allocated {allocated} bytes");
    }

    [Fact]
    public void Element_names_attribute_names_and_values_are_separate_targets()
    {
        var session = SessionFixture.Scan("""
            <root xmlns:a="urn:ex" xmlns:b="urn:ex">
              <!-- secret -->
              <?pi secret?>
              <box>
                <title>hello</title>
              </box>
              <a:item id="id">item</a:item>
              <b:item>other</b:item>
              <note>note</note>
            </root>
            """);

        session.Search(new TextSearch { FindWhat = "secret" });
        session.PumpAll();
        Assert.Empty(session.Hits);

        session.Search(new TextSearch { FindWhat = "note", EntireCell = true });
        session.PumpAll();
        Assert.Equal(2, session.Hits.Count);
        Assert.All(session.Hits, hit => Assert.Equal("/root/note", session.FormatPath(hit.Path)));
        Assert.Equal("note", session.ReadHitText(session.Hits[0], 40).Text);
        Assert.Equal("note", session.ReadHitText(session.Hits[1], 40).Text);

        session.Search(new TextSearch { FindWhat = "note", EntireCell = true, ElementName = false });
        session.PumpAll();
        Assert.Single(session.Hits);

        session.Search(new TextSearch { FindWhat = "note", EntireCell = true, Value = false });
        session.PumpAll();
        Assert.Single(session.Hits);

        session.Search(new TextSearch
        {
            FindWhat = "note",
            ElementName = false,
            AttributeName = false,
            Value = false,
        });
        session.PumpAll();
        Assert.Empty(session.Hits);

        session.Search(new TextSearch { FindWhat = "not", Value = false });
        session.PumpAll();
        var noteName = Assert.Single(session.Hits);
        var noteText = session.ReadHitText(noteName, 40);
        Assert.Equal("note", noteText.Text);
        Assert.Equal(0, noteText.MarkStart);
        Assert.Equal(3, noteText.MarkLength);

        session.Search(new TextSearch { FindWhat = "id" });
        session.PumpAll();
        Assert.Equal(2, session.Hits.Count);
        Assert.Equal("/root/a:item[1]/@id", session.FormatPath(session.Hits[0].Path));
        Assert.Equal("/root/a:item[1]/@id", session.FormatPath(session.Hits[1].Path));
        var attrName = session.ReadHitText(session.Hits[0], 40);
        Assert.Equal("@id", attrName.Text);
        Assert.Equal(1, attrName.MarkStart);
        Assert.Equal(2, attrName.MarkLength);
        var attrValue = session.ReadHitText(session.Hits[1], 40);
        Assert.Equal("id", attrValue.Text);
        Assert.Equal(0, attrValue.MarkStart);

        session.Search(new TextSearch { FindWhat = "id", AttributeName = false });
        session.PumpAll();
        Assert.Equal("id", session.ReadHitText(Assert.Single(session.Hits), 40).Text);

        session.Search(new TextSearch { FindWhat = "@id", EntireCell = true });
        session.PumpAll();
        var wholeName = session.ReadHitText(Assert.Single(session.Hits), 40);
        Assert.Equal("@id", wholeName.Text);
        Assert.Equal(0, wholeName.MarkStart);
        Assert.Equal(3, wholeName.MarkLength);

        session.Search(new TextSearch { FindWhat = "id", EntireCell = true, ElementName = false });
        session.PumpAll();
        Assert.Equal("id", session.ReadHitText(Assert.Single(session.Hits), 40).Text);

        session.Search(new TextSearch { FindWhat = "id", StartsWith = true, ElementName = false, Value = false });
        session.PumpAll();
        Assert.Empty(session.Hits);

        session.Search(new TextSearch { FindWhat = "a:item", EntireCell = true, Value = false });
        session.PumpAll();
        Assert.Equal(2, session.Hits.Count);
        Assert.Equal("/root/a:item[1]", session.FormatPath(session.Hits[0].Path));
        Assert.Equal("/root/a:item[2]", session.FormatPath(session.Hits[1].Path));
        Assert.Equal("a:item", session.ReadHitText(session.Hits[0], 40).Text);
        Assert.Equal("a:item", session.ReadHitText(session.Hits[1], 40).Text);

        session.Search(new TextSearch { FindWhat = "b:item", EntireCell = true });
        session.PumpAll();
        Assert.Empty(session.Hits);

        session.Search(new TextSearch { FindWhat = "A:ITEM", EntireCell = true, MatchCase = false, Value = false });
        session.PumpAll();
        Assert.Equal(2, session.Hits.Count);
        Assert.Equal("a:item", session.ReadHitText(session.Hits[0], 40).Text);
    }

    [Fact]
    public void Revealing_a_name_lands_on_the_field_row_or_the_list_entry()
    {
        var xs = new string('x', 40);
        var session = SessionFixture.Scan($"""
            <root>
              <box>
                <title>hello</title>
              </box>
              <item>one</item>
              <item>two</item>
              <flag verylongattributenameprefix="{xs}">z</flag>
            </root>
            """);
        session.SetViewport(0, 0, 900, 800);

        session.Search(new TextSearch { FindWhat = "box", EntireCell = true, Value = false });
        session.PumpAll();
        var box = Assert.Single(session.Hits);
        Assert.Equal("/root/box", session.FormatPath(box.Path));
        Assert.Equal("box", session.ReadHitText(box, 40).Text);
        session.Reveal(box);
        Assert.Equal(box.Path, session.Marked);
        Assert.Contains(session.ReadSurface().Rows, row => row.IsMarked && row.Path.Equals(box.Path));

        session.Search(new TextSearch { FindWhat = "item", EntireCell = true, Value = false });
        session.PumpAll();
        Assert.Equal("/root/item[1]", session.FormatPath(session.Hits[0].Path));
        Assert.Equal("/root/item[2]", session.FormatPath(session.Hits[1].Path));
        session.Reveal(session.Hits[0]);
        Assert.True(session.Field("item").IsExpanded);
        Assert.Equal(session.Hits[0].Path, session.Marked);
        Assert.Contains(
            session.ReadSurface().Rows,
            row => row.IsMarked && row.Kind == SurfaceKind.EntryIndex && row.Path.Equals(session.Hits[0].Path));

        session.Search(new TextSearch { FindWhat = "prefix", ElementName = false, Value = false });
        session.PumpAll();
        var attr = Assert.Single(session.Hits);
        var shown = session.ReadHitText(attr, 40);
        Assert.Equal("prefix", shown.Text.Substring(shown.MarkStart, shown.MarkLength));
        Assert.DoesNotContain("xxxx", shown.Text, StringComparison.Ordinal);
        session.Reveal(attr);
        var scalar = session.ReadScalar(attr.Path);
        Assert.Equal(0, scalar.WindowStart);
        Assert.StartsWith("xxxx", scalar.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Field_name_limits_the_hit_to_the_displayed_name()
    {
        var session = SessionFixture.Scan("""
            <root xmlns:a="urn:ex" xmlns:b="urn:ex">
              <Extended_covering>
                <Start_node>RTM=123</Start_node>
              </Extended_covering>
              <Start_node id="x">RTM=123</Start_node>
              <a:item>one</a:item>
              <b:item>two</b:item>
              <b:note>two</b:note>
              <box id="2" extra="20"/>
              <pad>a </pad>
            </root>
            """);

        session.Search(new TextSearch { FindWhat = "123" });
        session.PumpAll();
        Assert.Equal(2, session.Hits.Count);

        session.Search(new TextSearch { FindWhat = "123", FieldName = "Start_node" });
        session.PumpAll();
        var simple = Assert.Single(session.Hits);
        Assert.Equal("/root/Extended_covering/Start_node", session.FormatPath(simple.Path));
        Assert.Equal(4, simple.CharacterOffset);

        session.Search(new TextSearch { FindWhat = "123", FieldName = "start_node" });
        session.PumpAll();
        Assert.Empty(session.Hits);

        session.Search(new TextSearch { FindWhat = "123", FieldName = "start_node", MatchCase = false });
        session.PumpAll();
        Assert.Equal("/root/Extended_covering/Start_node", session.FormatPath(Assert.Single(session.Hits).Path));

        session.Search(new TextSearch { FindWhat = "123", FieldName = "#text" });
        session.PumpAll();
        Assert.Equal("/root/Start_node/#text", session.FormatPath(Assert.Single(session.Hits).Path));

        session.Search(new TextSearch { FindWhat = "123", FieldName = "Start_node", EntireCell = true });
        session.PumpAll();
        Assert.Empty(session.Hits);

        session.Search(new TextSearch { FindWhat = "RTM=123", FieldName = "Start_node", EntireCell = true });
        session.PumpAll();
        Assert.Equal("/root/Extended_covering/Start_node", session.FormatPath(Assert.Single(session.Hits).Path));

        session.Search(new TextSearch { FindWhat = "one", FieldName = "#text", EntireCell = true });
        session.PumpAll();
        Assert.Equal("/root/a:item[1]/#text", session.FormatPath(Assert.Single(session.Hits).Path));

        session.Search(new TextSearch { FindWhat = "two", FieldName = "#text", EntireCell = true });
        session.PumpAll();
        Assert.Equal("/root/a:item[2]/#text", session.FormatPath(Assert.Single(session.Hits).Path));

        session.Search(new TextSearch { FindWhat = "two", FieldName = "b:note", EntireCell = true });
        session.PumpAll();
        Assert.Equal("/root/b:note", session.FormatPath(Assert.Single(session.Hits).Path));

        session.Search(new TextSearch { FindWhat = "two", FieldName = "a:note", EntireCell = true });
        session.PumpAll();
        Assert.Empty(session.Hits);

        session.Search(new TextSearch { FindWhat = "2", FieldName = "@id", EntireCell = true });
        session.PumpAll();
        Assert.Equal("/root/box/@id", session.FormatPath(Assert.Single(session.Hits).Path));

        session.Search(new TextSearch { FindWhat = "2", FieldName = "id", EntireCell = true });
        session.PumpAll();
        Assert.Empty(session.Hits);

        session.Search(new TextSearch { FindWhat = "2", FieldName = "@ID", EntireCell = true, MatchCase = false });
        session.PumpAll();
        Assert.Equal("/root/box/@id", session.FormatPath(Assert.Single(session.Hits).Path));

        session.Search(new TextSearch { FindWhat = "a ", FieldName = "pad", EntireCell = true });
        session.PumpAll();
        Assert.Single(session.Hits);

        session.Search(new TextSearch { FindWhat = "a", FieldName = "pad", EntireCell = true });
        session.PumpAll();
        Assert.Empty(session.Hits);

        var plain = SessionFixture.Scan("""<root xmlns="urn:ex"><item>one</item></root>""");
        plain.Search(new TextSearch { FindWhat = "one", FieldName = "item", EntireCell = true });
        plain.PumpAll();
        Assert.Equal("/root/item", plain.FormatPath(Assert.Single(plain.Hits).Path));
    }

    [Fact]
    public void Parent_grandfather_and_parent_attribute_narrow_the_hit()
    {
        var session = SessionFixture.Scan("""
            <root>
              <Extended_covering id="keep">
                <Start_node>RTM=AAA</Start_node>
              </Extended_covering>
              <Extended_covering>
                <Start_node>RTM=BBB</Start_node>
              </Extended_covering>
              <Harness>
                <Extended_covering>
                  <Start_node>RTM=CCC</Start_node>
                </Extended_covering>
              </Harness>
              <Start_node id="abc">RTM=DDD</Start_node>
              <Loose>RTM=EEE</Loose>
            </root>
            """);

        session.Search(new TextSearch { FindWhat = "AAA", ParentElementName = "Extended_covering" });
        session.PumpAll();
        Assert.Equal("/root/Extended_covering[1]/Start_node", session.FormatPath(Assert.Single(session.Hits).Path));

        session.Search(new TextSearch { FindWhat = "AAA", ParentElementName = "Start_node" });
        session.PumpAll();
        Assert.Empty(session.Hits);

        session.Search(new TextSearch { FindWhat = "DDD", ParentElementName = "Start_node" });
        session.PumpAll();
        Assert.Equal("/root/Start_node/#text", session.FormatPath(Assert.Single(session.Hits).Path));

        session.Search(new TextSearch { FindWhat = "RTM", ParentElementName = "Extended_covering" });
        session.PumpAll();
        Assert.Equal(3, session.Hits.Count);
        Assert.Contains(session.Hits, hit => session.FormatPath(hit.Path) == "/root/Extended_covering[1]/Start_node");
        Assert.Contains(session.Hits, hit => session.FormatPath(hit.Path) == "/root/Extended_covering[2]/Start_node");
        Assert.Contains(session.Hits, hit => session.FormatPath(hit.Path) == "/root/Harness/Extended_covering/Start_node");

        session.Search(new TextSearch { FindWhat = "AAA", ParentElementName = "extended_covering" });
        session.PumpAll();
        Assert.Empty(session.Hits);

        session.Search(new TextSearch { FindWhat = "AAA", ParentElementName = "extended_covering", MatchCase = false });
        session.PumpAll();
        Assert.Single(session.Hits);

        session.Search(new TextSearch
        {
            FindWhat = "AAA",
            ParentElementName = "Extended_covering",
            GrandfatherElementName = "root",
        });
        session.PumpAll();
        Assert.Equal("/root/Extended_covering[1]/Start_node", session.FormatPath(Assert.Single(session.Hits).Path));

        session.Search(new TextSearch { FindWhat = "CCC", GrandfatherElementName = "root" });
        session.PumpAll();
        Assert.Empty(session.Hits);

        session.Search(new TextSearch { FindWhat = "CCC", GrandfatherElementName = "Harness" });
        session.PumpAll();
        Assert.Equal("/root/Harness/Extended_covering/Start_node", session.FormatPath(Assert.Single(session.Hits).Path));

        session.Search(new TextSearch { FindWhat = "EEE", GrandfatherElementName = "root" });
        session.PumpAll();
        Assert.Empty(session.Hits);

        session.Search(new TextSearch { FindWhat = "EEE", ParentElementName = "root" });
        session.PumpAll();
        Assert.Equal("/root/Loose", session.FormatPath(Assert.Single(session.Hits).Path));

        session.Search(new TextSearch { FindWhat = "AAA", ParentElementName = "Missing" });
        session.PumpAll();
        Assert.Empty(session.Hits);

        session.Search(new TextSearch { FindWhat = "AAA", GrandfatherElementName = "Missing" });
        session.PumpAll();
        Assert.Empty(session.Hits);

        session.Search(new TextSearch { FindWhat = "AAA", ParentAttributeName = "@id" });
        session.PumpAll();
        Assert.Equal("/root/Extended_covering[1]/Start_node", session.FormatPath(Assert.Single(session.Hits).Path));

        session.Search(new TextSearch { FindWhat = "BBB", ParentAttributeName = "@id" });
        session.PumpAll();
        Assert.Empty(session.Hits);

        session.Search(new TextSearch { FindWhat = "AAA", ParentAttributeName = "id" });
        session.PumpAll();
        Assert.Empty(session.Hits);

        session.Search(new TextSearch { FindWhat = "AAA", ParentAttributeName = "@ID" });
        session.PumpAll();
        Assert.Empty(session.Hits);

        session.Search(new TextSearch { FindWhat = "AAA", ParentAttributeName = "@ID", MatchCase = false });
        session.PumpAll();
        Assert.Single(session.Hits);

        session.Search(new TextSearch { FindWhat = "AAA" });
        session.PumpAll();
        Assert.Single(session.Hits);
    }

    [Fact]
    public void A_promoted_simple_value_uses_the_text_field_parent()
    {
        var session = SessionFixture.Scan("""
            <root id="keep">
              <item>AAA</item>
              <item>BBB</item>
            </root>
            """);

        session.Search(new TextSearch { FindWhat = "AAA", ParentElementName = "item" });
        session.PumpAll();
        Assert.Equal("/root/item[1]/#text", session.FormatPath(Assert.Single(session.Hits).Path));

        session.Search(new TextSearch { FindWhat = "AAA", ParentElementName = "root" });
        session.PumpAll();
        Assert.Empty(session.Hits);

        session.Search(new TextSearch { FindWhat = "AAA", ParentAttributeName = "@id" });
        session.PumpAll();
        Assert.Empty(session.Hits);

        session.Search(new TextSearch
        {
            FindWhat = "AAA",
            ParentElementName = "item",
            GrandfatherElementName = "root",
        });
        session.PumpAll();
        Assert.Equal("/root/item[1]/#text", session.FormatPath(Assert.Single(session.Hits).Path));

        session.Search(new TextSearch { FindWhat = "BBB", ParentElementName = "item" });
        session.PumpAll();
        Assert.Equal("/root/item[2]/#text", session.FormatPath(Assert.Single(session.Hits).Path));
    }

    [Fact]
    public void A_list_inside_one_entry_does_not_rewrite_an_earlier_single_field()
    {
        var session = SessionFixture.Scan("""
            <root>
              <item><note>KEEP</note></item>
              <item><note>a</note><note>KEEP</note></item>
            </root>
            """);

        session.Search(new TextSearch { FindWhat = "KEEP" });
        session.PumpAll();

        Assert.Equal(
            ["/root/item[1]/note", "/root/item[2]/note[2]/#text"],
            session.Hits.Select(hit => session.FormatPath(hit.Path)).ToArray());
    }
}
