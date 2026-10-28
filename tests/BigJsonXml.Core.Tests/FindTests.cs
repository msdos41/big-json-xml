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

    [Fact]
    public void Hit_text_stays_readable_while_a_later_sibling_repeats_the_list()
    {
        var xml = new System.Text.StringBuilder();
        xml.Append("<root>");
        for (var item = 0; item < 2; item++)
        {
            xml.Append("<item>");
            for (var row = 0; row < 80; row++)
            {
                var mark = (item == 0 && row == 60) || (item == 1 && row == 3) ? "123" : "0";
                xml.Append("<row><cell v=\"").Append(mark).Append("\">x</cell></row>");
            }

            xml.Append("</item>");
        }

        xml.Append("</root>");
        var source = new MemoryByteSource();
        source.Append(System.Text.Encoding.UTF8.GetBytes(xml.ToString()));
        source.Complete();
        var session = ViewerSession.Open(source);
        session.Search(new TextSearch { FindWhat = "123" });
        var seen = 0;
        for (var step = 0; step < 100_000; step++)
        {
            var more = session.Pump(24);
            for (; seen < session.Hits.Count; seen++)
            {
                var text = session.ReadHitText(session.Hits[seen], 20);
                Assert.Contains("123", text.Text, StringComparison.Ordinal);
            }

            if (!more && session.Progress != ScanProgress.Scanning)
                break;
        }

        Assert.Equal(2, session.Hits.Count);
        session.Reveal(session.Hits[0]);
        session.Reveal(session.Hits[1]);
        Assert.Contains("123", session.ReadHitText(session.Hits[0], 20).Text, StringComparison.Ordinal);
        Assert.Contains("123", session.ReadHitText(session.Hits[1], 20).Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_published_hit_stays_when_a_later_sibling_makes_a_list()
    {
        var source = new MemoryByteSource();
        source.Append("<root><item>alpha</item><item>beta</item><note>alpha</note></root>");
        source.Complete();
        var session = ViewerSession.Open(source);
        session.Search(new TextSearch { FindWhat = "alpha", StartsWith = true });
        var seen = new List<Hit>();
        for (var step = 0; step < 10_000; step++)
        {
            var more = session.Pump(1);
            // Every hit already shown keeps its place and its identity.
            for (var i = 0; i < seen.Count; i++)
                Assert.Same(seen[i], session.Hits[i]);
            for (var i = seen.Count; i < session.Hits.Count; i++)
                seen.Add(session.Hits[i]);
            if (!more && session.Progress != ScanProgress.Scanning)
                break;
        }

        Assert.Equal(
            ["/root/item[1]/#text", "/root/note"],
            session.Hits.Select(hit => session.FormatPath(hit.Path)).ToArray());
    }

    [Fact]
    public void Hit_text_stays_readable_when_the_scan_drains_one_sibling_at_a_time()
    {
        var xml = new System.Text.StringBuilder();
        xml.Append("<root>");
        for (var item = 0; item < 3; item++)
        {
            xml.Append("<item>");
            for (var row = 0; row < 80; row++)
            {
                var mark = (item == 0 && row == 60) || (item == 1 && row == 3) || (item == 2 && row == 79) ? "123" : "0";
                xml.Append("<row><cell v=\"").Append(mark).Append("\">x</cell></row>");
            }

            xml.Append("</item>");
        }

        xml.Append("</root>");
        var bytes = System.Text.Encoding.UTF8.GetBytes(xml.ToString());
        var source = new MemoryByteSource();
        source.Append(bytes);
        source.Complete();
        var session = ViewerSession.Open(source);
        // A slow machine: each drain of repeated siblings stops after one entry.
        session.FlatDrainTicks = 0;
        session.Search(new TextSearch { FindWhat = "123" });
        var seen = 0;
        for (var step = 0; step < 100_000; step++)
        {
            var more = session.Pump(24);
            for (; seen < session.Hits.Count; seen++)
            {
                var text = session.ReadHitText(session.Hits[seen], 20);
                Assert.Contains("123", text.Text, StringComparison.Ordinal);
            }

            if (!more && session.Progress != ScanProgress.Scanning)
                break;
        }

        var whole = new MemoryByteSource();
        whole.Append(bytes);
        whole.Complete();
        var reference = ViewerSession.Open(whole);
        reference.Search(new TextSearch { FindWhat = "123" });
        reference.PumpAll();

        string[] expected = ["/root/item[1]/row[61]/cell/@v", "/root/item[2]/row[4]/cell/@v", "/root/item[3]/row[80]/cell/@v"];
        Assert.Equal(expected, reference.Hits.Select(hit => reference.FormatPath(hit.Path)).ToArray());
        Assert.Equal(expected, session.Hits.Select(hit => session.FormatPath(hit.Path)).ToArray());
        foreach (var hit in session.Hits)
        {
            session.Reveal(hit);
            Assert.Contains("123", session.ReadHitText(hit, 20).Text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Hit_text_stays_readable_when_a_simple_field_becomes_a_list()
    {
        var xml = new System.Text.StringBuilder();
        xml.Append("<root>");
        for (var i = 0; i < 40; i++)
            xml.Append("<item>123</item>");
        xml.Append("</root>");
        var source = new MemoryByteSource();
        source.Append(System.Text.Encoding.UTF8.GetBytes(xml.ToString()));
        source.Complete();
        var session = ViewerSession.Open(source);
        for (var step = 0; step < 100_000 && session.Progress == ScanProgress.Scanning; step++)
            session.Pump(32);

        var item = Assert.Single(session.ReadRecord(DocumentPath.Root).Fields, field => field.Name == "item");
        Assert.Equal(FieldKind.List, item.Kind);

        session.Search(new TextSearch { FindWhat = "123" });
        var seen = 0;
        for (var step = 0; step < 100_000; step++)
        {
            var more = session.Pump(1);
            for (; seen < session.Hits.Count; seen++)
            {
                var text = session.ReadHitText(session.Hits[seen], 20);
                Assert.Contains("123", text.Text, StringComparison.Ordinal);
            }

            if (!more && session.Progress != ScanProgress.Scanning)
                break;
        }

        Assert.Equal(40, session.Hits.Count);
    }

    [Fact]
    public void Cancel_search_keeps_hits_already_found_and_lets_the_scan_finish()
    {
        var source = new MemoryByteSource();
        source.Append("<root><item>123</item><item>nope</item><item>123</item></root>");
        source.Complete();
        var session = ViewerSession.Open(source);
        session.Search(new TextSearch { FindWhat = "123", MatchCase = false });
        for (var step = 0; step < 100_000 && session.Hits.Count < 1; step++)
            Assert.True(session.Pump(1));

        Assert.Single(session.Hits);
        session.CancelSearch();
        session.PumpAll();
        Assert.Single(session.Hits);
        Assert.Equal(ScanProgress.Completed, session.Progress);
    }
}
