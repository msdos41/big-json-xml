using System.Diagnostics;
using System.Text;
using IOPath = System.IO.Path;

namespace BigJsonXml.Desktop.Tests;

public class BackgroundSearchTickTests
{
    private static string WriteItems(int count, Func<int, bool> needle)
    {
        var builder = new StringBuilder("<root>\n");
        for (var i = 1; i <= count; i++)
            builder.Append(needle(i) ? "<item><v>needle</v></item>\n" : $"<item><v>v{i}</v></item>\n");
        builder.Append("</root>\n");
        var directory = IOPath.Combine(IOPath.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = IOPath.Combine(directory, "items.xml");
        File.WriteAllText(path, builder.ToString());
        return path;
    }

    private static void ScanToEnd(ViewerModel model)
    {
        for (var i = 0; i < 10_000 && model.Tabs[0].Progress == ScanProgress.Scanning; i++)
            model.Tick();
        Assert.Equal(ScanProgress.Completed, model.Tabs[0].Progress);
    }

    [Fact]
    public void Find_next_waits_for_the_search_thread_and_lands_on_the_last_entry()
    {
        using var model = new ViewerModel();
        model.OpenPath(WriteItems(200_000, i => i == 199_999));
        ScanToEnd(model);
        model.ShowFind();
        model.FindWhat = "needle";
        model.FindAll();
        model.FindNext();

        var current = model.CurrentResult;
        Assert.NotNull(current);
        Assert.Equal("needle", current.Text);
        Assert.Contains(model.Rows, row => row.IsMarked);
    }

    [Fact]
    public void A_tick_during_a_search_after_the_scan_only_hands_over_hits()
    {
        using var model = new ViewerModel();
        model.OpenPath(WriteItems(300_000, i => i % 1000 == 0));
        ScanToEnd(model);
        model.ShowFind();
        model.FindWhat = "needle";
        model.FindAll();

        var ticks = new List<double>();
        for (var i = 0; i < 100_000 && model.SearchRunning; i++)
        {
            var started = Stopwatch.GetTimestamp();
            model.Tick();
            ticks.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            Thread.Sleep(2);
        }

        for (var i = 0; i < 10_000 && model.ResultCount < 300; i++)
            model.Tick();
        Assert.Equal(300, model.ResultCount);
        if (ticks.Count > 2)
        {
            ticks.Sort();
            var median = ticks[ticks.Count / 2];
            Assert.True(median < model.CurrentScanSliceMilliseconds / 2.0, $"median tick {median} ms");
        }
    }
}
