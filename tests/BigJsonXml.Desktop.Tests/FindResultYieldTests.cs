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

    private static void WaitForHits(ViewerModel model, int hits)
    {
        for (var i = 0; i < 100_000 && model.HitCount < hits; i++)
        {
            model.Tick();
            if (model.SearchRunning)
                Thread.Sleep(1);
        }

        Assert.True(model.HitCount >= hits, $"hits {model.HitCount}");
    }

    [Fact]
    public void Hit_count_and_list_extent_update_without_describing_every_row()
    {
        using var model = new ViewerModel();
        model.OpenPath(WriteItems(20_000, i => i % 2 == 0));
        ScanToEnd(model);
        model.ShowFind();
        model.FindWhat = "needle";
        model.FindAll();
        WaitForHits(model, 10_000);

        Assert.Equal(model.HitCount, model.ResultCount);
        Assert.True(model.VisibleResults.Count >= 1 + 10_000, $"visible extent {model.VisibleResults.Count}");
        Assert.Equal(0, model.DescribedResultCount);
        var document = Assert.Single(model.ResultDocuments);
        Assert.Equal(model.HitCount, document.MatchCount);
    }

    [Fact]
    public void Indexer_returns_a_placeholder_without_reading_hit_text()
    {
        using var model = new ViewerModel();
        model.OpenPath(WriteItems(20_000, i => i % 2 == 0));
        ScanToEnd(model);
        model.ShowFind();
        model.FindWhat = "needle";
        model.FindAll();
        WaitForHits(model, 10_000);

        var started = Stopwatch.GetTimestamp();
        var far = Assert.IsType<ResultHitLine>(model.VisibleResults[5_000]);
        var ms = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        Assert.False(far.Result.IsReady);
        Assert.Equal("\u2026", far.Result.Text);
        Assert.Equal(0, model.DescribedResultCount);
        Assert.True(ms < 5, $"indexer took {ms} ms");
    }

    [Fact]
    public void Tick_fills_placeholders_near_the_requested_row()
    {
        using var model = new ViewerModel();
        model.OpenPath(WriteItems(20_000, i => i % 2 == 0));
        ScanToEnd(model);
        model.ShowFind();
        model.FindWhat = "needle";
        model.FindAll();
        WaitForHits(model, 10_000);

        var far = Assert.IsType<ResultHitLine>(model.VisibleResults[5_000]);
        Assert.False(far.Result.IsReady);

        for (var i = 0; i < 200 && !far.Result.IsReady; i++)
            model.Tick();

        Assert.True(far.Result.IsReady);
        Assert.Equal("needle", far.Result.Text);
        Assert.True(model.DescribedResultCount > 0);
        Assert.True(model.DescribedResultCount < 700,
            $"described {model.DescribedResultCount} — should stay near the prefetch window");
    }

    [Fact]
    public void A_tick_that_only_publishes_hits_stays_inside_the_scan_slice()
    {
        using var model = new ViewerModel();
        model.OpenPath(WriteItems(20_000, i => i % 2 == 0));
        ScanToEnd(model);
        model.ShowFind();
        model.FindWhat = "needle";
        model.FindAll();

        var ticks = new List<double>();
        for (var i = 0; i < 100_000 && model.HitCount < 10_000; i++)
        {
            var started = Stopwatch.GetTimestamp();
            model.Tick();
            ticks.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            if (model.SearchRunning)
                Thread.Sleep(1);
        }

        Assert.True(ticks.Count > 0);
        ticks.Sort();
        var median = ticks[ticks.Count / 2];
        Assert.True(median < model.CurrentScanSliceMilliseconds * 2.0,
            $"median tick {median} ms with slice {model.CurrentScanSliceMilliseconds}");
        Assert.Equal(0, model.DescribedResultCount);
    }

    [Fact]
    public void Cancel_find_stops_the_search_thread()
    {
        using var model = new ViewerModel();
        model.OpenPath(WriteItems(200_000, i => i % 2 == 0));
        ScanToEnd(model);
        model.ShowFind();
        model.FindWhat = "needle";
        model.FindAll();

        for (var i = 0; i < 100_000 && !model.SearchRunning && model.HitCount == 0; i++)
        {
            model.Tick();
            Thread.Sleep(1);
        }

        model.CancelFind();
        Assert.False(model.SearchRunning);
    }

    [Fact]
    public void Find_next_ensures_the_current_hit_body()
    {
        using var model = new ViewerModel();
        model.OpenPath(WriteItems(10_000, i => i % 10 == 0));
        ScanToEnd(model);
        model.ShowFind();
        model.FindWhat = "needle";
        model.FindAll();
        WaitForHits(model, 100);

        model.FindNext();
        Assert.NotNull(model.CurrentResult);
        Assert.True(model.CurrentResult.IsReady);
        Assert.Equal("needle", model.CurrentResult.Text);
        Assert.Equal(0, model.CurrentHitIndex);
    }

    [Fact]
    public void Close_find_after_many_hits_returns_quickly()
    {
        using var model = new ViewerModel();
        model.OpenPath(WriteItems(40_000, i => i % 2 == 0));
        ScanToEnd(model);
        model.ShowFind();
        model.FindWhat = "needle";
        model.FindAll();
        WaitForHits(model, 20_000);
        _ = model.VisibleResults[1];
        _ = model.VisibleResults[10_000];
        for (var i = 0; i < 5; i++)
            model.Tick();

        var started = Stopwatch.GetTimestamp();
        model.CloseFind();
        var ms = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        Assert.False(model.FindWindowOpen);
        Assert.False(model.ResultsWindowOpen);
        Assert.Empty(model.VisibleResults);
        Assert.True(ms < 500, $"CloseFind took {ms} ms with {model.HitCount} hits");
    }

    [Fact]
    public void Release_result_views_drops_the_visible_extent_immediately()
    {
        using var model = new ViewerModel();
        model.OpenPath(WriteItems(60_000, i => i % 2 == 0));
        ScanToEnd(model);
        model.ShowFind();
        model.FindWhat = "needle";
        model.FindAll();
        WaitForHits(model, 30_000);
        Assert.True(model.VisibleResults.Count > 30_000);
        _ = model.VisibleResults[500];

        var started = Stopwatch.GetTimestamp();
        model.ReleaseResultViews();
        var ms = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        Assert.Empty(model.VisibleResults);
        Assert.True(ms < 100, $"ReleaseResultViews took {ms} ms");
        // Hits remain for Find Next after the window is gone.
        Assert.True(model.HitCount >= 30_000);
        Assert.Equal("needle", model.Results[0].Text);
    }

    [Fact]
    public void Close_results_clears_visible_rows_without_dropping_hits()
    {
        using var model = new ViewerModel();
        model.OpenPath(WriteItems(40_000, i => i % 2 == 0));
        ScanToEnd(model);
        model.ShowFind();
        model.FindWhat = "needle";
        model.FindAll();
        WaitForHits(model, 20_000);
        Assert.True(model.VisibleResults.Count > 20_000);

        var started = Stopwatch.GetTimestamp();
        model.CloseResults();
        var ms = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        Assert.False(model.ResultsWindowOpen);
        Assert.True(model.FindWindowOpen);
        Assert.Empty(model.VisibleResults);
        Assert.True(model.HitCount >= 20_000);
        Assert.True(ms < 500, $"CloseResults took {ms} ms with {model.HitCount} hits");
    }

    [Fact]
    public void Settle_fill_describes_the_requested_viewport_first()
    {
        using var model = new ViewerModel();
        model.OpenPath(WriteItems(40_000, i => i % 2 == 0));
        ScanToEnd(model);
        model.ShowFind();
        model.FindWhat = "needle";
        model.FindAll();
        WaitForHits(model, 20_000);

        // Simulate a jump: queue a far viewport, then settle-fill once.
        model.PrefetchResultRange(9_000, 9_030);
        var started = Stopwatch.GetTimestamp();
        model.PumpResultDescribeSettled();
        var ms = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        Assert.True(ms < 50, $"settle fill took {ms} ms");
        // Settle pump should paint most of a ~30-row viewport in one shot (no EnsureDescribed).
        Assert.True(model.DescribedResultCount >= 8,
            $"described {model.DescribedResultCount} after settle fill");
    }
}
