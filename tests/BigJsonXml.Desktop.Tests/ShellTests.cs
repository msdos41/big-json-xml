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

        var list = model.Rows.Single(row => row.Name == "item");
        model.Toggle(list);
        Assert.Contains(model.Rows, row => row.Kind == SurfaceKind.Entry && row.Value == "alpha");

        var nestedPath = Write("<root><item><meta><x>1</x></meta></item><item><meta><x>2</x></meta></item></root>");
        using var nested = new ViewerModel();
        nested.OpenPath(nestedPath);
        nested.Toggle(nested.Rows.Single(row => row.Name == "item"));
        var entry = nested.Rows.First(row => row.Kind == SurfaceKind.Entry && row.CanExpand);
        nested.Toggle(entry);
        Assert.Contains(nested.Rows, row => row.Name == "x" && row.Value == "1");

        model.Query = "beta";
        model.Search();
        var hit = Assert.Single(model.Hits);
        model.Reveal(hit);
        Assert.Contains(model.Rows, row => row.IsMarked);
        Assert.Equal("Complete", model.Status);
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
