using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace BigJsonXml.Desktop.Tests;

public class ShellTests
{
    [Fact]
    public void The_scan_idle_gap_is_not_longer_than_the_slice()
    {
        Assert.InRange(ViewerModel.ScanIdleMilliseconds, 1, ViewerModel.ScanSliceMilliseconds);
        Assert.InRange(ViewerModel.ScanSliceMilliseconds, 1, 99);
    }

    [Fact]
    public void The_window_model_shows_the_record_expands_a_list_and_reveals_a_hit()
    {
        var path = Write("<root id=\"1\"><item>alpha</item><item>beta</item></root>");
        using var model = new ViewerModel();
        model.OpenPath(path);

        Assert.Contains(model.Rows, row => row.Name == "@id" && row.Value == "1");
        Assert.Contains(model.Rows, row => row.Name == "item" && row.Value == "item[2]");

        var list = model.Rows.Single(row => row.Kind == SurfaceKind.Field && row.Name == "item");
        model.Toggle(list);
        var openList = model.Rows.Single(row => row.Kind == SurfaceKind.Field && row.Name == "item" && row.IsExpanded);
        Assert.Equal("item[2]", openList.Value);
        var openHeader = model.Rows.Single(row => row.Kind == SurfaceKind.ColumnHeader && row.Name == "#text");
        Assert.True(openHeader.Y >= openList.Y + ViewerSession.RowHeight);
        var alpha = model.Rows.Single(row => row.Kind == SurfaceKind.Entry && row.Name == "#text" && row.Value == "alpha");
        Assert.Equal(list.Path.WithIndex(1).Field("#text"), alpha.Path);
        Assert.DoesNotContain(model.Rows, row => row.Value != null && row.Value.Contains('|', StringComparison.Ordinal));

        var nestedPath = Write("<root><item><meta><x>1</x></meta></item><item><meta><x>2</x></meta></item></root>");
        using var nested = new ViewerModel();
        nested.OpenPath(nestedPath);
        nested.Toggle(nested.Rows.Single(row => row.Kind == SurfaceKind.Field && row.Name == "item"));
        var entry = nested.Rows.First(row => row.Kind == SurfaceKind.Entry && row.CanExpand);
        nested.Toggle(entry);
        var openEntry = nested.Rows.Single(row => row.Kind == SurfaceKind.Entry && row.Path.Equals(entry.Path) && row.IsExpanded);
        Assert.Equal("meta", openEntry.Value);
        var nestedField = nested.Rows.Single(row => row.Name == "x" && row.Value == "1");
        Assert.True(nestedField.Y >= openEntry.Y + ViewerSession.RowHeight);

        model.FindWhat = "beta";
        model.FindAll();
        var hit = Assert.Single(model.Results);
        model.ChooseResult(hit);
        Assert.Contains(model.Rows, row => row.IsMarked);
        Assert.Equal("Complete", model.Status);

        model.ChooseResult(null);
        Assert.DoesNotContain(model.Rows, row => row.IsMarked);
        Assert.Same(hit, Assert.Single(model.Results));
    }

    [Fact]
    public void The_find_and_results_windows_list_a_hit_and_choosing_it_marks_the_cell()
    {
        var path = Write("<root><item>alpha</item><item>beta</item></root>");
        using var model = new ViewerModel();
        Assert.True(model.FindWindowOpen);
        Assert.True(model.ResultsWindowOpen);
        model.OpenPath(path);

        model.FindWhat = "";
        model.FindAll();
        Assert.Empty(model.Results);
        Assert.Equal(0, model.ResultCount);
        Assert.DoesNotContain(model.Rows, row => row.IsMarked);

        model.FindWhat = "beta";
        model.ResultWidth = 40;
        model.FindAll();
        var row = Assert.Single(model.Results);
        Assert.Equal(1, model.ResultCount);
        Assert.Equal("beta", row.Text);
        Assert.Equal(0, row.MarkStart);
        Assert.Equal(4, row.MarkLength);
        Assert.Equal("beta", row.Match);
        Assert.Equal("/root/item[2]/#text", row.Path);
        Assert.DoesNotContain(model.Rows, row => row.IsMarked);

        model.FindWhat = row.Path;
        model.FindAll();
        Assert.Empty(model.Results);
        Assert.DoesNotContain(model.Rows, row => row.IsMarked);

        model.FindWhat = "beta";
        model.FindAll();
        model.ChooseResult(Assert.Single(model.Results));
        Assert.Contains(model.Rows, row => row.IsMarked && row.Path.Equals(model.Results[0].Hit.Path));
        Assert.Equal(0, model.Results[0].MarkStart);

        var body = new string('a', 40) + "NEEDLE" + new string('b', 40);
        var wide = Write($"<root><note>{body}</note></root>");
        model.OpenPath(wide);
        model.ResultWidth = 10;
        model.FindWhat = "NEEDLE";
        model.FindAll();
        var window = Assert.Single(model.Results);
        Assert.Equal(10, window.Text.Length);
        Assert.Equal("NEEDLE", window.Text.Substring(window.MarkStart, window.MarkLength));
        Assert.Equal("/root/note", window.Path);
    }

    [Fact]
    public void Match_case_on_the_find_window_is_passed_to_the_search()
    {
        var path = Write("<root><item>beta</item></root>");
        using var model = new ViewerModel();
        model.OpenPath(path);
        model.FindWhat = "Beta";
        model.FindAll();
        Assert.Empty(model.Results);

        model.MatchCase = false;
        model.FindAll();
        Assert.Equal("beta", Assert.Single(model.Results).Text);

        model.MatchCase = true;
        model.StartsWith = true;
        model.FindWhat = "b";
        model.FindAll();
        Assert.Single(model.Results);

        model.EntireCell = true;
        model.FindAll();
        Assert.Empty(model.Results);

        model.FindWhat = "beta";
        model.FindAll();
        Assert.Single(model.Results);
    }

    [Fact]
    public void Turning_a_target_off_keeps_the_name_and_reveals_that_row()
    {
        var path = Write("""
            <root>
              <box id="box">keep</box>
              <item>one</item>
              <item>two</item>
            </root>
            """);
        using var model = new ViewerModel();
        model.OpenPath(path);
        model.FindWhat = "box";
        model.EntireCell = true;
        model.FindAll();
        Assert.Equal(2, model.ResultCount);

        model.Value = false;
        model.FindAll();
        var name = Assert.Single(model.Results);
        Assert.Equal("box", name.Text);
        Assert.Equal("/root/box", name.Path);
        Assert.Equal(0, name.MarkStart);
        Assert.Equal(3, name.MarkLength);
        model.ChooseResult(name);
        Assert.Contains(model.Rows, row => row.IsMarked && row.Path.Equals(name.Hit.Path));

        model.Value = true;
        model.ElementName = false;
        model.FindAll();
        var value = Assert.Single(model.Results);
        Assert.Equal("box", value.Text);
        Assert.Equal("/root/box/@id", value.Path);
    }

    [Fact]
    public void Field_name_on_the_find_window_limits_the_hits()
    {
        var path = Write("""
            <root>
              <Start_node>RTM=123</Start_node>
              <other>RTM=123</other>
            </root>
            """);
        using var model = new ViewerModel();
        model.OpenPath(path);
        model.FindWhat = "123";
        model.FieldName = "Start_node";
        model.FindAll();
        var hit = Assert.Single(model.Results);
        Assert.Equal("/root/Start_node", hit.Path);
        Assert.Equal("RTM=123", hit.Text);

        model.FieldName = "";
        model.FindAll();
        Assert.Equal(2, model.ResultCount);
    }

    [Fact]
    public void Parent_element_name_on_the_find_window_limits_the_hits()
    {
        var path = Write("""
            <root>
              <Extended_covering>
                <Start_node>RTM=123</Start_node>
              </Extended_covering>
              <other>
                <Start_node>RTM=123</Start_node>
              </other>
            </root>
            """);
        using var model = new ViewerModel();
        model.OpenPath(path);
        model.FindWhat = "123";
        model.ParentElementName = "Extended_covering";
        model.FindAll();
        Assert.Equal("/root/Extended_covering/Start_node", Assert.Single(model.Results).Path);

        model.ParentElementName = "";
        model.GrandfatherElementName = "root";
        model.FindAll();
        Assert.Equal(2, model.ResultCount);

        model.GrandfatherElementName = "";
        model.ParentAttributeName = "@id";
        model.FindAll();
        Assert.Empty(model.Results);
    }

    [Fact]
    public void Results_grow_while_the_scan_is_still_reading()
    {
        var source = new MemoryByteSource();
        source.Append("<root><item>findme</item>");
        using var model = new ViewerModel();
        model.Open(source);
        model.FindWhat = "findme";
        model.FindAll();

        Assert.Equal("Scanning", model.Status);
        Assert.Equal(1, model.ResultCount);
        Assert.Equal("findme", Assert.Single(model.Results).Text);

        source.Append("<item>later</item><item>findme</item></root>");
        source.Complete();
        for (var i = 0; i < 40 && model.Status == "Scanning"; i++)
            model.Tick();

        Assert.Equal("Complete", model.Status);
        Assert.Equal(2, model.ResultCount);
        Assert.Equal("/root/item[1]/#text", model.Results[0].Path);
        Assert.Equal("/root/item[3]/#text", model.Results[1].Path);
    }

    [Fact]
    public void Bare_attribute_still_shows_an_expandable_child()
    {
        var path = Write("""
            <CadDocument id="doc-main" name="Harness" units="mm">
              <Layers>
                <Layer id="0" visible locked="false"></Layer>
              </Layers>
            </CadDocument>
            """);
        using var model = new ViewerModel();
        model.OpenPath(path);

        Assert.Equal("Complete", model.Status);
        Assert.Equal("", model.Error);
        Assert.Contains(model.Rows, row => row.Name == "@id" && row.Value == "doc-main");
        var layers = Assert.Single(model.Rows, row => row.Kind == SurfaceKind.Field && row.Name == "Layers");
        Assert.True(layers.CanExpand);

        model.Toggle(layers);
        var layer = Assert.Single(model.Rows, row => row.Kind == SurfaceKind.Field && row.Name == "Layer");
        Assert.True(layer.CanExpand);
        model.Toggle(layer);
        Assert.Contains(model.Rows, row => row.Name == "@visible" && row.Value == "");
        Assert.Contains(model.Rows, row => row.Name == "@locked" && row.Value == "false");
    }

    [Fact]
    public void The_window_clips_a_scalar_until_the_cell_is_wide_enough_and_drags_a_column()
    {
        var path = Write("<root><title>abcdefghijklmnopqrstuvwxyz</title><item><a>1</a></item><item><a>2</a></item></root>");
        var before = File.ReadAllBytes(path);
        using var model = new ViewerModel();
        model.OpenPath(path);

        var title = model.Rows.Single(row => row.Kind == SurfaceKind.Field && row.Name == "title");
        Assert.True(title.IsCutOff);
        Assert.EndsWith("…", DocumentSurface.PaintedText(title), StringComparison.Ordinal);
        model.DragWidth(title, 800);
        var fitted = model.Rows.Single(row => row.Kind == SurfaceKind.Field && row.Name == "title");
        Assert.False(fitted.IsCutOff);
        Assert.Equal("abcdefghijklmnopqrstuvwxyz", DocumentSurface.PaintedText(fitted));
        Assert.Equal(before, File.ReadAllBytes(path));

        var list = model.Rows.Single(row => row.Kind == SurfaceKind.Field && row.Name == "item");
        var hit = DocumentSurface.HitTest(model.Rows, list.X + 12, list.Y + 8);
        Assert.False(hit.Resize);
        Assert.Equal(list.Path, hit.Row!.Path);
        model.Toggle(hit.Row!);

        var header = model.Rows.Single(row => row.Kind == SurfaceKind.ColumnHeader && row.Name == "a");
        var edge = DocumentSurface.HitTest(model.Rows, header.X + header.Width - 2, header.Y + 8);
        Assert.True(edge.Resize);
        Assert.Equal("a", edge.Row!.WidthColumn);
        model.DragWidth(edge.Row, 320);
        Assert.Equal(320, model.Rows.Single(row => row.Kind == SurfaceKind.ColumnHeader && row.Name == "a").Width);
        Assert.Contains(model.Rows, row => row.Kind == SurfaceKind.Entry && row.Name == "a" && row.Width == 320 && row.Value == "1");

        var index = model.Rows.First(row => row.Kind == SurfaceKind.EntryIndex && row.Name == "1");
        var indexHit = DocumentSurface.HitTest(model.Rows, index.X + (index.Width / 2), index.Y + 8);
        Assert.False(indexHit.Resize);
        Assert.Equal(SurfaceKind.EntryIndex, indexHit.Row!.Kind);
        var indexWidth = index.Width;
        model.Toggle(indexHit.Row);
        model.Toggle(model.Rows.Single(row => row.Kind == SurfaceKind.ColumnHeader && row.Name == "a"));
        Assert.Equal(indexWidth, model.Rows.First(row => row.Kind == SurfaceKind.EntryIndex && row.Name == "1").Width);
        Assert.Contains(model.Rows, row => row.Kind == SurfaceKind.Entry && row.Value == "1");

        var nestedPath = Write("<root><item><meta><x>1</x></meta></item><item><meta><x>2</x></meta></item></root>");
        using var nested = new ViewerModel();
        nested.OpenPath(nestedPath);
        nested.Toggle(nested.Rows.Single(row => row.Kind == SurfaceKind.Field && row.Name == "item"));
        var meta = nested.Rows.First(row => row.Kind == SurfaceKind.Entry && row.Name == "meta" && row.CanExpand && row.Value == "meta");
        var clicked = DocumentSurface.HitTest(nested.Rows, meta.X + 10, meta.Y + 8);
        Assert.Equal(meta.Path, clicked.Row!.Path);
        nested.Toggle(clicked.Row);
        Assert.Contains(nested.Rows, row => row.Name == "x" && row.Value == "1");
    }

    [Fact]
    public void An_expandable_cell_shows_an_expand_button()
    {
        var path = Write("<root><item>a</item><item>b</item><note>hi</note></root>");
        using var model = new ViewerModel();
        model.OpenPath(path);

        var list = model.Rows.Single(row => row.Kind == SurfaceKind.Field && row.Name == "item");
        var note = model.Rows.Single(row => row.Kind == SurfaceKind.Field && row.Name == "note");
        var name = model.Rows.Single(row => row.Kind == SurfaceKind.FieldName && row.Name == "item");
        Assert.True(DocumentSurface.HasExpandButton(list));
        Assert.False(DocumentSurface.HasExpandButton(note));
        Assert.False(DocumentSurface.HasExpandButton(name));
        Assert.True(DocumentSurface.TryButton(list, out var button));
        Assert.True(button.X >= DocumentSurface.ResizeEdge + 4);
        Assert.False(DocumentSurface.HitsExpandButton(list, DocumentSurface.ResizeEdge, 12));
        Assert.True(DocumentSurface.HitsExpandButton(list, button.X + 2, 12));
        Assert.Equal(ViewerSession.ExpandGutter, DocumentSurface.TextOrigin(list));
        Assert.True(DocumentSurface.TextOrigin(list) >= button.Right);
        Assert.Equal(4, DocumentSurface.TextOrigin(note));

        model.Toggle(list);
        var open = model.Rows.Single(row => row.Kind == SurfaceKind.Field && row.Name == "item");
        Assert.True(open.IsExpanded);
        Assert.True(DocumentSurface.HasExpandButton(open));
        var header = model.Rows.Single(row => row.Kind == SurfaceKind.ColumnHeader && row.Name == "#text");
        Assert.False(DocumentSurface.HasExpandButton(header));
        Assert.True(header.X >= open.X + ViewerSession.ExpandGutter);
    }

    [Fact]
    public void A_tall_entry_centers_its_text_and_an_opened_caption_stays_on_the_first_line()
    {
        var tall = new SurfaceRow(
            0, 0, 80, ViewerSession.RowHeight * 3,
            "Unit", "bar", false, false, false, DocumentPath.Root, SurfaceKind.Entry);
        var caption = tall with { Name = "UnitName", Value = "UnitName", CanExpand = true, IsExpanded = true };
        var index = tall with { Kind = SurfaceKind.EntryIndex, Name = "1", Value = null };
        Assert.Equal(4, DocumentSurface.TextTop(caption));
        Assert.True(DocumentSurface.TextTop(tall) > ViewerSession.RowHeight);
        Assert.Equal(DocumentSurface.TextTop(tall), DocumentSurface.TextTop(index));
    }

    [Fact]
    public void The_expand_button_is_painted_and_a_plain_cell_is_not()
    {
        if (Application.Current is null)
            AppBuilder.Configure<App>().UsePlatformDetect().SetupWithoutStarting();

        var path = Write("""
            <root flag="">
              <features>
                <feature id="1"></feature>
                <feature id="2"></feature>
              </features>
            </root>
            """);
        using var model = new ViewerModel();
        model.OpenPath(path);
        var surface = new DocumentSurface { DataContext = model };
        surface.Measure(new Size(800, 400));
        surface.Arrange(new Rect(0, 0, 800, 400));

        var cell = model.Rows.Single(row => row.Kind == SurfaceKind.Field && row.Name == "features");
        var plain = model.Rows.Single(row => row.Kind == SurfaceKind.Field && row.Name == "@flag");
        Assert.Equal("feature[2]", cell.Value);
        Assert.True(DocumentSurface.TryButton(cell, out var button));
        var bitmap = new RenderTargetBitmap(new PixelSize(800, 400), new Vector(96, 96));
        bitmap.Render(surface);

        var midX = (int)(cell.X + button.X + (button.Width / 2));
        var midY = (int)(cell.Y + button.Y + (button.Height / 2));
        var armY = (int)(cell.Y + button.Y + 3);
        Assert.True(Dark(bitmap, midX, midY));
        Assert.True(Dark(bitmap, midX, armY));
        Assert.False(Dark(bitmap, (int)(cell.X + DocumentSurface.ResizeEdge - 1), midY));
        Assert.False(Dark(bitmap, (int)plain.X + 8, (int)plain.Y + 12));

        model.Toggle(cell);
        var open = model.Rows.Single(row => row.Kind == SurfaceKind.Field && row.Name == "features" && row.IsExpanded);
        Assert.Equal("feature[2]", open.Value);
        bitmap.Render(surface);
        Assert.True(Dark(bitmap, (int)(open.X + button.X + (button.Width / 2)), (int)(open.Y + button.Y + (button.Height / 2))));
        Assert.False(Dark(bitmap, (int)(open.X + button.X + (button.Width / 2)), (int)(open.Y + button.Y + 3)));
    }

    [Fact]
    public void A_three_digit_index_fits_inside_its_column()
    {
        if (Application.Current is null)
            AppBuilder.Configure<App>().UsePlatformDetect().SetupWithoutStarting();

        var width = ViewerSession.IndexTrackWidth(295);
        var row = new SurfaceRow(
            0, 0, width, ViewerSession.RowHeight,
            "295", null, false, false, false,
            DocumentPath.Root.Field("row").WithIndex(295),
            SurfaceKind.EntryIndex);
        var text = new FormattedText(
            "888", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 13, Brushes.Black);
        Assert.True(
            DocumentSurface.TextOrigin(row) + text.Width <= width - 2,
            $"column {width} cannot hold {text.Width}px starting at {DocumentSurface.TextOrigin(row)}");
    }

    [Fact]
    public void A_column_edge_uses_the_horizontal_resize_cursor()
    {
        var header = new SurfaceRow(
            10, 0, 80, ViewerSession.RowHeight,
            "name", "name", false, false, false,
            DocumentPath.Root.Field("row"),
            SurfaceKind.ColumnHeader,
            Axis: WidthAxis.ListColumn);
        var edge = DocumentSurface.HitTest([header], header.X + header.Width - 1, 4);
        var inside = DocumentSurface.HitTest([header], header.X + 8, 4);
        Assert.True(edge.Resize);
        Assert.False(inside.Resize);
        Assert.Equal(StandardCursorType.SizeWestEast, DocumentSurface.CursorFor(edge));
        Assert.Equal(StandardCursorType.Arrow, DocumentSurface.CursorFor(inside));
    }

    [Fact]
    public void Collapsing_the_last_record_while_scrolled_to_the_end_keeps_the_rows_above()
    {
        if (Application.Current is null)
            AppBuilder.Configure<App>().UsePlatformDetect().SetupWithoutStarting();

        var body = string.Concat(Enumerable.Range(0, 120).Select(i => $"<row><name>n{i}</name></row>"));
        var path = Write($"<root>{body}<Routing><child>z</child></Routing></root>");
        using var model = new ViewerModel();
        model.OpenPath(path);
        model.SetViewportSize(500, ViewerSession.RowHeight * 4);
        var bar = new ScrollBar
        {
            DataContext = model,
            Orientation = Avalonia.Layout.Orientation.Vertical,
            ViewportSize = model.ViewportHeight,
        };
        bar.Bind(RangeBase.MaximumProperty, new Binding(nameof(ViewerModel.ScrollMaxY)));
        bar.Bind(RangeBase.ValueProperty, new Binding(nameof(ViewerModel.ScrollY)) { Mode = BindingMode.TwoWay });

        model.Toggle(model.Rows.Single(row => row.Kind == SurfaceKind.Field && row.Name == "row"));
        model.ScrollY = model.ScrollMaxY;
        var routing = model.Rows.Single(row => row.Kind == SurfaceKind.Field && row.Name == "Routing");
        model.Toggle(routing);
        model.ScrollY = model.ScrollMaxY;
        model.Toggle(model.Rows.Single(row => row.Kind == SurfaceKind.Field && row.Name == "Routing" && row.IsExpanded));

        AssertFilled(model);
        var surface = new DocumentSurface { DataContext = model, Width = model.ViewportWidth, Height = model.ViewportHeight };
        surface.Measure(new Size(model.ViewportWidth, model.ViewportHeight));
        surface.Arrange(new Rect(0, 0, model.ViewportWidth, model.ViewportHeight));
        var bitmap = new RenderTargetBitmap(new PixelSize((int)model.ViewportWidth, (int)model.ViewportHeight), new Vector(96, 96));
        bitmap.Render(surface);
        var index = model.Rows.First(row => row.Kind == SurfaceKind.EntryIndex && row.Y >= model.ScrollY && row.Y < model.ScrollY + ViewerSession.RowHeight);
        Assert.True(Dark(bitmap, (int)(index.X - model.ScrollX + 6), (int)(index.Y - model.ScrollY + 10)));
    }

    private static void AssertFilled(ViewerModel model)
    {
        Assert.True(model.ScrollY <= model.ScrollMaxY + 0.5, $"scroll {model.ScrollY} past {model.ScrollMaxY}");
        var top = model.ScrollY;
        var bottom = Math.Min(model.ScrollY + model.ViewportHeight, model.ContentHeight);
        var leaves = model.Rows.Where(row => row.Y >= top - ViewerSession.RowHeight || row.Height <= ViewerSession.RowHeight * 2).ToList();
        var cursor = top;
        while (cursor < bottom - 0.5)
        {
            var cover = leaves.Where(row => row.Y <= cursor + 0.1 && cursor < row.Y + row.Height - 0.1).ToList();
            Assert.True(cover.Count > 0, $"gap at {cursor - top}");
            cursor = cover.Min(row => row.Y + row.Height);
        }
    }

    [Fact]
    public void Check_mode_opens_expands_searches_and_reveals()
    {
        var path = Write("<root><item>alpha</item><item>beta</item></root>");
        Assert.Equal(0, SessionCheck.Run(path));
    }

    [Fact]
    public void Shipping_project_is_a_self_contained_single_file_without_native_aot()
    {
        var root = RepoRoot();
        var project = File.ReadAllText(Path.Combine(root, "src", "BigJsonXml.Desktop", "BigJsonXml.Desktop.csproj"));
        Assert.Contains("<PublishSingleFile>true</PublishSingleFile>", project, StringComparison.Ordinal);
        Assert.Contains("<SelfContained>true</SelfContained>", project, StringComparison.Ordinal);
        Assert.Contains("win-x64", project, StringComparison.Ordinal);
        Assert.DoesNotContain("PublishAot", project, StringComparison.Ordinal);
        var core = Directory.GetFiles(Path.Combine(root, "src", "BigJsonXml.Core"), "*.cs", SearchOption.AllDirectories);
        foreach (var file in core)
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("DllImport", text, StringComparison.Ordinal);
            Assert.DoesNotContain("LibraryImport", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Dragging_selects_cell_text_and_a_click_selects_shown_text()
    {
        var path = DocumentPath.Root.Field("note");
        var note = new SurfaceRow(
            0, 0, 200, ViewerSession.RowHeight,
            "note", "hello", false, false, false, path, SurfaceKind.Field,
            ScalarText: true);
        var press = new CellPress();
        press.Begin(note, false, new Point(0, 0), 1, null);
        press.Move(new Point(20, 0), note, 4);
        Assert.Equal(CellGesture.Select, press.End(5, null));
        Assert.Equal(1, press.Selection!.Start);
        Assert.Equal(4, press.Selection.End);
        Assert.True(press.Selection.Scalar);

        press.Begin(note, false, new Point(0, 0), 0, null);
        Assert.Equal(CellGesture.Select, press.End(5, null));
        Assert.Equal(0, press.Selection!.Start);
        Assert.Equal(5, press.Selection.End);

        var collapsed = note with { CanGrowValue = true, Value = "abc", IsExpanded = false };
        press.Begin(collapsed, false, default, 0, null);
        Assert.Equal(CellGesture.Toggle, press.End(40, null));

        press.Begin(collapsed, false, new Point(0, 0), 0, null);
        press.Move(new Point(8, 0), collapsed, 2);
        Assert.Equal(CellGesture.Select, press.End(40, null));
        Assert.Equal(0, press.Selection!.Start);
        Assert.Equal(2, press.Selection.End);

        var open = collapsed with { IsExpanded = true };
        press.Begin(open, false, default, 3, null);
        Assert.Equal(CellGesture.Select, press.End(40, null));
        Assert.Equal(0, press.Selection!.Start);
        Assert.Equal(40, press.Selection.End);

        press.Begin(open, true, default, 0, null);
        Assert.Equal(CellGesture.Toggle, press.End(40, null));

        var next = open with { Kind = SurfaceKind.ValueLine, IsExpanded = false, TextStart = 4, Value = "bbbb", Y = 24 };
        press.Begin(open, false, new Point(0, 0), 1, null);
        press.Move(new Point(30, 24), next, 6);
        Assert.Equal(CellGesture.Select, press.End(8, null));
        Assert.Equal(1, press.Selection!.Start);
        Assert.Equal(6, press.Selection.End);

        var line = open with { Kind = SurfaceKind.ValueLine, IsExpanded = false, TextStart = 25, Value = "rest" };
        press.Begin(line, false, default, 26, null);
        Assert.Equal(CellGesture.Select, press.End(40, null));
        Assert.Equal(0, press.Selection!.Start);
        Assert.Equal(40, press.Selection.End);

        var title = new SurfaceRow(
            0, 0, 80, ViewerSession.RowHeight,
            "title", "ti", false, false, false, path, SurfaceKind.FieldName,
            IsCutOff: true);
        press.Begin(title, false, default, 0, CellPress.CopySource(title));
        Assert.Equal(CellGesture.Select, press.End(0, CellPress.CopySource(title)));
        Assert.Equal("title", press.Selection!.Label);
        Assert.Equal(5, press.Selection.Length);

        var list = note with { Name = "item", Value = "item[2]", CanExpand = true, ScalarText = false };
        press.Begin(list, false, default, 0, "item[2]");
        Assert.Equal(CellGesture.Select, press.End(0, "item[2]"));
        Assert.Equal("item[2]", press.Selection!.Label);

        press.Begin(list, true, default, 0, "item[2]");
        Assert.Equal(CellGesture.Toggle, press.End(0, "item[2]"));

        var openList = list with { IsExpanded = true };
        press.Begin(openList, false, default, 0, "item[2]");
        Assert.Equal(CellGesture.Select, press.End(0, "item[2]"));
        press.Begin(openList, true, default, 0, "item[2]");
        Assert.Equal(CellGesture.Toggle, press.End(0, "item[2]"));
    }

    [Fact]
    public void A_column_header_click_selects_only_that_header()
    {
        var list = DocumentPath.Root.Field("item");
        var name = new SurfaceRow(
            0, 0, 80, ViewerSession.RowHeight,
            "name", "name", false, false, false, list, SurfaceKind.ColumnHeader);
        var city = name with { X = 80, Name = "city", Value = "city" };
        var press = new CellPress();
        press.Begin(name, false, new Point(0, 0), 0, CellPress.CopySource(name));
        press.Move(new Point(12, 0), name, 2);
        Assert.Equal(CellGesture.Select, press.End(0, CellPress.CopySource(name)));
        Assert.True(press.Selection!.Covers(name));
        Assert.False(press.Selection.Covers(city));
        Assert.False(CellPress.SameText(name, city));
        Assert.True(CellPress.SameText(name, name with { Width = 120 }));
    }

    [Fact]
    public void The_model_returns_the_selected_slice_of_a_cell()
    {
        var body = new string('m', 40);
        var path = Write($"<root><note>{body}</note></root>");
        using var model = new ViewerModel();
        model.OpenPath(path);
        var note = model.Rows.Single(row => row.Kind == SurfaceKind.Field && row.Name == "note");
        Assert.True(note.ScalarText);
        model.SetSelection(new CellSelection(note.Path, 0, 4, true, null, note.Kind));
        Assert.Equal("mmmm", model.SelectedText());

        model.Toggle(note);
        model.SetSelection(new CellSelection(note.Path, 20, 30, true, null, SurfaceKind.Field));
        Assert.Equal(new string('m', 10), model.SelectedText());
        model.SelectAll();
        Assert.Equal(body, model.SelectedText());

        model.SetSelection(new CellSelection(note.Path, 1, 4, false, "alpha", SurfaceKind.ColumnHeader));
        Assert.Equal("lph", model.SelectedText());
    }

    [Fact]
    public void An_opened_scalar_shows_a_collapse_button_on_the_first_line()
    {
        var path = Write($"<root><note>{new string('n', 80)}</note></root>");
        using var model = new ViewerModel();
        model.OpenPath(path);
        var note = model.Rows.Single(row => row.Kind == SurfaceKind.Field && row.Name == "note");
        Assert.True(note.CanGrowValue);
        Assert.False(DocumentSurface.HasCollapseButton(note));
        Assert.False(DocumentSurface.HasExpandButton(note));
        model.Toggle(note);
        var open = model.Rows.Single(row => row.Kind == SurfaceKind.Field && row.Path.Equals(note.Path));
        Assert.True(DocumentSurface.HasCollapseButton(open));
        Assert.Equal(4, DocumentSurface.TextOrigin(open));
        Assert.True(DocumentSurface.HitsExpandButton(open, open.Width - 4, 12));
        Assert.False(DocumentSurface.HitsExpandButton(open, 8, 12));
        var line = model.Rows.First(row => row.Kind == SurfaceKind.ValueLine && row.Path.Equals(note.Path));
        Assert.False(DocumentSurface.HasCollapseButton(line));
    }

    [Fact]
    public void A_character_hit_follows_the_painted_glyphs()
    {
        if (Application.Current is null)
            AppBuilder.Configure<App>().UsePlatformDetect().SetupWithoutStarting();

        const string text = "abcd";
        Assert.Equal(0, CellText.IndexAt(text, 0));
        Assert.Equal(text.Length, CellText.IndexAt(text, 10_000));
        var rects = CellText.Rects(text, 1, 2);
        Assert.NotEmpty(rects);
        Assert.True(rects[0].Width > 0);
        Assert.Equal(1, CellText.IndexAt(text, rects[0].X + 0.25));
    }

    private static bool Dark(RenderTargetBitmap bitmap, int x, int y)
    {
        var pixel = new byte[4];
        var handle = GCHandle.Alloc(pixel, GCHandleType.Pinned);
        try
        {
            bitmap.CopyPixels(new PixelRect(x, y, 1, 1), handle.AddrOfPinnedObject(), 4, 4);
        }
        finally
        {
            handle.Free();
        }

        return pixel[0] < 200 && pixel[1] < 200 && pixel[2] < 200;
    }

    private static string Write(string xml)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".xml");
        File.WriteAllText(path, xml);
        return path;
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "BigJsonXml.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }
}
