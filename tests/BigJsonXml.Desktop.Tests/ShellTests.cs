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
        var alpha = model.Rows.Single(row => row.Kind == SurfaceKind.Entry && row.Name == "#text" && row.Value == "alpha");
        Assert.Equal(list.Path.WithIndex(1).Field("#text"), alpha.Path);
        Assert.DoesNotContain(model.Rows, row => row.Value != null && row.Value.Contains('|', StringComparison.Ordinal));

        var nestedPath = Write("<root><item><meta><x>1</x></meta></item><item><meta><x>2</x></meta></item></root>");
        using var nested = new ViewerModel();
        nested.OpenPath(nestedPath);
        nested.Toggle(nested.Rows.Single(row => row.Kind == SurfaceKind.Field && row.Name == "item"));
        var entry = nested.Rows.First(row => row.Kind == SurfaceKind.Entry && row.CanExpand);
        nested.Toggle(entry);
        Assert.Contains(nested.Rows, row => row.Name == "x" && row.Value == "1");

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
