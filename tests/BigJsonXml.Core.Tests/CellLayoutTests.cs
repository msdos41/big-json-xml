using System.Globalization;

namespace BigJsonXml.Tests;

public class CellLayoutTests
{
    private const string LongName = "thisnameisdefinitelylongerthantwentyfive";
    private const int LabelRoom = (int)((ViewerSession.DefaultWidth - ViewerSession.ExpandGutter) / ViewerSession.Advance);

    [Fact]
    public void Record_names_and_values_share_a_width_and_clip()
    {
        var session = SessionFixture.Scan(
            $"""<root id="1" xmlns:a="urn:ex"><name>Ada</name><a:title>prefixed</a:title><{LongName}>x</{LongName}><note>abcdefghijklmnopqrstuvwxyz</note>keep</root>""");
        var frame = Show(session);

        var names = frame.Rows.Where(row => row.Kind == SurfaceKind.FieldName).ToList();
        var values = frame.Rows.Where(row => row.Kind == SurfaceKind.Field).ToList();
        Assert.NotEmpty(names);
        Assert.Equal(names.Count, values.Count);
        Assert.All(names, row => Assert.Equal(200, row.Width));
        Assert.All(names, row => Assert.Equal(names[0].X, row.X));
        Assert.All(values, row => Assert.Equal(200, row.Width));
        Assert.All(values, row => Assert.Equal(names[0].X + 200, row.X));

        var ada = values.Single(row => row.Name == "name");
        Assert.Equal("Ada", ada.Value);
        Assert.False(ada.IsCutOff);
        var note = values.Single(row => row.Name == "note");
        Assert.Equal("abcdefghijklmnopqrstuvwxy", note.Value);
        Assert.True(note.IsCutOff);
        var longName = names.Single(row => row.Name == LongName);
        Assert.Equal(25, longName.Value!.Length);
        Assert.True(longName.IsCutOff);
        Assert.Equal(longName.X + longName.Width, values.Single(row => row.Name == LongName).X);

        Assert.Contains(values, row => row.Name == "@id" && row.Value == "1" && row.CanExpand == false);
        Assert.Contains(values, row => row.Name == "a:title" && row.Value == "prefixed");
        Assert.Contains(values, row => row.Name == "#text" && row.Value == "keep");
        Assert.Equal(1, frame.ScrollSurfaces);
    }

    [Fact]
    public void List_columns_line_up_under_their_headers()
    {
        var session = SessionFixture.Scan("""
            <root xmlns:a="urn:ex">
              <item id="1">abcdefghijklmnopqrstuvwxyz</item>
              <item id="2"><a:part>p</a:part></item>
              <item><gone>no</gone></item>
            </root>
            """);
        var list = DocumentPath.Root.Field("item");
        session.Expand(list);
        var frame = Show(session);

        var headers = frame.Rows.Where(row => row.Kind == SurfaceKind.ColumnHeader && row.WidthTable!.Equals(list)).ToList();
        Assert.Equal(["@id", "#text", "a:part", "gone"], headers.Select(row => row.Name).ToArray());
        Assert.All(headers, row => Assert.Equal(200, row.Width));
        Assert.All(headers, row => Assert.False(row.CanExpand));

        foreach (var header in headers)
        {
            var cells = frame.Rows.Where(row => row.Kind == SurfaceKind.Entry && row.Name == header.Name).ToList();
            Assert.Equal(3, cells.Count);
            Assert.All(cells, row => Assert.Equal(header.X, row.X));
            Assert.All(cells, row => Assert.Equal(header.Width, row.Width));
        }

        var part = headers.Single(row => row.Name == "a:part");
        var firstPart = frame.Rows.Single(row => row.Kind == SurfaceKind.Entry && row.Name == "a:part" && row.Y == frame.Rows.Single(index => index.Kind == SurfaceKind.EntryIndex && index.Name == "1").Y);
        Assert.Equal("", firstPart.Value);
        Assert.Equal(part.X, firstPart.X);
        var gone = headers.Single(row => row.Name == "gone");
        Assert.True(gone.X > part.X);

        var indexes = frame.Rows.Where(row => row.Kind == SurfaceKind.EntryIndex && row.Name.Length > 0).OrderBy(row => row.Y).ToList();
        Assert.Equal(["1", "2", "3"], indexes.Select(row => row.Name).ToArray());
        Assert.Equal(2 * ViewerSession.Advance, ViewerSession.IndexTrackWidth(3));
        Assert.All(indexes, row => Assert.Equal(ViewerSession.IndexTrackWidth(3), row.Width));
        Assert.All(indexes, row => Assert.True(row.X < headers[0].X));
        Assert.All(indexes, row => Assert.False(row.CanExpand));
        Assert.DoesNotContain(frame.Rows, row => row.Kind == SurfaceKind.ColumnHeader && row.X == indexes[0].X);

        var longText = frame.Rows.Single(row => row.Kind == SurfaceKind.Entry && row.Name == "#text" && row.IsCutOff);
        Assert.Equal(25, longText.Value!.Length);
        Assert.DoesNotContain(frame.Rows, row => row.Value != null && row.Value.Contains('|', StringComparison.Ordinal));
    }

    [Fact]
    public void A_field_found_later_is_appended_while_the_scan_is_running()
    {
        var source = new MemoryByteSource();
        var session = ViewerSession.Open(source);
        source.Append("<root><item><a>1</a></item><item><a>2</a></item>");
        session.PumpAll();
        var list = DocumentPath.Root.Field("item");
        session.Expand(list);
        var before = Show(session);
        var header = before.Rows.Single(row => row.Kind == SurfaceKind.ColumnHeader);
        Assert.Equal("a", header.Name);

        source.Append("<item><b>3</b></item>");
        session.PumpAll();
        Assert.Equal(ScanProgress.Scanning, session.Progress);
        var during = Show(session);
        var headers = during.Rows.Where(row => row.Kind == SurfaceKind.ColumnHeader && row.WidthTable!.Equals(list)).ToList();
        Assert.Equal(["a", "b"], headers.Select(row => row.Name).ToArray());
        Assert.Equal(header.X, headers[0].X);
        Assert.True(headers[1].X > headers[0].X);
        Assert.Contains(during.Rows, row => row.Kind == SurfaceKind.Entry && row.Name == "b" && row.Value == "");
    }

    [Fact]
    public void A_dragged_width_belongs_to_that_table_for_this_opening()
    {
        const string wide = "descriptionofthefieldthatisverylong";
        var xml = $"""
            <root>
              <left><item><n>one</n><{wide}>d</{wide}></item><item><n>two</n></item></left>
              <right><item><n>three</n><{wide}>e</{wide}></item><item><n>four</n></item></right>
            </root>
            """;
        var path = Write(xml);
        var before = File.ReadAllBytes(path);
        using var session = Open(path);
        var left = DocumentPath.Root.Field("left").Field("item");
        var right = DocumentPath.Root.Field("right").Field("item");
        session.Expand(DocumentPath.Root.Field("left"));
        session.Expand(DocumentPath.Root.Field("right"));
        session.Expand(left);
        session.Expand(right);

        var indexes = Show(session).Rows.Where(row => row.Kind == SurfaceKind.EntryIndex && row.WidthTable is null && row.Name.Length > 0).Select(row => row.Width).Distinct().ToList();
        session.SetColumnWidth(left, wide, 280);
        session.SetColumnWidth(left, "n", 1);
        var frame = Show(session);

        var leftHeader = frame.Rows.Single(row => row.Kind == SurfaceKind.ColumnHeader && row.WidthTable!.Equals(left) && row.Name == wide);
        var rightHeader = frame.Rows.Single(row => row.Kind == SurfaceKind.ColumnHeader && row.WidthTable!.Equals(right) && row.Name == wide);
        Assert.Equal(280, leftHeader.Width);
        Assert.Equal(200, rightHeader.Width);
        Assert.Equal(8, frame.Rows.Single(row => row.Kind == SurfaceKind.ColumnHeader && row.WidthTable!.Equals(left) && row.Name == "n").Width);
        Assert.All(frame.Rows.Where(row => row.Kind == SurfaceKind.Entry && row.WidthTable!.Equals(left) && row.Name == wide), row => Assert.Equal(leftHeader.X, row.X));
        Assert.All(frame.Rows.Where(row => row.Kind == SurfaceKind.Entry && row.WidthTable!.Equals(left) && row.Name == wide), row => Assert.Equal(280, row.Width));
        Assert.Equal(indexes, frame.Rows.Where(row => row.Kind == SurfaceKind.EntryIndex && row.Name.Length > 0).Select(row => row.Width).Distinct().ToList());
        Assert.All(frame.Rows.Where(row => row.Kind == SurfaceKind.ColumnHeader), row => Assert.False(row.CanExpand));
        Assert.All(frame.Rows.Where(row => row.Kind == SurfaceKind.EntryIndex), row => Assert.False(row.CanExpand));
        session.SetRecordNameWidth(DocumentPath.Root, 10);
        Assert.All(Show(session).Rows.Where(row => row.Kind == SurfaceKind.FieldName && row.WidthTable!.Equals(DocumentPath.Root)), row => Assert.Equal(32, row.Width));

        session.SetColumnWidth(left, wide, 32);
        var clipped = Show(session).Rows.Single(row => row.Kind == SurfaceKind.ColumnHeader && row.WidthTable!.Equals(left) && row.Name == wide);
        Assert.Equal(32, clipped.Width);
        Assert.True(clipped.IsCutOff);
        Assert.Equal(4, clipped.Value!.Length);

        session.SetViewport(0, 50_000, 400, 40);
        _ = session.ReadSurface();
        session.SetViewport(0, 0, 1100, 800);
        Assert.Equal(32, session.ReadSurface().Rows.Single(row => row.Kind == SurfaceKind.ColumnHeader && row.WidthTable!.Equals(left) && row.Name == wide).Width);

        session.SetRecordNameWidth(DocumentPath.Root, 240);
        session.SetRecordValueWidth(DocumentPath.Root, 3000);
        var wideFrame = Show(session, 400, 400);
        Assert.True(wideFrame.ContentWidth >= 240 + 3000);
        session.SetViewport(2800, 0, 400, 400);
        var scrolled = session.ReadSurface();
        Assert.Contains(scrolled.Rows, row => row.Kind == SurfaceKind.Field && row.X + row.Width > 3000);
        Assert.DoesNotContain(scrolled.Rows, row => row.Kind == SurfaceKind.FieldName && row.WidthTable!.Equals(DocumentPath.Root));
        Assert.Equal(before, File.ReadAllBytes(path));

        using var again = Open(path);
        again.Expand(DocumentPath.Root.Field("left"));
        again.Expand(left);
        Assert.Equal(200, Show(again).Rows.Single(row => row.Kind == SurfaceKind.ColumnHeader && row.Name == wide).Width);
        Assert.All(Show(again).Rows.Where(row => row.Kind == SurfaceKind.FieldName), row => Assert.Equal(200, row.Width));
    }

    [Fact]
    public void An_embedded_table_stays_inside_the_opened_cell()
    {
        var session = SessionFixture.Scan("""
            <root>
              <item>
                <left><a>1</a></left>
                <left><a>2</a></left>
                <right><b>1</b></right>
                <right><b>2</b></right>
              </item>
              <item>
                <left><a>3</a></left>
                <left><a>4</a></left>
                <right><b>5</b></right>
                <right><b>6</b></right>
              </item>
              <note>hi</note>
            </root>
            """);
        var list = DocumentPath.Root.Field("item");
        var left = list.WithIndex(1).Field("left");
        var right = list.WithIndex(1).Field("right");
        session.Expand(list);
        session.Expand(left);
        session.Expand(right);
        var frame = Show(session);

        var leftCell = frame.Rows.Single(row => row.Kind == SurfaceKind.Entry && row.Path.Equals(left) && row.IsExpanded);
        var rightCell = frame.Rows.Single(row => row.Kind == SurfaceKind.Entry && row.Path.Equals(right) && row.IsExpanded);
        Assert.Equal("left[2]", leftCell.Value);
        Assert.Equal("right[2]", rightCell.Value);
        Assert.Equal(leftCell.Y, rightCell.Y);
        Assert.True(leftCell.Height > ViewerSession.RowHeight);
        Assert.Equal(leftCell.Height, rightCell.Height);
        var leftHeader = frame.Rows.Single(row => row.Kind == SurfaceKind.ColumnHeader && row.WidthTable!.Equals(left));
        Assert.True(leftHeader.X >= leftCell.X);
        Assert.True(leftHeader.X + leftHeader.Width <= leftCell.X + leftCell.Width + 0.01);
        Assert.True(leftHeader.Y >= leftCell.Y && leftHeader.Y < leftCell.Y + leftCell.Height);
        Assert.True(leftHeader.X + leftHeader.Width <= rightCell.X);
        var next = frame.Rows.Single(row => row.Kind == SurfaceKind.EntryIndex && row.Path.Equals(list.WithIndex(2)));
        Assert.Equal(leftCell.Y + leftCell.Height, next.Y);

        session.Collapse(left);
        var oneOpen = Show(session);
        Assert.Contains(oneOpen.Rows, row => row.Path.Equals(right) && row.IsExpanded);
        Assert.DoesNotContain(oneOpen.Rows, row => row.Path.Equals(left) && row.IsExpanded);
        Assert.True(oneOpen.Rows.Single(row => row.Kind == SurfaceKind.EntryIndex && row.Path.Equals(list.WithIndex(2))).Y > oneOpen.Rows.Single(row => row.Kind == SurfaceKind.EntryIndex && row.Path.Equals(list.WithIndex(1))).Y + ViewerSession.RowHeight);

        session.Collapse(right);
        var closed = Show(session);
        var first = closed.Rows.Single(row => row.Kind == SurfaceKind.EntryIndex && row.Path.Equals(list.WithIndex(1)));
        var second = closed.Rows.Single(row => row.Kind == SurfaceKind.EntryIndex && row.Path.Equals(list.WithIndex(2)));
        Assert.Equal(ViewerSession.RowHeight, second.Y - first.Y);
        Assert.Contains(closed.Rows, row => row.Path.Equals(left) && row.Value == "left[2]");

        var record = SessionFixture.Scan("""
            <root>
              <meta><id>1</id><name>Ada</name></meta>
              <note>hi</note>
            </root>
            """);
        var meta = DocumentPath.Root.Field("meta");
        var note = DocumentPath.Root.Field("note");
        record.Expand(meta);
        var rows = Show(record);
        var metaCell = rows.Rows.Single(row => row.Kind == SurfaceKind.Field && row.Path.Equals(meta));
        var noteCell = rows.Rows.Single(row => row.Kind == SurfaceKind.Field && row.Path.Equals(note));
        var metaName = rows.Rows.Single(row => row.Kind == SurfaceKind.FieldName && row.Path.Equals(meta));
        Assert.True(metaCell.CanExpand);
        Assert.True(metaName.CanExpand);
        Assert.Equal(metaCell.Path, metaName.Path);
        Assert.True(metaCell.Height > ViewerSession.RowHeight);
        Assert.Equal(ViewerSession.RowHeight, noteCell.Height);
        Assert.False(noteCell.IsExpanded);
        Assert.Equal(metaCell.Y + metaCell.Height, noteCell.Y);
        Assert.Equal(metaCell.Width, noteCell.Width);
        var id = rows.Rows.Single(row => row.Kind == SurfaceKind.Field && row.Name == "id");
        Assert.True(id.Y >= metaCell.Y && id.Y < metaCell.Y + metaCell.Height);
        Assert.Equal(ViewerSession.RowHeight, id.Height);
    }

    [Fact]
    public void A_tall_embedded_list_keeps_one_scroll_surface()
    {
        var xml = "<root><wrap>" + string.Concat(Enumerable.Range(0, 500).Select(i => $"<item>{i}</item>")) + "</wrap></root>";
        var session = SessionFixture.Scan(xml);
        var list = DocumentPath.Root.Field("wrap").Field("item");
        session.Expand(DocumentPath.Root.Field("wrap"));
        session.Expand(list);
        session.SetViewport(0, 0, 800, ViewerSession.RowHeight * 4);
        var top = session.ReadSurface();

        Assert.Equal(1, top.ScrollSurfaces);
        Assert.InRange(top.Rows.Count(row => row.Kind == SurfaceKind.Entry), 1, 12);
        Assert.True(top.ContentHeight > 400 * ViewerSession.RowHeight);
        Assert.Contains(top.Rows, row => row.Kind == SurfaceKind.ColumnHeader);

        session.SetViewport(0, top.ContentHeight - (ViewerSession.RowHeight * 3), 800, ViewerSession.RowHeight * 3);
        var bottom = session.ReadSurface();
        Assert.DoesNotContain(bottom.Rows, row => row.Kind == SurfaceKind.ColumnHeader);
        Assert.Contains(bottom.Rows, row => row.Kind == SurfaceKind.EntryIndex && int.Parse(row.Name, CultureInfo.InvariantCulture) > 490);
        Assert.InRange(bottom.Rows.Count(row => row.Kind == SurfaceKind.Entry), 1, 12);

        session.Search(new TextSearch { FindWhat = "499" });
        session.PumpAll();
        var hit = Assert.Single(session.Hits);
        session.Reveal(hit);
        session.SetViewport(session.ViewportX, session.ViewportY, 800, ViewerSession.RowHeight * 6);
        Assert.Contains(session.ReadSurface().Rows, row => row.IsMarked && row.Path.Equals(hit.Path));
    }

    [Fact]
    public void Opening_a_cell_widens_its_column_until_the_last_one_closes()
    {
        var session = SessionFixture.Scan("""
            <root>
              <item><child><a>1</a><b>2</b></child><child><a>3</a><b>4</b></child></item>
              <item><child><a>5</a><b>6</b><c>7</c></child><child><a>8</a></child></item>
            </root>
            """);
        var list = DocumentPath.Root.Field("item");
        var narrow = list.WithIndex(1).Field("child");
        var wide = list.WithIndex(2).Field("child");
        session.Expand(list);
        var narrowOuter = ViewerSession.IndexTrackWidth(2) + (200 * 2);
        var wideOuter = ViewerSession.IndexTrackWidth(2) + (200 * 3);

        session.SetColumnWidth(list, "child", 180);
        session.Expand(narrow);
        session.Expand(wide);
        Assert.Equal(wideOuter + ViewerSession.ExpandGutter, ColumnWidth(session, list, "child"));

        session.Collapse(narrow);
        Assert.Equal(wideOuter + ViewerSession.ExpandGutter, ColumnWidth(session, list, "child"));
        session.Collapse(wide);
        Assert.Equal(180, ColumnWidth(session, list, "child"));
        session.Expand(narrow);
        Assert.Equal(narrowOuter + ViewerSession.ExpandGutter, ColumnWidth(session, list, "child"));

        session.SetColumnWidth(list, "child", 90);
        Assert.Equal(90, ColumnWidth(session, list, "child"));
        var clipped = Show(session);
        Assert.Equal(90, ColumnWidth(session, list, "child"));
        var cell = clipped.Rows.Single(row => row.Kind == SurfaceKind.Entry && row.Path.Equals(narrow) && row.IsExpanded);
        foreach (var row in clipped.Rows)
        {
            if (row.Kind == SurfaceKind.Entry && row.Path.Equals(narrow))
                continue;
            if (row.X >= cell.X && row.X < cell.X + cell.Width && row.Y >= cell.Y && row.Y < cell.Y + cell.Height)
                Assert.True(row.X + row.Width <= cell.X + cell.Width + 0.01);
        }

        Assert.Equal(1, clipped.ScrollSurfaces);
        session.Collapse(narrow);
        Assert.Equal(180, ColumnWidth(session, list, "child"));
        session.Expand(narrow);
        Assert.Equal(narrowOuter + ViewerSession.ExpandGutter, ColumnWidth(session, list, "child"));

        var record = SessionFixture.Scan("<root><meta><id>1</id><name>Ada</name></meta><note>hi</note></root>");
        var meta = DocumentPath.Root.Field("meta");
        record.Expand(meta);
        var opened = Show(record);
        var metaCell = opened.Rows.Single(row => row.Kind == SurfaceKind.Field && row.Path.Equals(meta));
        var id = opened.Rows.Single(row => row.Kind == SurfaceKind.Field && row.Name == "id");
        Assert.Equal(400 + ViewerSession.ExpandGutter, metaCell.Width);
        Assert.True(id.X >= metaCell.X + ViewerSession.ExpandGutter);
        var note = opened.Rows.Single(row => row.Kind == SurfaceKind.Field && row.Name == "note");
        Assert.Equal(400 + ViewerSession.ExpandGutter, note.Width);
        Assert.False(note.IsExpanded);
    }

    [Fact]
    public void An_embedded_table_keeps_its_own_column_widths()
    {
        var session = SessionFixture.Scan("""
            <root>
              <item><child><a>1</a><b>2</b></child><child><a>3</a></child></item>
              <item><child><a>4</a></child><child><a>5</a></child></item>
            </root>
            """);
        var list = DocumentPath.Root.Field("item");
        var child = list.WithIndex(1).Field("child");
        session.Expand(list);
        session.SetColumnWidth(list, "child", 180);
        session.Expand(child);
        session.SetColumnWidth(child, "a", 480);

        session.Collapse(child);
        Assert.Equal(180, ColumnWidth(session, list, "child"));
        session.Expand(child);
        var frame = Show(session);
        Assert.Equal(480, frame.Rows.Single(row => row.Kind == SurfaceKind.ColumnHeader && row.WidthTable!.Equals(child) && row.Name == "a").Width);
        Assert.True(ColumnWidth(session, list, "child") > 180);
    }

    [Fact]
    public void A_collapsed_scalar_follows_the_cell_width()
    {
        var body = new string('m', 40);
        var session = SessionFixture.Scan($"<root><note>{body}</note></root>");
        var note = DocumentPath.Root.Field("note");
        var field = session.ReadScalar(note);
        var frame = Show(session);
        var cell = frame.Rows.Single(row => row.Kind == SurfaceKind.Field && row.Path.Equals(note));

        Assert.Equal(25, field.Text!.Length);
        Assert.Equal(field.Text, cell.Value);
        Assert.True(cell.IsCutOff);
        Assert.StartsWith("m", cell.Value, StringComparison.Ordinal);
        Assert.True(cell.CanGrowValue);

        session.SetRecordValueWidth(DocumentPath.Root, 40 * ViewerSession.Advance);
        var wide = session.ReadScalar(note);
        var wideCell = Show(session).Rows.Single(row => row.Kind == SurfaceKind.Field && row.Path.Equals(note));
        Assert.Equal(body, wide.Text);
        Assert.Equal(body, wideCell.Value);
        Assert.False(wideCell.IsCutOff);
        Assert.False(wide.CanExpandFullValue);

        session.SetRecordValueWidth(DocumentPath.Root, 200);
        Assert.Equal(new string('m', 25), session.ReadScalar(note).Text);
    }

    [Fact]
    public void A_full_value_rewraps_to_the_cell_and_a_hit_stays_collapsed()
    {
        var body = new string('w', 400);
        var session = SessionFixture.Scan($"<root><note>{body}</note></root>");
        var note = DocumentPath.Root.Field("note");
        Assert.True(session.ReadScalar(note).CanExpandFullValue);

        session.ExpandFullValue(note);
        var open = Show(session, 900, 2000);
        var openHeight = open.ContentHeight;
        Assert.True(openHeight > ViewerSession.RowHeight * 2);
        Assert.Contains(open.Rows, row => row.Path.Equals(note) && row.Value != null && row.Value.Length > 0);

        session.SetRecordValueWidth(DocumentPath.Root, 800);
        var wider = Show(session, 900, 2000).ContentHeight;
        Assert.True(wider < openHeight);
        session.SetRecordValueWidth(DocumentPath.Root, 48);
        var narrower = Show(session, 900, 4000).ContentHeight;
        Assert.True(narrower > wider);

        session.CollapseFullValue(note);
        var closed = session.ReadScalar(note);
        Assert.False(closed.IsFullValueExpanded);
        Assert.Equal(0, closed.WindowStart);
        Assert.Equal(6, closed.Text!.Length);
        Assert.True(Show(session).ContentHeight < narrower);

        var shortSession = SessionFixture.Scan("<root><note>short</note></root>");
        var shortField = shortSession.ReadScalar(DocumentPath.Root.Field("note"));
        Assert.False(shortField.CanExpandFullValue);
        Assert.False(Show(shortSession).Rows.Single(row => row.Kind == SurfaceKind.Field).CanGrowValue);

        var listBody = new string('x', 240);
        var listSession = SessionFixture.Scan($"<root><item><t>{listBody}</t></item><item><t>{listBody}</t></item></root>");
        var list = DocumentPath.Root.Field("item");
        listSession.Expand(list);
        var inside = listSession.ReadList(list, 0, 1).Entries[0].Cells.Single(cell => cell.Name == "t");
        Assert.False(inside.IsFullValueExpanded);
        Assert.Equal(25, inside.Text!.Length);

        var hitBody = new string('a', 150) + "NEEDLE" + new string('b', 80);
        var hits = SessionFixture.Scan($"<root><item>{hitBody}</item><item>x</item></root>");
        hits.Expand(DocumentPath.Root.Field("item"));
        hits.Search(new TextSearch { FindWhat = "NEEDLE" });
        hits.PumpAll();
        var hit = Assert.Single(hits.Hits);
        hits.Reveal(hit);
        var window = hits.ReadScalar(hit.Path);
        Assert.False(window.IsFullValueExpanded);
        Assert.Equal(25, window.Text!.Length);
        Assert.Contains("NEEDLE", window.Text, StringComparison.Ordinal);
        var marked = Show(hits).Rows.Single(row => row.IsMarked);
        Assert.Equal(window.Text, marked.Value);
        hits.SetColumnWidth(DocumentPath.Root.Field("item"), "#text", 400);
        var grown = hits.ReadScalar(hit.Path);
        Assert.Equal(50, grown.Text!.Length);
        Assert.Contains("NEEDLE", grown.Text, StringComparison.Ordinal);
        Assert.False(grown.IsFullValueExpanded);
    }

    [Fact]
    public void A_far_slice_of_a_huge_value_does_not_allocate_the_whole_value()
    {
        var huge = new string('q', 1_500_000);
        var session = SessionFixture.Scan($"<root><note>{huge}</note></root>");
        var note = DocumentPath.Root.Field("note");
        session.ExpandFullValue(note);
        session.SetViewport(0, 0, 400, 80);
        var height = session.ReadSurface().ContentHeight;
        session.SetViewport(0, Math.Max(0, height - 48), 400, 48);

        var before = GC.GetAllocatedBytesForCurrentThread();
        var frame = session.ReadSurface();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        var shown = string.Concat(frame.Rows.Select(row => row.Value));

        Assert.True(shown.Length < 500, $"decoded {shown.Length} characters");
        Assert.Contains("q", shown, StringComparison.Ordinal);
        Assert.True(allocated < 80_000, $"allocated {allocated} bytes");
    }

    [Fact]
    public void The_index_track_grows_by_a_digit_and_keeps_room_for_the_inset()
    {
        Assert.Equal(2 * ViewerSession.Advance, ViewerSession.IndexTrackWidth(9));
        Assert.Equal(3 * ViewerSession.Advance, ViewerSession.IndexTrackWidth(10));
        Assert.Equal(4 * ViewerSession.Advance, ViewerSession.IndexTrackWidth(100));
        Assert.Equal(4 * ViewerSession.Advance, ViewerSession.IndexTrackWidth(999));
    }

    [Fact]
    public void A_long_collapsed_label_is_cut_to_the_cell()
    {
        const string name = "thisnameisdefinitelylongerthantwentyfive";
        var session = SessionFixture.Scan($"<root><{name}>1</{name}><{name}>2</{name}></root>");
        var label = Show(session).Rows.Single(row => row.Kind == SurfaceKind.Field && row.Name == name);
        Assert.Equal(LabelRoom, label.Value!.Length);
        Assert.True(label.IsCutOff);
        Assert.StartsWith(name[..20], label.Value, StringComparison.Ordinal);

        var nested = SessionFixture.Scan($"""
            <root>
              <item><{name}><child>1</child></{name}></item>
              <item><{name}><child>2</child></{name}></item>
            </root>
            """);
        var list = DocumentPath.Root.Field("item");
        nested.Expand(list);
        var frame = Show(nested);
        var top = frame.Rows.Single(row => row.Kind == SurfaceKind.EntryIndex && row.Name == "1");
        var cell = frame.Rows.Single(row => row.Kind == SurfaceKind.Entry && row.Name == name && row.Y == top.Y);
        Assert.Equal(LabelRoom, cell.Value!.Length);
        Assert.True(cell.IsCutOff);
    }

    [Fact]
    public void A_hit_window_keeps_the_match_and_the_previous_scalar_returns_to_the_start()
    {
        var token = "NEEDLE";
        var first = new string('a', 80) + token + new string('b', 80);
        var second = new string('c', 80) + token + new string('d', 80);
        var session = SessionFixture.Scan($"<root><item>{first}</item><item>{second}</item></root>");
        session.Search(new TextSearch { FindWhat = token });
        session.PumpAll();
        Assert.Equal(2, session.Hits.Count);
        session.Reveal(session.Hits[0]);
        Assert.True(session.ReadScalar(session.Hits[0].Path).WindowStart > 0);

        session.Reveal(session.Hits[1]);
        var restored = session.ReadScalar(session.Hits[0].Path);
        Assert.Equal(0, restored.WindowStart);
        Assert.StartsWith(new string('a', 20), restored.Text, StringComparison.Ordinal);
        Assert.Contains(token, session.ReadScalar(session.Hits[1].Path).Text, StringComparison.Ordinal);

        var fit = new string('N', 20);
        var fitted = SessionFixture.Scan($"<root><note>{new string('a', 40)}{fit}{new string('b', 40)}</note></root>");
        fitted.Search(new TextSearch { FindWhat = fit });
        fitted.PumpAll();
        fitted.Reveal(Assert.Single(fitted.Hits));
        var fittedWindow = fitted.ReadScalar(DocumentPath.Root.Field("note"));
        Assert.Equal(25, fittedWindow.Text!.Length);
        Assert.Contains(fit, fittedWindow.Text, StringComparison.Ordinal);

        var wide = new string('Q', 30);
        var overflow = SessionFixture.Scan($"<root><note>{new string('a', 40)}{wide}{new string('b', 40)}</note></root>");
        overflow.Search(new TextSearch { FindWhat = wide });
        overflow.PumpAll();
        overflow.Reveal(Assert.Single(overflow.Hits));
        var overflowWindow = overflow.ReadScalar(DocumentPath.Root.Field("note"));
        Assert.Equal(25, overflowWindow.Text!.Length);
        Assert.StartsWith(wide[..25], overflowWindow.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_scalar_slice_records_its_place_in_the_value()
    {
        var body = new string('m', 40);
        var session = SessionFixture.Scan($"<root><note>{body}</note><item>a</item><item>b</item></root>");
        var note = DocumentPath.Root.Field("note");
        var frame = Show(session);
        var cell = frame.Rows.Single(row => row.Kind == SurfaceKind.Field && row.Path.Equals(note));
        var name = frame.Rows.Single(row => row.Kind == SurfaceKind.FieldName && row.Path.Equals(note));
        Assert.True(cell.ScalarText);
        Assert.Equal(0, cell.TextStart);
        Assert.Equal(body[..cell.Value!.Length], cell.Value);
        Assert.False(name.ScalarText);
        Assert.Equal(body.Length, session.ScalarLength(note));
        var list = frame.Rows.Single(row => row.Kind == SurfaceKind.Field && row.Name == "item");
        Assert.False(list.ScalarText);

        session.ExpandFullValue(note);
        var lines = Show(session, 900, 2000).Rows
            .Where(row => row.ScalarText && row.Path.Equals(note))
            .OrderBy(row => row.Y)
            .ToList();
        Assert.True(lines.Count > 1);
        Assert.Equal(0, lines[0].TextStart);
        Assert.True(lines[0].IsExpanded);
        Assert.Equal(lines[0].Value!.Length, lines[1].TextStart);
        Assert.Equal(SurfaceKind.ValueLine, lines[1].Kind);
        Assert.False(lines[1].IsExpanded);
        Assert.Equal(body, string.Concat(lines.Select(row => row.Value)));

        var hitBody = new string('a', 80) + "NEEDLE" + new string('b', 40);
        var hits = SessionFixture.Scan($"<root><item>{hitBody}</item><item>x</item></root>");
        hits.Expand(DocumentPath.Root.Field("item"));
        hits.Search(new TextSearch { FindWhat = "NEEDLE" });
        hits.PumpAll();
        hits.Reveal(Assert.Single(hits.Hits));
        var window = hits.ReadScalar(hits.Hits[0].Path);
        var marked = Show(hits).Rows.Single(row => row.IsMarked);
        Assert.True(marked.ScalarText);
        Assert.Equal(window.WindowStart, marked.TextStart);
        Assert.True(marked.TextStart > 0);
    }

    private static double ColumnWidth(ViewerSession session, DocumentPath list, string name) =>
        Show(session).Rows.Single(row => row.Kind == SurfaceKind.ColumnHeader && row.WidthTable!.Equals(list) && row.Name == name).Width;

    private static SurfaceFrame Show(ViewerSession session, double width = 1100, double height = 800)
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
