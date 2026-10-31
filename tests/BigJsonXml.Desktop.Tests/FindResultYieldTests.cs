using System.Diagnostics;
using System.Text;
using IOPath = System.IO.Path;

namespace BigJsonXml.Desktop.Tests;

public class FindResultYieldTests
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
    public void Sync_describes_hits_across_ticks_instead_of_all_at_once()
    {
        using var model = new ViewerModel();
        model.OpenPath(WriteItems(20_000, i => i % 2 == 0));
        ScanToEnd(model);
        model.ShowFind();
        model.FindWhat = "needle";
        model.FindAll();

        var sawPartial = false;
        var maxStep = 0;
        var previous = model.ResultCount;
        for (var i = 0; i < 100_000; i++)
        {
            model.Tick();
            var step = model.ResultCount - previous;
            if (step > maxStep)
                maxStep = step;
            if (model.HitCount >= 1_000 && model.ResultCount < model.HitCount)
                sawPartial = true;
            previous = model.ResultCount;
            if (model.HitCount >= 10_000 && !model.SearchRunning && model.ResultCount >= 10_000)
                break;
            if (model.SearchRunning || step > 0)
                Thread.Sleep(1);
        }

        Assert.True(model.HitCount >= 10_000, $"hits {model.HitCount}");
        Assert.True(sawPartial, "listing stayed equal to Hits on every tick");
        Assert.True(maxStep <= ViewerModel.ResultDescribeBudget * 2,
            $"largest listing step {maxStep}");
        Assert.Equal(model.HitCount, model.ResultCount);
    }

    [Fact]
    public void A_tick_that_only_lists_hits_stays_inside_the_scan_slice()
    {
        using var model = new ViewerModel();
        model.OpenPath(WriteItems(20_000, i => i % 2 == 0));
        ScanToEnd(model);
        model.ShowFind();
        model.FindWhat = "needle";
        model.FindAll();

        var ticks = new List<double>();
        for (var i = 0; i < 100_000 && model.ResultCount < 10_000; i++)
        {
            var before = model.ResultCount;
            var started = Stopwatch.GetTimestamp();
            model.Tick();
            var ms = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            if (model.ResultCount > before)
                ticks.Add(ms);
            if (model.SearchRunning)
                Thread.Sleep(1);
            if (i > 20 && ticks.Count == 0 && !model.SearchRunning && model.HitCount == 0)
                break;
        }

        Assert.True(ticks.Count > 0, "no listing ticks observed");
        ticks.Sort();
        var median = ticks[ticks.Count / 2];
        Assert.True(median < model.CurrentScanSliceMilliseconds * 2.0,
            $"median listing tick {median} ms with slice {model.CurrentScanSliceMilliseconds}");
    }

    [Fact]
    public void Cancel_find_stops_further_listing_and_the_search_thread()
    {
        using var model = new ViewerModel();
        model.OpenPath(WriteItems(20_000, i => i % 2 == 0));
        ScanToEnd(model);
        model.ShowFind();
        model.FindWhat = "needle";
        model.FindAll();

        for (var i = 0; i < 100_000 && !(model.HitCount >= 2_000 && model.ResultCount > 0 && model.ResultCount < model.HitCount); i++)
        {
            model.Tick();
            if (model.SearchRunning)
                Thread.Sleep(1);
        }

        Assert.True(model.ResultCount > 0);
        Assert.True(model.ResultCount < model.HitCount, $"listed {model.ResultCount} of {model.HitCount}");
        var listed = model.ResultCount;

        model.CancelFind();
        Assert.False(model.SearchRunning);
        for (var i = 0; i < 30; i++)
            model.Tick();
        Assert.Equal(listed, model.ResultCount);
    }

    [Fact]
    public void Listing_eventually_includes_every_hit()
    {
        using var model = new ViewerModel();
        const int need = 2_500;
        model.OpenPath(WriteItems(need * 2, i => i % 2 == 0));
        ScanToEnd(model);
        model.ShowFind();
        model.FindWhat = "needle";
        model.FindAll();

        for (var i = 0; i < 200_000 && (model.SearchRunning || model.ListingResults || model.HitCount < need); i++)
        {
            model.Tick();
            if (model.SearchRunning)
                Thread.Sleep(1);
        }

        Assert.True(model.HitCount >= need, $"hits {model.HitCount}");
        Assert.Equal(model.HitCount, model.ResultCount);
        var document = Assert.Single(model.ResultDocuments);
        Assert.Equal(model.HitCount, document.MatchCount);
        Assert.Equal(model.HitCount.ToString() + " matches", document.MatchLabel);
        Assert.DoesNotContain("Showing first", document.MatchLabel, StringComparison.Ordinal);
    }

    [Fact]
    public void Find_next_stays_snappy_before_the_whole_list_is_built()
    {
        using var model = new ViewerModel();
        model.OpenPath(WriteItems(10_000, i => i % 10 == 0));
        ScanToEnd(model);
        model.ShowFind();
        model.FindWhat = "needle";
        model.FindAll();

        for (var i = 0; i < 100_000 && model.ResultCount == 0; i++)
        {
            model.Tick();
            if (model.SearchRunning)
                Thread.Sleep(1);
        }

        Assert.True(model.ResultCount > 0);
        Assert.True(model.ResultCount < 1_000 || model.HitCount > model.ResultCount);

        model.FindNext();
        Assert.NotNull(model.CurrentResult);
        Assert.Equal("needle", model.CurrentResult.Text);
    }
}
