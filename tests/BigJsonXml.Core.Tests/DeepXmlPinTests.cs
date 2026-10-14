using System.Diagnostics;
using System.Text;

namespace BigJsonXml.Tests;

public class DeepXmlPinTests
{
    private const string BenchmarkPath = @"C:\D\OPC\data\4d7480cde29a662758904cbe0e1afdbb-BD_.xml";

    [Fact]
    public void Benchmark_document_pins_root_fields_kinds_and_prefentry_columns()
    {
        if (!File.Exists(BenchmarkPath))
            return;

        using var stream = File.Open(BenchmarkPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var session = ViewerSession.Open(stream);
        session.PumpAll();

        Assert.Equal(ScanProgress.Completed, session.Progress);
        Assert.Null(session.Error);

        var root = session.ReadRecord(DocumentPath.Root);
        Assert.Equal(36, root.Fields.Count);

        var expectedAttributes = new[]
        {
            ("@id", "_1"),
            ("@name", "A02"),
            ("@modified", "6447"),
            ("@description", ""),
            ("@folderpath", "01在研项目"),
            ("@baseid", "UID0fba3c-17a5fdc2776-83d"),
            ("@domainref", ""),
            ("@systemlogicpreferenceset", "Default"),
            ("@appsuite", "Capital"),
            ("@proproject", "false")
        };

        for (var i = 0; i < expectedAttributes.Length; i++)
        {
            var field = root.Fields[i];
            Assert.Equal(expectedAttributes[i].Item1, field.Name);
            Assert.Equal(FieldKind.Scalar, field.Kind);
            Assert.Equal(expectedAttributes[i].Item2, field.Text);
        }

        var prefPath = DocumentPath.Root.Field("preferencemgr");
        session.Expand(prefPath);
        var pref = session.ReadRecord(prefPath);
        Assert.Equal(4, pref.Fields.Count);
        Assert.Equal("@id", pref.Fields[0].Name);
        Assert.Equal("@modified", pref.Fields[1].Name);
        Assert.Equal("@baseid", pref.Fields[2].Name);
        Assert.Equal("prefentry", pref.Fields[3].Name);
        Assert.Equal(FieldKind.List, pref.Fields[3].Kind);
        Assert.Equal(3420, pref.Fields[3].ListCount);

        var prefEntryPath = prefPath.Field("prefentry");
        session.Expand(prefEntryPath);
        var prefList = session.ReadList(prefEntryPath, 0, 3);
        Assert.Equal(["@path", "@key", "@value"], prefList.Columns);
        Assert.Equal("ArtisanFunction.Applicabl", prefList.Entries[0].Cells.Single(c => c.Name == "@path").Text);
        Assert.Equal("MandateApplicableOptions", prefList.Entries[0].Cells.Single(c => c.Name == "@key").Text);
        Assert.Equal("false", prefList.Entries[0].Cells.Single(c => c.Name == "@value").Text);

        Assert.Equal("ArtisanFunction.Checks", prefList.Entries[1].Cells.Single(c => c.Name == "@path").Text);
        Assert.Equal("ArtisanFunctionChecks", prefList.Entries[1].Cells.Single(c => c.Name == "@key").Text);
        Assert.Equal("Checks", prefList.Entries[1].Cells.Single(c => c.Name == "@value").Text);

        Assert.Equal("ArtisanFunction.Checks.Un", prefList.Entries[2].Cells.Single(c => c.Name == "@path").Text);
        Assert.Equal("FontName", prefList.Entries[2].Cells.Single(c => c.Name == "@key").Text);
        Assert.Equal("Lucida Sans", prefList.Entries[2].Cells.Single(c => c.Name == "@value").Text);
    }

    [Fact]
    public void Micro_list_entry_offsets_survive_collapse_and_open_single_entry_efficiently()
    {
        var xml = new StringBuilder();
        xml.Append("<root><branch>");
        for (var i = 0; i < 50; i++)
        {
            xml.Append($"<group id=\"g{i}\">");
            for (var j = 0; j < 10; j++)
            {
                xml.Append($"<item k=\"k_{i}_{j}\"><val>v_{i}_{j}</val></item>");
            }
            xml.Append("</group>");
        }
        xml.Append("</branch></root>");

        var session = SessionFixture.Scan(xml.ToString());
        Assert.Equal(ScanProgress.Completed, session.Progress);

        var listPath = DocumentPath.Root.Field("branch").Field("group");
        session.Expand(DocumentPath.Root.Field("branch"));
        session.Expand(listPath);

        // Collapse and re-expand
        session.Collapse(listPath);
        session.Expand(listPath);

        var groupRows = session.ReadList(listPath, 0, 50);
        Assert.Equal(50, groupRows.EntryCount);
        Assert.Equal(["@id", "item"], groupRows.Columns);

        // Check deep list under group index 25
        var innerList = listPath.WithIndex(25).Field("item");
        session.Expand(innerList);
        session.Collapse(innerList);
        session.Expand(innerList);

        var beforeAlloc = GC.GetAllocatedBytesForCurrentThread();
        var itemRows = session.ReadList(innerList, 7, 1);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - beforeAlloc;

        Assert.Single(itemRows.Entries);
        Assert.Equal(["@k", "val"], itemRows.Columns);
        Assert.Equal("k_24_7", itemRows.Entries[0].Cells.Single(c => c.Name == "@k").Text);
        Assert.Equal("v_24_7", itemRows.Entries[0].Cells.Single(c => c.Name == "val").Text);
        Assert.True(allocated < 50_000, $"Reading single entry allocated {allocated} bytes.");
    }

    [Fact]
    public void Unclosed_non_simple_child_elements_appear_in_parent_record_table_during_scan()
    {
        var source = new MemoryByteSource();
        source.Append("<root><catalog><section id=\"s1\"><deep><leaf>value"u8);
        using var session = ViewerSession.Open(source);

        // Pump what's available so far
        session.Pump(256);
        Assert.Equal(ScanProgress.Scanning, session.Progress);

        // Under ADR 0021, an unclosed element must be visible as a field in its parent
        var rootRecord = session.ReadRecord(DocumentPath.Root);
        Assert.Contains(rootRecord.Fields, f => f.Name == "catalog" && f.Kind == FieldKind.Record);

        var catalogRecord = session.ReadRecord(DocumentPath.Root.Field("catalog"));
        Assert.Contains(catalogRecord.Fields, f => f.Name == "section" && f.Kind == FieldKind.Record);

        var sectionRecord = session.ReadRecord(DocumentPath.Root.Field("catalog").Field("section"));
        Assert.Contains(sectionRecord.Fields, f => f.Name == "@id" && f.Text == "s1");
        Assert.Contains(sectionRecord.Fields, f => f.Name == "deep" && f.Kind == FieldKind.Record);

        // Now complete the document
        source.Append("</leaf></deep></section></catalog></root>"u8);
        source.Complete();
        session.PumpAll();

        Assert.Equal(ScanProgress.Completed, session.Progress);
        var finalSection = session.ReadRecord(DocumentPath.Root.Field("catalog").Field("section"));
        Assert.Contains(finalSection.Fields, f => f.Name == "deep" && f.Kind == FieldKind.Record);
    }

    [Fact]
    public void Mixed_content_empty_tags_and_attribute_only_elements_retain_outline_shape()
    {
        const string xml = """
            <root>
                <mixed id="m1">Prefix <child a="1">inside</child> Suffix</mixed>
                <emptySelfClosed id="e1"/>
                <emptyPair id="e2"></emptyPair>
                <attrOnly a="1" b="2" c="3"/>
            </root>
            """;

        var session = SessionFixture.Scan(xml);
        Assert.Equal(ScanProgress.Completed, session.Progress);

        var root = session.ReadRecord(DocumentPath.Root);
        Assert.Equal(["mixed", "emptySelfClosed", "emptyPair", "attrOnly"], root.Fields.Select(f => f.Name).ToArray());

        session.Expand(DocumentPath.Root.Field("mixed"));
        var mixed = session.ReadRecord(DocumentPath.Root.Field("mixed"));
        Assert.Equal(["@id", "#text", "child"], mixed.Fields.Select(f => f.Name).ToArray());
        Assert.Equal("m1", mixed.Fields[0].Text);
        Assert.Equal("Prefix  Suffix", mixed.Fields[1].Text);

        session.Expand(DocumentPath.Root.Field("emptySelfClosed"));
        var emptySelfClosed = session.ReadRecord(DocumentPath.Root.Field("emptySelfClosed"));
        Assert.Equal(["@id"], emptySelfClosed.Fields.Select(f => f.Name).ToArray());
        Assert.Equal("e1", emptySelfClosed.Fields[0].Text);

        session.Expand(DocumentPath.Root.Field("emptyPair"));
        var emptyPair = session.ReadRecord(DocumentPath.Root.Field("emptyPair"));
        Assert.Equal(["@id"], emptyPair.Fields.Select(f => f.Name).ToArray());
        Assert.Equal("e2", emptyPair.Fields[0].Text);

        session.Expand(DocumentPath.Root.Field("attrOnly"));
        var attrOnly = session.ReadRecord(DocumentPath.Root.Field("attrOnly"));
        Assert.Equal(["@a", "@b", "@c"], attrOnly.Fields.Select(f => f.Name).ToArray());
        Assert.Equal("1", attrOnly.Fields[0].Text);
        Assert.Equal("2", attrOnly.Fields[1].Text);
        Assert.Equal("3", attrOnly.Fields[2].Text);
    }
}
