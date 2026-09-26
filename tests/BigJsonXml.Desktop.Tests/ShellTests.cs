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

        model.Query = "beta";
        model.Search();
        var hit = Assert.Single(model.Hits);
        model.Reveal(hit);
        Assert.Contains(model.Rows, row => row.IsMarked);
        Assert.Equal("Complete", model.Status);

        model.ChooseHit(null);
        Assert.DoesNotContain(model.Rows, row => row.IsMarked);
        Assert.Same(hit, Assert.Single(model.Hits));
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
        Assert.Equal(ViewerSession.ExpandGutter, DocumentSurface.TextOrigin(list));
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
        var bitmap = new RenderTargetBitmap(new PixelSize(800, 400), new Vector(96, 96));
        bitmap.Render(surface);

        Assert.True(Dark(bitmap, (int)cell.X + 8, (int)cell.Y + 12));
        Assert.True(Dark(bitmap, (int)cell.X + 8, (int)cell.Y + 9));
        Assert.False(Dark(bitmap, (int)plain.X + 8, (int)plain.Y + 12));

        model.Toggle(cell);
        var open = model.Rows.Single(row => row.Kind == SurfaceKind.Field && row.Name == "features" && row.IsExpanded);
        Assert.Equal("feature[2]", open.Value);
        bitmap.Render(surface);
        Assert.True(Dark(bitmap, (int)open.X + 8, (int)open.Y + 12));
        Assert.False(Dark(bitmap, (int)open.X + 8, (int)open.Y + 9));
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
