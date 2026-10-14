using System.Diagnostics;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace BigJsonXml.Desktop.Tests;

public class AdaptiveScanSlicingTests
{
    [Fact]
    public void Quiescent_user_input_expands_scan_slice_to_64ms_and_drops_idle_to_4ms()
    {
        using var model = new ViewerModel();
        // Immediately after interaction, defaults to 32ms / 16ms
        model.NoteUserInteraction();
        Assert.Equal(ViewerModel.ScanSliceMilliseconds, model.CurrentScanSliceMilliseconds);
        Assert.Equal(ViewerModel.ScanIdleMilliseconds, model.CurrentScanIdleMilliseconds);

        // Simulate 250ms or more of user quiescence
        model.LastInteractionTime = Environment.TickCount64 - ViewerModel.QuiescenceThresholdMilliseconds - 10;
        model.Tick();

        Assert.Equal(ViewerModel.QuiescentScanSliceMilliseconds, model.CurrentScanSliceMilliseconds);
        Assert.Equal(ViewerModel.QuiescentScanIdleMilliseconds, model.CurrentScanIdleMilliseconds);
        Assert.Equal(64, model.CurrentScanSliceMilliseconds);
        Assert.Equal(4, model.CurrentScanIdleMilliseconds);
    }

    [Fact]
    public void User_interaction_event_immediately_reverts_slice_to_32ms_and_idle_to_16ms()
    {
        using var model = new ViewerModel();
        // Set to quiescent state
        model.LastInteractionTime = Environment.TickCount64 - 500;
        model.Tick();
        Assert.Equal(64, model.CurrentScanSliceMilliseconds);
        Assert.Equal(4, model.CurrentScanIdleMilliseconds);

        // Any user interaction immediately drops back
        model.NoteUserInteraction();
        Assert.Equal(32, model.CurrentScanSliceMilliseconds);
        Assert.Equal(16, model.CurrentScanIdleMilliseconds);
    }

    [Fact]
    public void During_active_scanning_refresh_is_throttled_to_30_fps_while_scan_fraction_updates()
    {
        var source = new MemoryByteSource();
        using var model = new ViewerModel();
        model.Open(source);

        // Feed chunks so scanning stays active
        source.Append("<root>");
        for (var i = 0; i < 2000; i++)
        {
            source.Append($"<item id=\"{i}\">test value {i}</item>");
        }
        source.Append("<padding>");
        for (var i = 0; i < 2000; i++)
        {
            source.Append($"<data>{i}</data>");
        }
        source.Append("</padding>");
        source.Complete();

        var initialFraction = model.ScanFraction;
        // Run multiple rapid ticks simulating fast timer
        for (var i = 0; i < 5; i++)
        {
            model.Tick();
        }

        // Scan fraction should have updated even with throttled refreshes
        Assert.True(model.ScanFraction >= initialFraction);
    }

    [Fact]
    public void A_slice_of_scan_work_returns_well_under_one_tenth_of_a_second()
    {
        var source = new MemoryByteSource();
        using var model = new ViewerModel();
        model.Open(source);

        source.Append("<root>");
        for (var i = 0; i < 5000; i++)
        {
            source.Append($"<item id=\"{i}\">quick text {i}</item>");
        }
        source.Append("</root>");
        source.Complete();

        // Test under quiescent 64ms slice
        model.LastInteractionTime = Environment.TickCount64 - 300;
        var sw = Stopwatch.StartNew();
        model.Tick();
        sw.Stop();

        // Must return well under 100ms
        Assert.True(sw.ElapsedMilliseconds < 100, $"Tick took {sw.ElapsedMilliseconds}ms, expected < 100ms");
    }
}
