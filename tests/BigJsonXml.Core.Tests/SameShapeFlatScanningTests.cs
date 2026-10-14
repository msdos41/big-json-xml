using System.Text;

namespace BigJsonXml.Tests;

public class SameShapeFlatScanningTests
{
    [Fact]
    public void Repeated_list_with_many_attributes_preserves_columns_order_and_values()
    {
        var sb = new StringBuilder();
        sb.Append("<root>");
        for (var i = 0; i < 50; i++)
        {
            sb.Append($"""<item a="{i}" b="{i * 2}" c="{i * 3}" d="{i * 4}" e="{i * 5}" f="{i * 6}" g="{i * 7}" h="{i * 8}" />""");
        }
        sb.Append("</root>");

        using var session = ViewerSession.Open(new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString())));
        session.PumpAll();

        Assert.Equal(ScanProgress.Completed, session.Progress);
        Assert.Null(session.Error);

        var listPath = DocumentPath.Root.Field("item");
        session.Expand(listPath);
        var list = session.ReadList(listPath, 0, 50);

        Assert.Equal(50, list.EntryCount);
        Assert.Equal(8, list.Columns.Count);
        var expectedColumns = new[] { "@a", "@b", "@c", "@d", "@e", "@f", "@g", "@h" };
        for (var c = 0; c < 8; c++)
        {
            Assert.Equal(expectedColumns[c], list.Columns[c]);
        }

        for (var i = 0; i < 50; i++)
        {
            var entry = list.Entries[i];
            Assert.Equal(i.ToString(), entry.Cells.Single(c => c.Name == "@a").Text);
            Assert.Equal((i * 2).ToString(), entry.Cells.Single(c => c.Name == "@b").Text);
            Assert.Equal((i * 8).ToString(), entry.Cells.Single(c => c.Name == "@h").Text);
        }
    }

    [Fact]
    public void Duplicate_attribute_on_subsequent_entry_fails_or_reports_error()
    {
        var xml = """
            <root>
              <item a="1" b="2" />
              <item a="3" b="4" a="5" />
            </root>
            """;

        using var session = ViewerSession.Open(new MemoryStream(Encoding.UTF8.GetBytes(xml)));
        session.PumpAll();

        Assert.Equal(ScanProgress.Stopped, session.Progress);
        Assert.NotNull(session.Error);
        Assert.Contains("attribute", session.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Differing_attributes_between_entries_gracefully_breaks_sameshape()
    {
        var xml = """
            <root>
              <item a="1" b="2" />
              <item a="3" c="4" />
              <item a="5" b="6" />
            </root>
            """;

        using var session = ViewerSession.Open(new MemoryStream(Encoding.UTF8.GetBytes(xml)));
        session.PumpAll();

        Assert.Equal(ScanProgress.Completed, session.Progress);
        Assert.Null(session.Error);

        var listPath = DocumentPath.Root.Field("item");
        session.Expand(listPath);
        var list = session.ReadList(listPath, 0, 3);

        Assert.Equal(3, list.EntryCount);
        Assert.Contains("@a", list.Columns);
        Assert.Contains("@b", list.Columns);
        Assert.Contains("@c", list.Columns);

        Assert.Equal("1", list.Entries[0].Cells.Single(c => c.Name == "@a").Text);
        Assert.Equal("2", list.Entries[0].Cells.Single(c => c.Name == "@b").Text);
        Assert.Equal("3", list.Entries[1].Cells.Single(c => c.Name == "@a").Text);
        Assert.Equal("4", list.Entries[1].Cells.Single(c => c.Name == "@c").Text);
        Assert.Equal("5", list.Entries[2].Cells.Single(c => c.Name == "@a").Text);
        Assert.Equal("6", list.Entries[2].Cells.Single(c => c.Name == "@b").Text);
    }

    [Fact]
    public void Cross_parent_microlists_with_many_attributes_preserve_columns_and_data()
    {
        var sb = new StringBuilder();
        sb.Append("<root>");
        for (var p = 0; p < 10; p++)
        {
            sb.Append($"<group id=\"g{p}\">");
            for (var c = 0; c < 5; c++)
            {
                sb.Append($"""<item a="{p}_{c}" b="{c}" c="const" d="extra_{p}" />""");
            }
            sb.Append("</group>");
        }
        sb.Append("</root>");

        using var session = ViewerSession.Open(new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString())));
        session.PumpAll();

        Assert.Equal(ScanProgress.Completed, session.Progress);
        Assert.Null(session.Error);

        for (var p = 0; p < 10; p++)
        {
            var gPath = DocumentPath.Root.Field("group", p + 1);
            session.Expand(gPath);
            var itemPath = gPath.Field("item");
            session.Expand(itemPath);
            var list = session.ReadList(itemPath, 0, 5);
            Assert.Equal(5, list.EntryCount);
            Assert.Equal(4, list.Columns.Count);
            Assert.Equal($"{p}_0", list.Entries[0].Cells.Single(cell => cell.Name == "@a").Text);
            Assert.Equal($"{p}_4", list.Entries[4].Cells.Single(cell => cell.Name == "@a").Text);
        }
    }
}
