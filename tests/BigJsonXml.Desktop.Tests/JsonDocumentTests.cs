namespace BigJsonXml.Desktop.Tests;

public class JsonDocumentTests
{
    [Fact]
    public void The_open_dialog_shows_json_and_xml_together()
    {
        var patterns = MainWindow.OpenFileTypes[0].Patterns ?? [];
        Assert.Contains("*.json", patterns);
        Assert.Contains("*.xml", patterns);
    }

    [Fact]
    public void A_json_name_still_opens_xml_and_an_xml_name_opens_json()
    {
        var xmlInJson = Write("<root><a>1</a></root>", ".json");
        using (var model = new ViewerModel())
        {
            model.OpenPath(xmlInJson);
            Assert.Contains(model.Rows, row => row.Name == "a" && row.Value == "1");
        }

        var jsonInXml = Write("{\"a\":\"hi\"}", ".xml");
        using (var model = new ViewerModel())
        {
            model.OpenPath(jsonInXml);
            Assert.Contains(model.Rows, row => row.Name == "a" && row.Value == "\"hi\"");
        }

        File.Delete(xmlInJson);
        File.Delete(jsonInXml);
    }

    [Fact]
    public void A_json_scan_shows_the_fraction_and_the_remaining_time()
    {
        var source = new MemoryByteSource();
        source.Append("["u8);
        // Large enough that one quiescent slice on a fast machine cannot finish the scan.
        AppendCopies(source, "{\"a\":1},"u8, 4_000_000);
        source.Append("{\"a\":1}]"u8);
        source.Complete();
        using var model = new ViewerModel();
        model.Open(source);

        Assert.Equal("Scanning", model.Status);
        Assert.True(model.ScanFractionShown);
        Assert.InRange(model.ScanFraction, 0, 1);
        var first = model.ScanFraction;
        model.SetScanElapsed(4000);
        Assert.Contains("remaining", model.RemainingTime, StringComparison.Ordinal);

        var moved = false;
        for (var i = 0; i < 8 && model.Status == "Scanning"; i++)
        {
            model.Tick();
            if (model.Status != "Scanning")
                break;
            if (model.ScanFraction > first)
            {
                moved = true;
                break;
            }
        }

        Assert.True(moved);
        Assert.True(model.ScanFraction < 1);
    }

    private static string Write(string text, string suffix)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + suffix);
        File.WriteAllText(path, text);
        return path;
    }

    private static void AppendCopies(MemoryByteSource source, ReadOnlySpan<byte> item, int count)
    {
        var batchItems = 128;
        var batch = new byte[item.Length * batchItems];
        for (var i = 0; i < batchItems; i++)
            item.CopyTo(batch.AsSpan(i * item.Length));
        var full = count / batchItems;
        var rest = count % batchItems;
        for (var i = 0; i < full; i++)
            source.Append(batch);
        if (rest > 0)
            source.Append(batch.AsSpan(0, rest * item.Length));
    }
}
