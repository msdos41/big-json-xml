namespace BigJsonXml.Tests;

public class OneRightEdgeTests
{
    [Fact]
    public void A_record_name_or_value_stops_at_32_and_a_wide_table_has_no_maximum()
    {
        var session = SessionFixture.Scan("<root><note>hello</note></root>");
        session.SetRecordNameWidth(DocumentPath.Root, 1);
        session.SetRecordValueWidth(DocumentPath.Root, 1);
        var frame = Show(session);
        Assert.All(frame.Rows.Where(row => row.Kind == SurfaceKind.FieldName), row => Assert.Equal(32, row.Width));
        Assert.All(frame.Rows.Where(row => row.Kind == SurfaceKind.Field), row => Assert.Equal(32, row.Width));

        session.SetRecordValueWidth(DocumentPath.Root, 4000);
        var wide = Show(session, 500, 400);
        Assert.Equal(4000, wide.Rows.Single(row => row.Kind == SurfaceKind.Field).Width);
        Assert.True(wide.ContentWidth >= 32 + 4000);
        Assert.Equal(1, wide.ScrollSurfaces);
    }

    [Fact]
    public void An_embedded_record_stays_on_the_value_edge()
    {
        var body = new string('m', 80);
        var xml = $"<root><meta><id>1</id><name>Ada</name></meta><note>{body}</note></root>";
        var path = Write(xml);
        var before = File.ReadAllBytes(path);
        using var session = Open(path);
        var meta = DocumentPath.Root.Field("meta");
        var note = DocumentPath.Root.Field("note");
        var id = meta.Field("id");

        session.Expand(meta);
        var opened = Show(session);
        var metaCell = Field(opened, meta);
        var idName = Name(opened, id);
        var idValue = Field(opened, id);
        Assert.Equal(424, metaCell.Width);
        Assert.Equal(metaCell.Width, Field(opened, note).Width);
        Assert.False(Field(opened, note).IsExpanded);
        Assert.Equal(200, idName.Width);
        Assert.Equal(200, idValue.Width);
        SameEdge(metaCell, idValue);
        Assert.True(idValue.X >= metaCell.X + ViewerSession.ExpandGutter);

        session.SetRecordValueWidth(DocumentPath.Root, 600);
        var wider = Show(session);
        metaCell = Field(wider, meta);
        idValue = Field(wider, id);
        Assert.Equal(600, metaCell.Width);
        Assert.Equal(200, Name(wider, id).Width);
        Assert.Equal(376, idValue.Width);
        SameEdge(metaCell, idValue);
        Assert.Equal(75, session.ReadScalar(note).Text!.Length);
        Assert.True(wider.ContentWidth >= 800);
        Assert.Equal(1, wider.ScrollSurfaces);

        session.SetRecordValueWidth(meta, 76);
        var narrower = Show(session);
        metaCell = Field(narrower, meta);
        idValue = Field(narrower, id);
        Assert.Equal(300, metaCell.Width);
        Assert.Equal(200, Name(narrower, id).Width);
        Assert.Equal(76, idValue.Width);
        SameEdge(metaCell, idValue);

        session.SetRecordValueWidth(DocumentPath.Root, 40);
        var floored = Show(session);
        metaCell = Field(floored, meta);
        Assert.Equal(256, metaCell.Width);
        Assert.Equal(200, Name(floored, id).Width);
        Assert.Equal(32, Field(floored, id).Width);
        SameEdge(metaCell, Field(floored, id));
        Assert.Equal(32, session.ReadScalar(note).Text!.Length);

        session.ExpandFullValue(note);
        var wrapped = Show(session, 1200, 4000).ContentHeight;
        session.SetRecordValueWidth(DocumentPath.Root, 600);
        Assert.True(Show(session, 1200, 4000).ContentHeight < wrapped);
        session.CollapseFullValue(note);

        session.Collapse(meta);
        Assert.Equal(200, Field(Show(session), meta).Width);
        session.Expand(meta);
        Assert.Equal(600, Field(Show(session), meta).Width);
        Assert.Equal(376, Field(Show(session), id).Width);
        Assert.Equal(before, File.ReadAllBytes(path));

        var other = SessionFixture.Scan(xml);
        other.Expand(meta);
        other.SetRecordValueWidth(DocumentPath.Root, 600);
        var fromHost = Show(other);
        Assert.Equal(600, Field(fromHost, meta).Width);
        Assert.Equal(376, Field(fromHost, id).Width);
        SameEdge(Field(fromHost, meta), Field(fromHost, id));
    }

    [Fact]
    public void A_host_that_was_already_wide_fills_and_then_forgets_a_narrower_drag()
    {
        var session = SessionFixture.Scan("<root><meta><id>1</id><name>Ada</name></meta><note>hi</note></root>");
        var meta = DocumentPath.Root.Field("meta");
        var id = meta.Field("id");
        session.SetRecordValueWidth(DocumentPath.Root, 3000);
        session.Expand(meta);
        var filled = Show(session);
        var metaCell = Field(filled, meta);
        var idValue = Field(filled, id);
        Assert.Equal(3000, metaCell.Width);
        Assert.Equal(200, Name(filled, id).Width);
        Assert.Equal(2776, idValue.Width);
        SameEdge(metaCell, idValue);

        session.Collapse(meta);
        session.SetRecordValueWidth(DocumentPath.Root, 200);
        session.Expand(meta);
        Assert.Equal(424, Field(Show(session), meta).Width);
        Assert.Equal(200, Field(Show(session), id).Width);

        session.Collapse(meta);
        session.SetRecordValueWidth(DocumentPath.Root, 3000);
        session.Expand(meta);
        session.SetRecordValueWidth(DocumentPath.Root, 500);
        var dragged = Show(session);
        Assert.Equal(500, Field(dragged, meta).Width);
        Assert.Equal(276, Field(dragged, id).Width);
        SameEdge(Field(dragged, meta), Field(dragged, id));
        session.Collapse(meta);
        Assert.Equal(3000, Field(Show(session), meta).Width);
        session.Expand(meta);
        var again = Show(session);
        Assert.Equal(3000, Field(again, meta).Width);
        Assert.Equal(2776, Field(again, id).Width);
        SameEdge(Field(again, meta), Field(again, id));
    }

    [Fact]
    public void A_json_record_uses_the_same_edge()
    {
        var session = SessionFixture.Scan("""{"meta":{"id":"1","name":"Ada"},"note":"hi"}""");
        var meta = DocumentPath.Root.Field("meta");
        session.Expand(meta);
        var opened = Show(session);
        var metaCell = Field(opened, meta);
        var idValue = Field(opened, meta.Field("id"));
        Assert.Equal(424, metaCell.Width);
        SameEdge(metaCell, idValue);

        session.SetRecordValueWidth(DocumentPath.Root, 600);
        var wider = Show(session);
        Assert.Equal(600, Field(wider, meta).Width);
        Assert.Equal(200, Name(wider, meta.Field("id")).Width);
        Assert.Equal(376, Field(wider, meta.Field("id")).Width);
        SameEdge(Field(wider, meta), Field(wider, meta.Field("id")));
        Assert.Equal(1, wider.ScrollSurfaces);
    }

    [Fact]
    public void An_embedded_list_stays_on_the_column_edge()
    {
        var text = new string('b', 80);
        var xml = $"""
            <root>
              <item><child><a>1</a><b>{text}</b></child><child><a>2</a><b>q</b></child></item>
              <item><child><a>3</a><b>z</b></child></item>
              <tail>t</tail>
            </root>
            """;
        var path = Write(xml);
        var before = File.ReadAllBytes(path);
        using var session = Open(path);
        var list = DocumentPath.Root.Field("item");
        var kids = list.WithIndex(1).Field("child");
        var scalar = kids.WithIndex(1).Field("b");
        session.Expand(list);
        session.SetColumnWidth(list, "child", 180);
        session.Expand(kids);

        var opened = Show(session);
        var cell = Entry(opened, kids);
        var index = opened.Rows.Single(row => row.Kind == SurfaceKind.EntryIndex && row.Path.Equals(kids.WithIndex(1)));
        var a = Header(opened, kids, "a");
        var b = Header(opened, kids, "b");
        Assert.Equal(440, cell.Width);
        Assert.Equal(ViewerSession.IndexTrackWidth(2), index.Width);
        Assert.Equal(200, a.Width);
        Assert.Equal(200, b.Width);
        SameEdge(cell, b);
        Assert.True(a.X > cell.X);

        session.SetColumnWidth(list, "child", 600);
        var wider = Show(session);
        cell = Entry(wider, kids);
        Assert.Equal(600, cell.Width);
        Assert.Equal(200, Header(wider, kids, "a").Width);
        Assert.Equal(360, Header(wider, kids, "b").Width);
        Assert.Equal(ViewerSession.IndexTrackWidth(2), wider.Rows.Single(row => row.Kind == SurfaceKind.EntryIndex && row.Path.Equals(kids.WithIndex(1))).Width);
        SameEdge(cell, Header(wider, kids, "b"));
        Assert.Equal(45, session.ReadScalar(scalar).Text!.Length);
        Assert.True(wider.ContentWidth >= cell.X + cell.Width);
        Assert.Equal(1, wider.ScrollSurfaces);

        session.SetColumnWidth(kids, "b", 60);
        var narrower = Show(session);
        Assert.Equal(300, Entry(narrower, kids).Width);
        Assert.Equal(200, Header(narrower, kids, "a").Width);
        Assert.Equal(60, Header(narrower, kids, "b").Width);
        SameEdge(Entry(narrower, kids), Header(narrower, kids, "b"));

        session.Collapse(kids);
        Assert.Equal(180, Header(Show(session), list, "child").Width);
        session.Expand(kids);
        Assert.Equal(300, Entry(Show(session), kids).Width);
        Assert.Equal(before, File.ReadAllBytes(path));

        var match = SessionFixture.Scan(xml);
        match.Expand(list);
        match.Expand(kids);
        match.SetColumnWidth(kids, "b", 360);
        var fromColumn = Show(match);
        Assert.Equal(600, Entry(fromColumn, kids).Width);
        Assert.Equal(360, Header(fromColumn, kids, "b").Width);
        SameEdge(Entry(fromColumn, kids), Header(fromColumn, kids, "b"));
    }

    [Fact]
    public void A_single_column_list_stops_at_the_index_and_32()
    {
        var session = SessionFixture.Scan("""
            <root>
              <item><child><a>1</a></child><child><a>2</a></child></item>
              <item><child><a>3</a></child></item>
              <tail>t</tail>
            </root>
            """);
        var list = DocumentPath.Root.Field("item");
        var kids = list.WithIndex(1).Field("child");
        session.Expand(list);
        session.Expand(kids);
        var floor = ViewerSession.ExpandGutter + ViewerSession.IndexTrackWidth(2) + ViewerSession.MinWidth;
        session.SetColumnWidth(list, "child", 40);
        var frame = Show(session);
        var cell = Entry(frame, kids);
        Assert.Equal(floor, cell.Width);
        Assert.Equal(32, Header(frame, kids, "a").Width);
        SameEdge(cell, Header(frame, kids, "a"));
        Assert.Equal(1, frame.ScrollSurfaces);
    }

    [Fact]
    public void A_json_list_uses_the_same_edge()
    {
        var session = SessionFixture.Scan("""{"rows":[{"a":"1","b":"xx"},{"a":"2","b":"yy"}],"note":1}""");
        var list = DocumentPath.Root.Field("rows");
        session.Expand(list);
        var opened = Show(session);
        var cell = Field(opened, list);
        var edge = Header(opened, list, "b");
        Assert.Equal(440, cell.Width);
        Assert.Equal(200, Header(opened, list, "a").Width);
        SameEdge(cell, edge);

        session.SetColumnWidth(list, "b", 360);
        var wider = Show(session);
        Assert.Equal(600, Field(wider, list).Width);
        Assert.Equal(200, Header(wider, list, "a").Width);
        Assert.Equal(360, Header(wider, list, "b").Width);
        SameEdge(Field(wider, list), Header(wider, list, "b"));
        Assert.Equal(1, wider.ScrollSurfaces);
    }

    [Fact]
    public void A_deeper_table_grows_every_ancestor_and_collapse_drops_only_that_growth()
    {
        var session = SessionFixture.Scan("""
            <root>
              <mid>
                <inner><id>1</id><name>Ada</name></inner>
                <side>hi</side>
              </mid>
              <note>zz</note>
            </root>
            """);
        var mid = DocumentPath.Root.Field("mid");
        var inner = mid.Field("inner");
        var side = mid.Field("side");
        var note = DocumentPath.Root.Field("note");
        session.Expand(mid);
        session.Expand(inner);
        var grown = Show(session);
        var midCell = Field(grown, mid);
        var innerCell = Field(grown, inner);
        var innerValue = Field(grown, inner.Field("id"));
        Assert.Equal(648, midCell.Width);
        Assert.Equal(424, innerCell.Width);
        Assert.Equal(200, innerValue.Width);
        Assert.Equal(200, Name(grown, inner.Field("id")).Width);
        SameEdge(midCell, innerCell);
        SameEdge(innerCell, innerValue);
        Assert.False(Field(grown, note).IsExpanded);
        Assert.Equal(midCell.Width, Field(grown, note).Width);
        Assert.False(Field(grown, side).IsExpanded);
        Assert.Equal(innerCell.Width, Field(grown, side).Width);
        Assert.Equal(1, grown.ScrollSurfaces);

        var wide = SessionFixture.Scan("""
            <root>
              <mid><inner><id>1</id><name>Ada</name></inner></mid>
            </root>
            """);
        wide.SetRecordValueWidth(DocumentPath.Root, 3000);
        wide.Expand(mid);
        wide.Expand(inner);
        Assert.Equal(3000, Field(Show(wide), mid).Width);

        var dragged = SessionFixture.Scan("""
            <root>
              <mid><inner><id>1</id><name>Ada</name></inner></mid>
            </root>
            """);
        dragged.Expand(mid);
        dragged.SetRecordValueWidth(DocumentPath.Root, 500);
        Assert.Equal(500, Field(Show(dragged), mid).Width);
        dragged.Expand(inner);
        Assert.Equal(648, Field(Show(dragged), mid).Width);
        dragged.Collapse(inner);
        Assert.Equal(500, Field(Show(dragged), mid).Width);

        dragged.Expand(inner);
        dragged.SetRecordValueWidth(DocumentPath.Root, 1);
        var floor = Show(dragged);
        Assert.Equal(480, Field(floor, mid).Width);
        Assert.Equal(200, Name(floor, mid).Width);
        Assert.Equal(200, Name(floor, inner).Width);
        Assert.Equal(32, Field(floor, inner.Field("id")).Width);
    }

    [Fact]
    public void A_neighbor_stretched_by_a_deeper_open_returns_when_that_table_closes()
    {
        var session = SessionFixture.Scan("""
            <root>
              <box>
                <small><a>1</a></small>
                <small><a>2</a></small>
                <big><a>1</a><b>2</b><c>3</c></big>
                <big><a>4</a><b>5</b><c>6</c></big>
              </box>
            </root>
            """);
        var box = DocumentPath.Root.Field("box");
        var small = box.Field("small");
        var big = box.Field("big");
        session.Expand(box);
        session.Expand(small);
        session.Expand(big);
        var both = Show(session);
        var smallHeader = Header(both, small, "a");
        Assert.Equal(640, Field(both, small).Width);
        Assert.Equal(600, smallHeader.Width);
        SameEdge(Field(both, small), Header(both, big, "c"));
        SameEdge(Field(both, small), smallHeader);

        session.Collapse(big);
        var left = Show(session);
        Assert.Equal(240, Field(left, small).Width);
        Assert.Equal(200, Header(left, small, "a").Width);
        Assert.Equal(1, left.ScrollSurfaces);
    }

    [Fact]
    public void Opening_inside_a_column_that_is_not_last_shifts_the_columns_on_its_right()
    {
        var session = SessionFixture.Scan("""
            <root>
              <box>
                <item>
                  <left><child><a>1</a><b>2</b></child><child><a>3</a><b>4</b></child></left>
                  <mid>m</mid>
                  <right>r</right>
                </item>
                <item>
                  <left><child><a>5</a><b>6</b></child></left>
                  <mid>n</mid>
                  <right>s</right>
                </item>
              </box>
              <note>hi</note>
            </root>
            """);
        var box = DocumentPath.Root.Field("box");
        var list = box.Field("item");
        var left = list.WithIndex(1).Field("left");
        session.Expand(box);
        session.Expand(list);
        var before = Show(session);
        var midBefore = Header(before, list, "mid");
        var rightBefore = Header(before, list, "right");
        Assert.Equal(200, midBefore.Width);
        Assert.Equal(200, rightBefore.Width);

        session.Expand(left);
        var after = Show(session);
        var mid = Header(after, list, "mid");
        var right = Header(after, list, "right");
        var leftHeader = Header(after, list, "left");
        Assert.Equal(440, leftHeader.Width);
        Assert.Equal(200, mid.Width);
        Assert.Equal(200, right.Width);
        Assert.Equal(leftHeader.X + leftHeader.Width, mid.X);
        Assert.Equal(mid.X + mid.Width, right.X);
        Assert.True(mid.X > midBefore.X);
        Assert.Equal(Field(after, box).Width, Field(after, DocumentPath.Root.Field("note")).Width);
        Assert.False(Field(after, DocumentPath.Root.Field("note")).IsExpanded);
        Assert.Equal("m", session.ReadScalar(list.WithIndex(1).Field("mid")).Text);
        Assert.Equal("r", session.ReadScalar(list.WithIndex(1).Field("right")).Text);
        Assert.Equal(1, after.ScrollSurfaces);
    }

    [Fact]
    public void A_left_divider_moves_every_edge_on_its_right()
    {
        var body = new string('m', 40);
        var session = SessionFixture.Scan($"<root><meta><id>1</id><name>Ada</name></meta><note>{body}</note></root>");
        var meta = DocumentPath.Root.Field("meta");
        var id = meta.Field("id");
        session.Expand(meta);
        session.SetRecordNameWidth(meta, 280);
        var wider = Show(session);
        Assert.Equal(280, Name(wider, id).Width);
        Assert.Equal(200, Field(wider, id).Width);
        Assert.Equal(504, Field(wider, meta).Width);
        SameEdge(Field(wider, meta), Field(wider, id));
        Assert.Equal(body.Length, session.ReadScalar(DocumentPath.Root.Field("note")).Text!.Length);

        session.SetRecordNameWidth(meta, 1);
        var floored = Show(session);
        Assert.Equal(32, Name(floored, id).Width);
        Assert.Equal(200, Field(floored, id).Width);
        Assert.Equal(256, Field(floored, meta).Width);
        SameEdge(Field(floored, meta), Field(floored, id));

        var held = SessionFixture.Scan("<root><meta><id>1</id><name>Ada</name></meta><note>hi</note></root>");
        held.SetRecordValueWidth(DocumentPath.Root, 3000);
        held.Expand(meta);
        held.SetRecordNameWidth(meta, 280);
        Assert.Equal(3080, Field(Show(held), meta).Width);
        SameEdge(Field(Show(held), meta), Field(Show(held), id));
        held.Collapse(meta);
        Assert.Equal(3000, Field(Show(held), meta).Width);
        held.Expand(meta);
        Assert.Equal(3000, Field(Show(held), meta).Width);
        Assert.Equal(1, Show(held).ScrollSurfaces);
    }

    [Fact]
    public void A_list_divider_keeps_the_columns_on_its_right_and_an_inner_edge_is_remembered()
    {
        var body = new string('q', 40);
        var session = SessionFixture.Scan($"""
            <root>
              <item><a>1</a><b>{body}</b><c>end</c></item>
              <item><a>2</a><b>z</b><c>end</c></item>
              <tail>t</tail>
            </root>
            """);
        var list = DocumentPath.Root.Field("item");
        session.Expand(list);
        var before = session.ReadScalar(list.WithIndex(1).Field("b")).Text!.Length;
        session.SetColumnWidth(list, "a", 280);
        var frame = Show(session);
        var a = Header(frame, list, "a");
        var b = Header(frame, list, "b");
        var c = Header(frame, list, "c");
        Assert.Equal(280, a.Width);
        Assert.Equal(200, b.Width);
        Assert.Equal(200, c.Width);
        Assert.Equal(a.X + a.Width, b.X);
        Assert.Equal(b.X + b.Width, c.X);
        Assert.Equal(before, session.ReadScalar(list.WithIndex(1).Field("b")).Text!.Length);
        Assert.Equal(1, frame.ScrollSurfaces);

        var nested = SessionFixture.Scan("""
            <root>
              <item>
                <left><id>1</id><name>Ada</name></left>
                <right>r</right>
              </item>
              <item>
                <left><id>2</id><name>Bea</name></left>
                <right>s</right>
              </item>
              <tail>t</tail>
            </root>
            """);
        var items = DocumentPath.Root.Field("item");
        var left = items.WithIndex(1).Field("left");
        nested.Expand(items);
        nested.SetColumnWidth(items, "left", 180);
        nested.Expand(left);
        nested.SetColumnWidth(items, "left", 500);
        var open = Show(nested);
        Assert.Equal(500, Entry(open, left).Width);
        Assert.Equal(200, Name(open, left.Field("id")).Width);
        Assert.Equal(276, Field(open, left.Field("id")).Width);
        SameEdge(Entry(open, left), Field(open, left.Field("id")));
        Assert.Equal(200, Header(open, items, "right").Width);
        nested.Collapse(left);
        Assert.Equal(180, Header(Show(nested), items, "left").Width);
        nested.Expand(left);
        Assert.Equal(500, Entry(Show(nested), left).Width);
    }

    [Fact]
    public void Two_open_cells_share_one_edge_until_the_wider_floor()
    {
        var session = SessionFixture.Scan("""
            <root>
              <box>
                <small><id>1</id><name>Ada</name></small>
                <big><a>1</a><b>2</b><c>3</c></big>
                <big><a>4</a><b>5</b><c>6</c></big>
              </box>
            </root>
            """);
        var box = DocumentPath.Root.Field("box");
        var small = box.Field("small");
        var big = box.Field("big");
        session.Expand(box);
        session.Expand(small);
        session.Expand(big);
        var both = Show(session);
        Assert.Equal(640, Field(both, small).Width);
        SameEdge(Field(both, small), Header(both, big, "c"));
        Assert.True(Field(both, small.Field("id")).Width > 200);

        session.Collapse(big);
        var onlySmall = Show(session);
        Assert.Equal(424, Field(onlySmall, small).Width);
        Assert.Equal(200, Field(onlySmall, small.Field("id")).Width);

        session.Expand(big);
        session.SetRecordNameWidth(small, 280);
        var pulled = Show(session);
        Assert.Equal(720, Field(pulled, small).Width);
        Assert.Equal(280, Name(pulled, small.Field("id")).Width);
        SameEdge(Field(pulled, small), Header(pulled, big, "c"));
        session.Collapse(big);
        session.Collapse(small);
        session.Expand(small);
        Assert.Equal(200, Field(Show(session), small.Field("id")).Width);
        session.Collapse(small);
        session.Expand(big);
        Assert.Equal(720, Field(Show(session), big).Width);

        var stopped = SessionFixture.Scan("""
            <root>
              <box>
                <small><id>1</id><name>Ada</name></small>
                <big><a>1</a><b>2</b><c>3</c></big>
                <big><a>4</a><b>5</b><c>6</c></big>
              </box>
            </root>
            """);
        stopped.Expand(box);
        stopped.SetColumnWidth(big, "a", 300);
        stopped.SetColumnWidth(big, "b", 300);
        stopped.SetColumnWidth(big, "c", 40);
        stopped.Expand(small);
        stopped.Expand(big);
        var shared = Field(Show(stopped), small).Width;
        stopped.SetRecordNameWidth(small, 32);
        var after = Show(stopped);
        Assert.Equal(32, Header(after, big, "c").Width);
        Assert.True(Name(after, small.Field("id")).Width > 32);
        Assert.True(Field(after, small).Width < shared);
        SameEdge(Field(after, small), Header(after, big, "c"));
        Assert.Equal(1, after.ScrollSurfaces);

        stopped.Collapse(small);
        var bigLeft = Field(Show(stopped), big).Width;
        stopped.Collapse(big);
        Assert.Equal(200, Field(Show(stopped), small).Width);
        Assert.True(200 < bigLeft);
    }

    private static void SameEdge(SurfaceRow cell, SurfaceRow edge) =>
        Assert.Equal(cell.X + cell.Width, edge.X + edge.Width, 2);

    private static SurfaceRow Field(SurfaceFrame frame, DocumentPath path) =>
        frame.Rows.Single(row => row.Kind == SurfaceKind.Field && row.Path.Equals(path));

    private static SurfaceRow Name(SurfaceFrame frame, DocumentPath path) =>
        frame.Rows.Single(row => row.Kind == SurfaceKind.FieldName && row.Path.Equals(path));

    private static SurfaceRow Entry(SurfaceFrame frame, DocumentPath path) =>
        frame.Rows.Single(row => row.Kind == SurfaceKind.Entry && row.Path.Equals(path) && row.IsExpanded);

    private static SurfaceRow Header(SurfaceFrame frame, DocumentPath table, string name) =>
        frame.Rows.Single(row => row.Kind == SurfaceKind.ColumnHeader && row.WidthTable!.Equals(table) && row.Name == name);

    private static SurfaceFrame Show(ViewerSession session, double width = 4000, double height = 1200)
    {
        session.SetViewport(0, 0, width, height);
        return session.ReadSurface();
    }

    private static ViewerSession Open(string path)
    {
        var session = ViewerSession.Open(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
        session.PumpAll();
        return session;
    }

    private static string Write(string xml)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".xml");
        File.WriteAllText(path, xml);
        return path;
    }
}
