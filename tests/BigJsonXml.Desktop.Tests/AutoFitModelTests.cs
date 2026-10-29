using System.Text;
using IOPath = System.IO.Path;

namespace BigJsonXml.Desktop.Tests;

public class AutoFitModelTests
{
    [Fact]
    public void An_expanded_table_fits_its_columns_on_the_next_tick()
    {
        using var model = new ViewerModel();
        model.SetViewportSize(2000, 800);
        Open(model, Write(40));
        model.Toggle(model.Rows.Single(row => row.Kind == SurfaceKind.Field && row.Name == "item"));
        Assert.Equal(ViewerSession.DefaultWidth, Header(model, "name").Width);

        Ticks(model);

        Assert.Equal("name-number-40".Length * ViewerSession.Advance, Header(model, "name").Width);
        Assert.Equal(ViewerSession.MinWidth, Header(model, "id").Width);
    }

    [Fact]
    public void A_dragged_column_is_not_fitted_again()
    {
        using var model = new ViewerModel();
        model.SetViewportSize(2000, 800);
        Open(model, Write(40));
        model.Toggle(model.Rows.Single(row => row.Kind == SurfaceKind.Field && row.Name == "item"));
        model.DragWidth(Header(model, "name"), 333);

        Ticks(model);

        Assert.Equal(333, Header(model, "name").Width);
        Assert.Equal(ViewerSession.MinWidth, Header(model, "id").Width);
    }

    [Fact]
    public void Switched_off_keeps_the_default_widths()
    {
        using var model = new ViewerModel { AutoFitColumns = false };
        model.SetViewportSize(2000, 800);
        Open(model, Write(40));
        model.Toggle(model.Rows.Single(row => row.Kind == SurfaceKind.Field && row.Name == "item"));

        Ticks(model);

        Assert.Equal(ViewerSession.DefaultWidth, Header(model, "name").Width);
    }

    // The scan of a small file can take more than one tick on a busy machine.
    private static void Open(ViewerModel model, string path)
    {
        model.OpenPath(path);
        for (var i = 0; i < 2000 && model.Tabs[0].Progress == ScanProgress.Scanning; i++)
            model.Tick();
        Assert.NotEqual(ScanProgress.Scanning, model.Tabs[0].Progress);
    }

    private static void Ticks(ViewerModel model)
    {
        for (var i = 0; i < 20; i++)
            model.Tick();
    }

    private static SurfaceRow Header(ViewerModel model, string column) =>
        model.Rows.Single(row => row.Kind == SurfaceKind.ColumnHeader && row.Name == column);

    private static string Write(int count)
    {
        var xml = new StringBuilder("<root>");
        for (var i = 1; i <= count; i++)
            xml.Append("<item><id>").Append(i).Append("</id><name>name-number-").Append(i).Append("</name><z>1</z></item>");
        xml.Append("</root>");
        var path = IOPath.Combine(IOPath.GetTempPath(), Guid.NewGuid().ToString("N") + ".xml");
        File.WriteAllText(path, xml.ToString());
        return path;
    }
}
