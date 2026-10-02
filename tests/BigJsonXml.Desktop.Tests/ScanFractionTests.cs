using System.Globalization;
using System.Text;

namespace BigJsonXml.Desktop.Tests;

public class ScanFractionTests
{
    [Fact]
    public void A_small_document_finishes_as_Complete_with_no_bar()
    {
        var source = new MemoryByteSource();
        source.Append("<root><a>1</a></root>");
        source.Complete();
        using var model = new ViewerModel();
        var sawEmpty = false;
        string? beforeAnyByte = null;
        model.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(ViewerModel.Status))
                return;
            if (model.Status != "Scanning" || !model.ScanFractionShown || model.ScanFraction != 0 || beforeAnyByte is not null)
                return;
            sawEmpty = true;
            model.SetScanElapsed(5_000);
            beforeAnyByte = model.RemainingTime;
        };

        model.Open(source);

        Assert.True(sawEmpty);
        Assert.Equal("", beforeAnyByte);
        Assert.Equal("Complete", model.Status);
        Assert.False(model.ScanFractionShown);
        Assert.Equal(0, model.ScanFraction);
        Assert.Equal("", model.RemainingTime);
        Assert.Equal("", model.Error);
    }

    [Fact]
    public void A_growing_source_has_no_bar_until_its_length_is_fixed()
    {
        var source = new MemoryByteSource();
        using var model = new ViewerModel();
        model.Open(source);

        Assert.Equal("Scanning", model.Status);
        Assert.False(model.ScanFractionShown);
        Assert.Equal("", model.RemainingTime);

        source.Append("<root>");
        AppendCopies(source, "<a>1</a>"u8, 498);
        source.Append("<a>xxx</a>");
        model.Tick();
        Assert.Equal("Scanning", model.Status);
        Assert.False(model.ScanFractionShown);
        Assert.Equal("", model.RemainingTime);
        Assert.Equal(4000, source.Length);

        AppendPad(source);
        source.Complete();
        model.SetScanElapsed(999);

        Assert.Equal("Scanning", model.Status);
        Assert.True(model.ScanFractionShown);
        Assert.Equal(4000d / 5000d, model.ScanFraction);
        Assert.Equal("", model.RemainingTime);
    }

    [Fact]
    public void The_bar_advances_with_file_bytes_and_stops_at_the_end()
    {
        var source = LargeDocument();
        using var model = new ViewerModel();
        model.Open(source);

        Assert.Equal("Scanning", model.Status);
        Assert.True(model.ScanFractionShown);
        Assert.InRange(model.ScanFraction, 0, 1);
        var first = model.ScanFraction;
        var moved = false;
        for (var i = 0; i < 8 && model.Status == "Scanning"; i++)
        {
            model.Tick();
            if (model.Status != "Scanning")
                break;
            Assert.InRange(model.ScanFraction, 0, 1);
            if (model.ScanFraction > first)
            {
                moved = true;
                break;
            }
        }

        Assert.True(moved);
        Assert.True(model.ScanFraction <= 1);
    }

    [Fact]
    public void Remaining_time_uses_bytes_still_ahead_and_the_supplied_elapsed_time()
    {
        using var held = OpenHeld();
        var model = held.Model;
        AppendPad(held.Source);
        held.Source.Complete();

        model.SetScanElapsed(999);
        Assert.Equal("", model.RemainingTime);
        Assert.Equal(4000d / 5000d, model.ScanFraction);

        model.SetScanElapsed(1000);
        Assert.Equal("<1s remaining", model.RemainingTime);
        Assert.Equal("Scanning", model.Status);

        model.SetScanElapsed(4000);
        Assert.Equal("1s remaining", model.RemainingTime);

        model.SetScanElapsed(4001);
        Assert.Equal("2s remaining", model.RemainingTime);

        model.SetScanElapsed(8000);
        Assert.Equal("2s remaining", model.RemainingTime);

        model.SetScanElapsed(398000);
        Assert.Equal("100s remaining", model.RemainingTime);
        Assert.DoesNotContain("left", model.RemainingTime, StringComparison.Ordinal);
        Assert.DoesNotContain("%", model.RemainingTime, StringComparison.Ordinal);
        Assert.DoesNotContain(",", model.RemainingTime, StringComparison.Ordinal);
        Assert.NotEqual("0s remaining", model.RemainingTime);
        Assert.True(model.RemainingTime.Length > "1s remaining".Length);

        while (model.Status == "Scanning")
            model.Tick();

        Assert.Equal("Complete", model.Status);
        Assert.False(model.ScanFractionShown);
        Assert.Equal("", model.RemainingTime);
    }

    [Fact]
    public void Opening_another_document_resets_the_fraction_and_the_one_second_gate()
    {
        using var held = OpenHeld();
        var model = held.Model;
        AppendPad(held.Source);
        held.Source.Complete();
        model.SetScanElapsed(4000);

        Assert.True(model.ScanFractionShown);
        Assert.Equal("1s remaining", model.RemainingTime);

        var other = new MemoryByteSource();
        other.Append("<root><item>beta</item>");
        model.Open(other);

        Assert.Equal("Scanning", model.Status);
        Assert.False(model.ScanFractionShown);
        Assert.Equal("", model.RemainingTime);

        other.Append("</root>");
        other.Complete();
        model.SetScanElapsed(999);
        Assert.Equal("", model.RemainingTime);
        Assert.True(model.ScanFractionShown);

        model.SetScanElapsed(5000);
        Assert.Equal("Scanning", model.Status);
        Assert.NotEqual("", model.RemainingTime);
        Assert.DoesNotContain("0s remaining", model.RemainingTime, StringComparison.Ordinal);
    }

    [Fact]
    public void A_stop_in_the_middle_keeps_the_fraction_and_drops_the_remaining_time()
    {
        var source = new MemoryByteSource();
        source.Append("<root>");
        AppendCopies(source, "<a>1</a>"u8, 40);
        source.Append("<<");
        AppendCopies(source, "<a>1</a>"u8, 40);
        source.Append("</root>");
        source.Complete();
        using var model = new ViewerModel();
        model.Open(source);
        model.SetScanElapsed(20000);

        Assert.Equal("Stopped", model.Status);
        Assert.True(model.ScanFractionShown);
        Assert.InRange(model.ScanFraction, 0.2, 0.8);
        Assert.Equal("", model.RemainingTime);
        Assert.Matches(@"\(line \d+, column \d+\)$", model.Error);
        Assert.DoesNotContain("0s remaining", model.RemainingTime, StringComparison.Ordinal);
        var fraction = model.ScanFraction;
        model.Tick();
        model.SetScanElapsed(40000);
        Assert.Equal("Stopped", model.Status);
        Assert.Equal(fraction, model.ScanFraction);
        Assert.True(model.ScanFractionShown);
        Assert.Equal("", model.RemainingTime);
        Assert.Matches(@"\(line \d+, column \d+\)$", model.Error);
    }

    [Fact]
    public void A_stop_near_the_end_leaves_a_nearly_full_bar()
    {
        var source = new MemoryByteSource();
        source.Append("<root>");
        AppendCopies(source, "<a>1</a>"u8, 500);
        source.Append("<<");
        source.Append("</root>");
        source.Complete();
        using var model = new ViewerModel();
        model.Open(source);

        Assert.Equal("Stopped", model.Status);
        Assert.True(model.ScanFractionShown);
        Assert.InRange(model.ScanFraction, 0.9, 1);
        Assert.True(model.ScanFraction < 1);
        Assert.Equal("", model.RemainingTime);
        Assert.Matches(@"\(line \d+, column \d+\)$", model.Error);
        var fraction = model.ScanFraction;
        model.Tick();
        Assert.Equal(fraction, model.ScanFraction);
    }

    [Fact]
    public void A_stop_before_any_byte_leaves_an_empty_bar()
    {
        var source = new MemoryByteSource();
        source.Append("nope");
        source.Complete();
        using var model = new ViewerModel();
        model.Open(source);
        model.SetScanElapsed(5000);

        Assert.Equal("Stopped", model.Status);
        Assert.True(model.ScanFractionShown);
        Assert.Equal(0, model.ScanFraction);
        Assert.Equal("", model.RemainingTime);
        Assert.Contains("no root element", model.Error, StringComparison.Ordinal);
        Assert.Matches(@"\(line \d+, column \d+\)$", model.Error);
        model.Tick();
        Assert.Equal(0, model.ScanFraction);
        Assert.True(model.ScanFractionShown);
    }

    [Fact]
    public void A_document_of_length_zero_is_Stopped_with_no_bar()
    {
        var source = new MemoryByteSource();
        source.Complete();
        using var model = new ViewerModel();
        model.Open(source);
        model.SetScanElapsed(5000);

        Assert.Equal("Stopped", model.Status);
        Assert.False(model.ScanFractionShown);
        Assert.Equal("", model.RemainingTime);
        Assert.Contains("no root element", model.Error, StringComparison.Ordinal);
        Assert.Matches(@"\(line \d+, column \d+\)$", model.Error);
    }

    [Fact]
    public void An_external_entity_stops_the_scan_and_freezes_the_bar()
    {
        var source = new MemoryByteSource();
        source.Append("""
            <!DOCTYPE root [
              <!ENTITY xxe SYSTEM "file:///C:/Windows/win.ini">
            ]>
            <root><a>1</a>&xxe;<b>2</b></root>
            """);
        source.Complete();
        using var model = new ViewerModel();
        model.Open(source);
        model.SetScanElapsed(9000);

        Assert.Equal("Stopped", model.Status);
        Assert.True(model.ScanFractionShown);
        Assert.InRange(model.ScanFraction, 0, 1);
        Assert.True(model.ScanFraction > 0);
        Assert.True(model.ScanFraction < 1);
        Assert.Equal("", model.RemainingTime);
        Assert.Contains("External", model.Error, StringComparison.Ordinal);
        Assert.Matches(@"\(line \d+, column \d+\)$", model.Error);
        var fraction = model.ScanFraction;
        model.Tick();
        Assert.Equal(fraction, model.ScanFraction);
        Assert.Equal("Stopped", model.Status);
    }

    [Fact]
    public void An_internal_entity_holds_the_fraction_until_the_scan_finishes()
    {
        var source = EntityDocument(60_000);
        var length = source.Length;
        using var model = new ViewerModel();
        model.Open(source);

        Assert.Equal("Scanning", model.Status);
        var previous = model.ScanFraction;
        var stalled = false;
        for (var i = 0; i < 80 && model.Status == "Scanning"; i++)
        {
            model.Tick();
            if (model.Status != "Scanning")
                break;
            if (previous > 0 && previous < 1 && model.ScanFraction == previous)
            {
                stalled = true;
                break;
            }

            previous = model.ScanFraction;
        }

        Assert.True(stalled, $"status={model.Status} fraction={model.ScanFraction} error={model.Error}");
        var held = model.ScanFraction;
        model.Tick();
        Assert.Equal("Scanning", model.Status);
        Assert.Equal(held, model.ScanFraction);

        var reached = Recover(held, length);
        var ahead = length - reached;
        Assert.True(reached > 0);
        Assert.True(ahead > 0);
        model.SetScanElapsed(1000);
        var first = model.RemainingTime;
        Assert.Equal(Expect(length, reached, 1000), first);
        var longerElapsed = 1000 * reached;
        model.SetScanElapsed(longerElapsed);
        var second = model.RemainingTime;
        Assert.Equal(Expect(length, reached, longerElapsed), second);
        Assert.True(Rank(second) > Rank(first));
        Assert.Equal("Scanning", model.Status);
        Assert.Equal(held, model.ScanFraction);

        for (var i = 0; i < 200 && model.Status == "Scanning"; i++)
            model.Tick();

        Assert.Equal("Complete", model.Status);
        Assert.False(model.ScanFractionShown);
        Assert.Equal("", model.RemainingTime);
    }

    [Fact]
    public void Search_and_scroll_work_while_the_bar_is_moving()
    {
        var source = LargeDocument();
        using var model = new ViewerModel();
        model.Open(source);

        Assert.Equal("Scanning", model.Status);
        Assert.True(model.ScanFractionShown);
        model.FindWhat = "findme";
        model.FindAll();

        Assert.Equal("Scanning", model.Status);
        Assert.True(model.ScanFractionShown);
        Assert.True(model.ResultCount >= 1);
        Assert.Contains(model.Results, result => result.Text.Contains("findme", StringComparison.Ordinal));

        model.SetViewportSize(400, 80);
        model.ScrollY = 200;
        Assert.True(model.ScrollY > 0);
        Assert.Equal("Scanning", model.Status);
        Assert.True(model.ScanFractionShown);
        Assert.InRange(model.ScanFraction, 0, 1);
    }

    private sealed class HeldScan : IDisposable
    {
        public ViewerModel Model { get; } = new();

        public MemoryByteSource Source { get; } = new();

        public void Dispose() => Model.Dispose();
    }

    private static HeldScan OpenHeld()
    {
        var held = new HeldScan();
        held.Source.Append("<root>");
        AppendCopies(held.Source, "<a>1</a>"u8, 498);
        held.Source.Append("<a>xxx</a>");
        held.Model.Open(held.Source);
        return held;
    }

    private static void AppendPad(MemoryByteSource source)
    {
        var pad = new byte[986];
        Array.Fill(pad, (byte)'x');
        source.Append("<a>"u8);
        source.Append(pad);
        source.Append("</a></root>"u8);
    }

    private static MemoryByteSource LargeDocument()
    {
        var source = new MemoryByteSource();
        source.Append("<root>");
        for (var i = 0; i < 80; i++)
            source.Append(Encoding.UTF8.GetBytes($"<f{i}>v</f{i}>"));
        source.Append("<a>findme</a>");
        AppendCopies(source, "<a>1</a>"u8, 1_000_000);
        source.Append("</root>");
        source.Complete();
        return source;
    }

    private static MemoryByteSource EntityDocument(int elements)
    {
        var source = new MemoryByteSource();
        source.Append("<!DOCTYPE root [<!ENTITY e \"");
        AppendCopies(source, "<a>x</a>"u8, elements);
        source.Append("\">]><root>&e;</root>");
        source.Complete();
        return source;
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

    private static long Recover(double fraction, long length)
    {
        var guess = (long)Math.Round(fraction * length);
        for (var delta = -3L; delta <= 3; delta++)
        {
            var candidate = guess + delta;
            if (candidate >= 0 && candidate <= length && (double)candidate / length == fraction)
                return candidate;
        }

        throw new InvalidOperationException($"Could not recover a file position from {fraction} of {length}.");
    }

    private static string Expect(long length, long reached, long elapsed)
    {
        if (elapsed < 1000 || reached <= 0)
            return "";
        var ahead = length - reached;
        if (ahead <= 0)
            return "";
        var quotient = (decimal)ahead * elapsed / reached;
        if (quotient <= 0)
            return "";
        if (quotient < 1000)
            return "<1s remaining";
        var seconds = (long)decimal.Ceiling(quotient / 1000m);
        return seconds.ToString(CultureInfo.InvariantCulture) + "s remaining";
    }

    private static int Rank(string text)
    {
        if (text == "<1s remaining")
            return 0;
        var mark = text.IndexOf('s');
        return int.Parse(text[..mark], CultureInfo.InvariantCulture);
    }
}
