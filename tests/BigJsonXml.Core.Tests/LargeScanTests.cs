using System.Diagnostics;
using System.Text;

namespace BigJsonXml.Tests;

public class LargeScanRuleTests
{
    [Fact]
    public void Carriage_return_gaps_are_not_fields()
    {
        var session = SessionFixture.Scan("<root>\r<child>  hi\r\n there  </child>\r\n\t<next>x</next>\r</root>");

        var fields = session.ReadRecord(DocumentPath.Root).Fields;

        Assert.Equal(["child", "next"], fields.Select(field => field.Name).ToArray());
        Assert.Equal("  hi\n there  ", fields[0].Text);
        Assert.Equal("x", fields[1].Text);
    }

    [Fact]
    public void Preserve_does_not_turn_a_carriage_return_gap_into_text()
    {
        var session = SessionFixture.Scan("<root xml:space=\"preserve\">\r\n<child> hi </child>\r</root>");

        var fields = session.ReadRecord(DocumentPath.Root).Fields;

        Assert.Equal(["@xml:space", "child"], fields.Select(field => field.Name).ToArray());
        Assert.Equal(" hi ", fields[1].Text);
    }

    [Fact]
    public void Mixed_content_keeps_breaks_beside_real_text()
    {
        var session = SessionFixture.Scan("<root id=\"1\">\r\n hello \r<child>x</child>\n world \r\n</root>");

        var fields = session.ReadRecord(DocumentPath.Root).Fields;

        Assert.Equal(["@id", "#text", "child"], fields.Select(field => field.Name).ToArray());
        Assert.Equal("\n hello \n\n world \n", fields[1].Text);
        Assert.Equal("x", fields[2].Text);
    }

    [Fact]
    public void A_break_on_line_three_is_reported_on_that_line()
    {
        var session = SessionFixture.Scan("<root>\r\n<a>1</a>\r\n<<b></b>\r\n</root>");

        Assert.Equal(ScanProgress.Stopped, session.Progress);
        Assert.NotNull(session.Error);
        Assert.Equal(3, session.Error.Line);
        Assert.Equal("1", session.Field("a").Text);
        Assert.DoesNotContain(session.ReadRecord(DocumentPath.Root).Fields, field => field.Name == "b");
    }

    [Fact]
    public void Collapsing_a_long_list_keeps_the_place_of_a_later_entry()
    {
        const int count = 20_000;
        var xml = new StringBuilder();
        xml.Append("<root>");
        for (var i = 0; i < count; i++)
            xml.Append(i == count - 1 ? "<i>end</i>" : "<i>v</i>");
        xml.Append("</root>");
        var session = SessionFixture.Scan(xml.ToString());
        var list = DocumentPath.Root.Field("i");
        session.Expand(list);
        session.Collapse(list);
        session.Expand(list);

        var before = GC.GetAllocatedBytesForCurrentThread();
        var slice = session.ReadList(list, count - 5, 5);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal("end", slice.Entries[^1].Cells.Single(cell => cell.Name == "#text").Text);
        Assert.Equal(count, slice.EntryCount);
        Assert.True(allocated < 400_000, $"Reading the tail after collapse allocated {allocated} bytes.");

        session.Collapse(list);
        session.Search(new TextSearch { FindWhat = "end" });
        session.PumpAll();
        var hit = Assert.Single(session.Hits);
        Assert.Equal(list.WithIndex(count).Field("#text"), hit.Path);
    }

    [Fact]
    public void Opening_a_closed_record_uses_the_list_already_scanned()
    {
        const int count = 20_000;
        var xml = new StringBuilder();
        xml.Append("<root><box>");
        for (var i = 0; i < count; i++)
            xml.Append(i == count - 1 ? "<i id=\"end\">z</i>" : "<i id=\"n\">v</i>");
        xml.Append("</box><tail>1</tail></root>");
        var session = SessionFixture.Scan(xml.ToString());
        var box = DocumentPath.Root.Field("box");

        var closed = session.ReadRecord(box);
        var listField = closed.Fields.Single(field => field.Name == "i");
        Assert.Equal(FieldKind.List, listField.Kind);
        Assert.Equal(count, listField.ListCount);
        Assert.Equal("1", session.Field("tail").Text);

        session.Expand(box);
        session.Collapse(box);

        var before = GC.GetAllocatedBytesForCurrentThread();
        var again = session.ReadRecord(box);
        var list = box.Field("i");
        session.Expand(list);
        var tail = session.ReadList(list, count - 1, 1);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(count, again.Fields.Single(field => field.Name == "i").ListCount);
        Assert.Equal(["@id", "#text"], tail.Columns);
        Assert.Equal("end", tail.Entries[0].Cells.Single(cell => cell.Name == "@id").Text);
        Assert.Equal("z", tail.Entries[0].Cells.Single(cell => cell.Name == "#text").Text);
        Assert.True(allocated < 500_000, $"Reopening the record allocated {allocated} bytes.");
    }

    [Fact]
    public void Opening_one_nested_record_does_not_walk_the_rest_of_the_document()
    {
        const int count = 4_000;
        var xml = new StringBuilder();
        xml.Append("<root>");
        for (var i = 0; i < count; i++)
            xml.Append($"<n{i}><a>v{i}</a></n{i}>");
        xml.Append("</root>");
        var session = SessionFixture.Scan(xml.ToString());
        var path = DocumentPath.Root.Field($"n{count - 1}");

        var before = GC.GetAllocatedBytesForCurrentThread();
        var clock = Stopwatch.StartNew();
        var record = session.ReadRecord(path);
        clock.Stop();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal($"v{count - 1}", record.Fields.Single(field => field.Name == "a").Text);
        Assert.True(clock.ElapsedMilliseconds < 250, $"Opening one record took {clock.ElapsedMilliseconds} ms.");
        Assert.True(allocated < 1_000_000, $"Opening one record allocated {allocated} bytes.");
    }

    [Fact]
    public void Opening_a_list_entry_does_not_parse_a_nested_field_body()
    {
        const int count = 200_000;
        var xml = new StringBuilder();
        xml.Append("<root><item id=\"1\"><title>T</title><deep>");
        xml.Append("<head>H</head><pad note=\"a&gt;b\"><!-- <ghost></ghost> --><![CDATA[<ghost>]]>");
        for (var i = 0; i < count; i++)
            xml.Append("<g><!--. --><h>0123456789abcdefghij</h></g>");
        xml.Append("<mark>M</mark></pad></deep><after>A</after></item><item id=\"2\"><title>U</title></item></root>");
        var session = SessionFixture.Scan(xml.ToString());
        var list = DocumentPath.Root.Field("item");
        session.Expand(list);

        var clock = Stopwatch.StartNew();
        var cells = session.ReadList(list, 0, 1).Entries[0].Cells;
        clock.Stop();

        Assert.Equal("T", cells.Single(cell => cell.Name == "title").Text);
        Assert.Equal(FieldKind.Record, cells.Single(cell => cell.Name == "deep").Kind);
        Assert.Equal("A", cells.Single(cell => cell.Name == "after").Text);
        Assert.True(clock.ElapsedMilliseconds < 150, $"Opening the entry took {clock.ElapsedMilliseconds} ms.");

        var deep = list.WithIndex(1).Field("deep");
        session.Expand(deep);
        var nested = session.ReadRecord(deep);
        Assert.Equal("H", nested.Fields.Single(field => field.Name == "head").Text);
        Assert.Equal(FieldKind.Record, nested.Fields.Single(field => field.Name == "pad").Kind);

        var pad = deep.Field("pad");
        session.Expand(pad);
        var inside = session.ReadRecord(pad);
        Assert.Equal("a>b", inside.Fields.Single(field => field.Name == "@note").Text);
        Assert.Equal("M", inside.Fields.Single(field => field.Name == "mark").Text);
        Assert.Equal(count, inside.Fields.Single(field => field.Name == "g").ListCount);
        Assert.DoesNotContain(inside.Fields, field => field.Name == "ghost");

        var rows = pad.Field("g");
        session.Expand(rows);
        var entry = session.ReadList(rows, count - 1, 1).Entries[0];
        Assert.Equal("0123456789abcdefghij", entry.Cells.Single(cell => cell.Name == "h").Text);
    }
}

[Collection("large-scan")]
public class LargeScanTimeTests
{
    [Fact]
    public void A_file_scan_reads_forward_and_one_slice_stays_short()
    {
        var path = ScanFiles.WriteRepeated(32L * 1024 * 1024);
        try
        {
            using var session = ViewerSession.Open(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
            var slice = Stopwatch.StartNew();
            Assert.True(session.Pump(256));
            slice.Stop();
            Assert.Equal(ScanProgress.Scanning, session.Progress);
            Assert.True(slice.ElapsedMilliseconds < 80, $"One slice took {slice.ElapsedMilliseconds} ms.");

            var clock = Stopwatch.StartNew();
            session.PumpAll();
            clock.Stop();
            Assert.Equal(ScanProgress.Completed, session.Progress);
            Assert.Null(session.Error);
            Assert.True(clock.Elapsed.TotalSeconds < 4, $"The file scan took {clock.Elapsed.TotalSeconds:F2} s.");

            var list = DocumentPath.Root.Field("item");
            var field = session.ReadRecord(DocumentPath.Root).Fields.Single();
            Assert.Equal(FieldKind.List, field.Kind);
            Assert.True(field.ListCount > 1000);
            session.Expand(list);
            session.Collapse(list);
            session.Expand(list);
            var tail = session.ReadList(list, field.ListCount!.Value - 1, 1);
            Assert.Equal("hello", tail.Entries[0].Cells.Single(cell => cell.Name == "value").Text);

            var note = ScanFiles.WriteNote();
            try
            {
                using var positioned = ViewerSession.Open(File.Open(note, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
                positioned.PumpAll();
                var body = positioned.ReadFullValueSlice(DocumentPath.Root.Field("note"), 80, 6);
                Assert.Equal("NEEDLE", body);
            }
            finally
            {
                File.Delete(note);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Search_of_a_file_reads_forward_and_returns_paths_in_order()
    {
        var path = ScanFiles.WriteRepeated(8L * 1024 * 1024, lastValue: "NEEDLE");
        try
        {
            using var session = ViewerSession.Open(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
            session.PumpAll();
            var count = session.ReadRecord(DocumentPath.Root).Fields.Single().ListCount!.Value;

            var clock = Stopwatch.StartNew();
            session.Search(new TextSearch { FindWhat = "NEEDLE" });
            session.PumpAll();
            clock.Stop();
            var textHit = Assert.Single(session.Hits);
            Assert.Equal(DocumentPath.Root.Field("item").WithIndex(count).Field("value"), textHit.Path);
            Assert.True(clock.Elapsed.TotalSeconds < 3, $"Text search took {clock.Elapsed.TotalSeconds:F2} s.");

            clock.Restart();
            session.Search(new TextSearch { FindWhat = "NEEDLE", FieldName = "value" });
            session.PumpAll();
            clock.Stop();
            var fieldHit = Assert.Single(session.Hits);
            Assert.Equal(textHit.Path, fieldHit.Path);
            Assert.True(clock.Elapsed.TotalSeconds < 3, $"Field search took {clock.Elapsed.TotalSeconds:F2} s.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void A_five_hundred_megabyte_scan_finishes_within_fifteen_seconds()
    {
        const long bytes = 500L * 1024 * 1024;
        var repeated = ScanFiles.WriteRepeated(bytes);
        try
        {
            AssertFileScan(repeated, list: true);
        }
        finally
        {
            File.Delete(repeated);
        }

        var distinct = ScanFiles.WriteDistinct(bytes, out var count);
        try
        {
            AssertFileScan(distinct, list: false, distinctCount: count);
        }
        finally
        {
            File.Delete(distinct);
        }
    }

    private static void AssertFileScan(string path, bool list, int distinctCount = 0)
    {
        using var session = ViewerSession.Open(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1 << 20, FileOptions.SequentialScan));
        var slice = Stopwatch.StartNew();
        Assert.True(session.Pump(256));
        slice.Stop();
        Assert.Equal(ScanProgress.Scanning, session.Progress);
        Assert.True(slice.ElapsedMilliseconds < 80, $"One slice took {slice.ElapsedMilliseconds} ms.");

        var clock = Stopwatch.StartNew();
        session.PumpAll();
        clock.Stop();
        var elapsed = slice.Elapsed + clock.Elapsed;
        Assert.Equal(ScanProgress.Completed, session.Progress);
        Assert.Null(session.Error);
        Assert.True(elapsed.TotalSeconds < 15, $"The scan took {elapsed.TotalSeconds:F2} s.");

        if (list)
        {
            var field = session.ReadRecord(DocumentPath.Root).Fields.Single();
            var listPath = DocumentPath.Root.Field("item");
            session.Expand(listPath);
            session.Collapse(listPath);
            session.Expand(listPath);
            var before = GC.GetAllocatedBytesForCurrentThread();
            var tail = session.ReadList(listPath, field.ListCount!.Value - 1, 1);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal("hello", tail.Entries[0].Cells.Single(cell => cell.Name == "value").Text);
            Assert.True(allocated < 400_000, $"The last entry allocated {allocated} bytes.");
            return;
        }

        var recordPath = DocumentPath.Root.Field($"n{distinctCount - 1}");
        var beforeOpen = GC.GetAllocatedBytesForCurrentThread();
        var open = Stopwatch.StartNew();
        var record = session.ReadRecord(recordPath);
        open.Stop();
        var openAllocated = GC.GetAllocatedBytesForCurrentThread() - beforeOpen;
        Assert.Equal(new string('x', 25), record.Fields.Single(field => field.Name == "a").Text);
        Assert.True(open.ElapsedMilliseconds < 250, $"Opening one record took {open.ElapsedMilliseconds} ms.");
        Assert.True(openAllocated < 1_000_000, $"Opening one record allocated {openAllocated} bytes.");
    }

    [Fact]
    public void Search_through_nested_lists_stays_with_the_scan()
    {
        const int count = 50_000;
        var xml = new StringBuilder();
        xml.Append("<root>");
        for (var i = 0; i < count; i++)
            xml.Append("<item><email>user@example.com</email><skills><secondary>a</secondary><secondary>b</secondary></skills></item>");
        xml.Append("</root>");
        var session = SessionFixture.Scan(xml.ToString());

        var clock = Stopwatch.StartNew();
        session.Search(new TextSearch { FindWhat = "example" });
        session.PumpAll();
        clock.Stop();

        Assert.Equal(count, session.Hits.Count);
        Assert.Equal($"/root/item[{count}]/email", session.FormatPath(session.Hits[^1].Path));
        Assert.True(clock.ElapsedMilliseconds < 1_500, $"Search took {clock.ElapsedMilliseconds} ms.");
    }
}

[CollectionDefinition("large-scan", DisableParallelization = true)]
public sealed class LargeScanCollection;

internal static class ScanFiles
{
    public static string WriteRepeated(long bytes, string lastValue = "hello")
    {
        var path = Path.Combine(Path.GetTempPath(), "bjx-" + Guid.NewGuid().ToString("n") + ".xml");
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 1 << 20);
        var utf8 = new UTF8Encoding(false);
        var item = utf8.GetBytes("  <item id=\"1\" name=\"alpha\">\r\n    <value>hello</value>\r\n  </item>\r\n");
        var end = utf8.GetBytes($"  <item id=\"1\" name=\"alpha\">\r\n    <value>{lastValue}</value>\r\n  </item>\r\n</root>\r\n");
        stream.Write("<root>\r\n"u8);
        while (stream.Length + item.Length + end.Length < bytes)
            stream.Write(item);
        stream.Write(end);
        return path;
    }

    public static string WriteDistinct(long bytes, out int count)
    {
        var path = Path.Combine(Path.GetTempPath(), "bjx-distinct-" + Guid.NewGuid().ToString("n") + ".xml");
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 1 << 20);
        var utf8 = new UTF8Encoding(false);
        var pad = utf8.GetBytes("<a>" + new string('x', 400) + "</a>");
        stream.Write("<root>"u8);
        count = 0;
        while (stream.Length + pad.Length + 48 < bytes)
        {
            stream.Write(utf8.GetBytes($"<n{count}>"));
            stream.Write(pad);
            stream.Write(utf8.GetBytes($"</n{count}>"));
            count++;
        }

        stream.Write("</root>"u8);
        return path;
    }

    public static string WriteNote()
    {
        var path = Path.Combine(Path.GetTempPath(), "bjx-note-" + Guid.NewGuid().ToString("n") + ".xml");
        var body = new string('a', 80) + "NEEDLE" + new string('b', 40);
        File.WriteAllText(path, $"<root><note>{body}</note></root>", new UTF8Encoding(false));
        return path;
    }
}
