using System.Text;

namespace BigJsonXml.Tests;

public class ShapePredictionTests
{
    private const string BenchmarkPath = @"C:\D\OPC\data\4d7480cde29a662758904cbe0e1afdbb-BD_.xml";

    [Fact]
    public void Cross_parent_shape_prediction_preserves_microlists_and_columns()
    {
        var xml = """
            <root>
              <group id="g1">
                <item a="1" b="10" />
                <item a="2" b="20" />
              </group>
              <group id="g2">
                <item a="3" b="30" />
                <item a="4" b="40" />
              </group>
            </root>
            """;

        using var session = ViewerSession.Open(new MemoryStream(Encoding.UTF8.GetBytes(xml)));
        session.PumpAll();

        Assert.Equal(ScanProgress.Completed, session.Progress);
        Assert.Null(session.Error);

        var root = session.ReadRecord(DocumentPath.Root);
        var groupField = Assert.Single(root.Fields);
        Assert.Equal("group", groupField.Name);
        Assert.Equal(FieldKind.List, groupField.Kind);
        Assert.Equal(2, groupField.ListCount);

        var g1Path = DocumentPath.Root.Field("group", 1);
        session.Expand(g1Path);
        var g1 = session.ReadRecord(g1Path);
        var item1 = Assert.Single(g1.Fields, f => f.Name == "item");
        Assert.Equal(FieldKind.List, item1.Kind);
        Assert.Equal(2, item1.ListCount);

        var g2Path = DocumentPath.Root.Field("group", 2);
        session.Expand(g2Path);
        var g2 = session.ReadRecord(g2Path);
        var item2 = Assert.Single(g2.Fields, f => f.Name == "item");
        Assert.Equal(FieldKind.List, item2.Kind);
        Assert.Equal(2, item2.ListCount);

        var g2ItemPath = g2Path.Field("item");
        session.Expand(g2ItemPath);
        var i2List = session.ReadList(g2ItemPath, 0, 2);
        Assert.Equal("3", i2List.Entries[0].Cells.Single(c => c.Name == "@a").Text);
        Assert.Equal("30", i2List.Entries[0].Cells.Single(c => c.Name == "@b").Text);
        Assert.Equal("4", i2List.Entries[1].Cells.Single(c => c.Name == "@a").Text);
        Assert.Equal("40", i2List.Entries[1].Cells.Single(c => c.Name == "@b").Text);
    }

    [Fact]
    public void Single_item_under_subsequent_parent_remains_record()
    {
        var xml = """
            <root>
              <wrapper1>
                <group id="g1">
                  <item a="1" b="10" />
                  <item a="2" b="20" />
                </group>
              </wrapper1>
              <wrapper2>
                <group id="g2">
                  <item a="3" b="30" />
                </group>
              </wrapper2>
            </root>
            """;

        using var session = ViewerSession.Open(new MemoryStream(Encoding.UTF8.GetBytes(xml)));
        session.PumpAll();

        Assert.Equal(ScanProgress.Completed, session.Progress);
        Assert.Null(session.Error);

        var g2Path = DocumentPath.Root.Field("wrapper2").Field("group");
        session.Expand(g2Path);
        var g2 = session.ReadRecord(g2Path);
        var item2 = Assert.Single(g2.Fields, f => f.Name == "item");
        Assert.Equal(FieldKind.Record, item2.Kind);

        var i2Path = g2Path.Field("item");
        session.Expand(i2Path);
        var i2 = session.ReadRecord(i2Path);
        Assert.Equal("3", Assert.Single(i2.Fields, f => f.Name == "@a").Text);
        Assert.Equal("30", Assert.Single(i2.Fields, f => f.Name == "@b").Text);
    }

    [Fact]
    public void Speculation_failure_falls_back_to_safe_reading_and_blacklists()
    {
        var xml = """
            <root>
              <group id="g1">
                <item a="1" />
                <item a="2" />
              </group>
              <group id="g2">
                <item a="3"><!-- comment within child -->test</item>
                <item a="4" />
              </group>
              <group id="g3">
                <item a="5" />
                <item a="6" />
              </group>
            </root>
            """;

        using var session = ViewerSession.Open(new MemoryStream(Encoding.UTF8.GetBytes(xml)));
        session.PumpAll();

        Assert.Equal(ScanProgress.Completed, session.Progress);
        Assert.Null(session.Error);

        var g2Path = DocumentPath.Root.Field("group", 2);
        session.Expand(g2Path);
        var g2 = session.ReadRecord(g2Path);
        var item2 = Assert.Single(g2.Fields, f => f.Name == "item");
        Assert.Equal(FieldKind.List, item2.Kind);
        Assert.Equal(2, item2.ListCount);

        var g3Path = DocumentPath.Root.Field("group", 3);
        session.Expand(g3Path);
        var g3 = session.ReadRecord(g3Path);
        var item3 = Assert.Single(g3.Fields, f => f.Name == "item");
        Assert.Equal(FieldKind.List, item3.Kind);
        Assert.Equal(2, item3.ListCount);
    }

    [Fact]
    public void Shape_cache_is_bounded_to_512_entries()
    {
        var sb = new StringBuilder();
        sb.Append("<root>");
        for (var i = 0; i < 600; i++)
        {
            sb.Append($"<p{i}><c{i} a=\"1\"/><c{i} a=\"2\"/></p{i}>");
        }
        sb.Append("</root>");

        using var session = ViewerSession.Open(new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString())));
        session.PumpAll();

        Assert.Equal(ScanProgress.Completed, session.Progress);
        Assert.Null(session.Error);
        Assert.InRange(session.XmlShapeCacheCount, 1, 512);
    }

    [Fact]
    public void Benchmark_document_reduces_start_events_by_more_than_200k()
    {
        if (!File.Exists(BenchmarkPath))
            return;

        using var stream = File.Open(BenchmarkPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var session = ViewerSession.Open(stream);
        session.PumpAll();

        Assert.Equal(ScanProgress.Completed, session.Progress);
        // Baseline full Start events was 436,163.
        // Halving micro-lists drops full Start events by > 200,000 to below 236,000.
        Assert.True(session.XmlStartCount < 236_000,
            $"Expected full Start events < 236,000, but was {session.XmlStartCount}");
    }
}
