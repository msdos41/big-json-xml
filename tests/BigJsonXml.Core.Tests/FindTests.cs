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
}
