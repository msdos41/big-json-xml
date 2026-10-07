using System.Net;
using System.Net.Sockets;

namespace BigJsonXml.Tests;

public class StopOnUnsafeXmlTests
{
    [Fact]
    public void External_entity_stops_the_scan_and_keeps_earlier_fields()
    {
        var session = SessionFixture.Scan("""
            <!DOCTYPE root [
              <!ENTITY xxe SYSTEM "file:///C:/Windows/win.ini">
            ]>
            <root><a>1</a>&xxe;<b>2</b></root>
            """);

        Assert.Equal(ScanProgress.Stopped, session.Progress);
        Assert.NotNull(session.Error);
        Assert.Contains("External", session.Error.Message, StringComparison.Ordinal);
        Assert.Equal("1", session.Field("a").Text);
        Assert.DoesNotContain(session.ReadRecord(DocumentPath.Root).Fields, field => field.Name == "b");
        Assert.DoesNotContain(session.ReadRecord(DocumentPath.Root).Fields, field => (field.Text ?? "").Contains("[fonts]", StringComparison.Ordinal));
    }

    [Fact]
    public void External_dtd_is_not_fetched_and_the_document_opens()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var session = SessionFixture.Scan($"""
            <!DOCTYPE root SYSTEM "http://127.0.0.1:{port}/missing.dtd">
            <root><a>1</a></root>
            """);

        Assert.Equal(ScanProgress.Completed, session.Progress);
        Assert.Null(session.Error);
        Assert.False(listener.Pending());
        Assert.Equal("1", session.Field("a").Text);
    }

    [Fact]
    public void Public_external_dtd_is_skipped_and_the_document_opens()
    {
        var session = SessionFixture.Scan("""
            <!DOCTYPE project PUBLIC "-//Mentor Graphics Corporation//Project 2014.1//EN" "D:/CHS/dtd/project.dtd">
            <project id="_1"><name>CD701</name></project>
            """);

        Assert.Equal(ScanProgress.Completed, session.Progress);
        Assert.Null(session.Error);
        Assert.Equal("_1", session.Field("@id").Text);
        Assert.Equal("CD701", session.Field("name").Text);
    }

    [Fact]
    public void External_id_does_not_hide_an_internal_subset()
    {
        var session = SessionFixture.Scan("""
            <!DOCTYPE root SYSTEM "missing.dtd" [
              <!ENTITY e "inside">
            ]>
            <root><a>&e;</a></root>
            """);

        Assert.Equal(ScanProgress.Completed, session.Progress);
        Assert.Equal("inside", session.Field("a").Text);
    }

    [Fact]
    public void An_entity_only_the_external_subset_would_define_stops_the_scan()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var session = SessionFixture.Scan($"""
            <!DOCTYPE root SYSTEM "http://127.0.0.1:{port}/missing.dtd">
            <root><a>1</a>&missing;</root>
            """);

        Assert.Equal(ScanProgress.Stopped, session.Progress);
        Assert.NotNull(session.Error);
        Assert.Contains("not defined", session.Error.Message, StringComparison.Ordinal);
        Assert.False(listener.Pending());
        Assert.Equal("1", session.Field("a").Text);
    }

    [Fact]
    public void Well_formedness_error_keeps_the_prefix_and_invents_nothing()
    {
        var session = SessionFixture.Scan("<root><a>1</a><b>2</b><<c>3</c></root>");

        Assert.Equal(ScanProgress.Stopped, session.Progress);
        Assert.NotNull(session.Error);
        Assert.True(session.Error.ByteOffset > 0);
        var names = session.ReadRecord(DocumentPath.Root).Fields.Select(field => field.Name).ToArray();
        Assert.Equal(["a", "b"], names);
        Assert.Equal("1", session.Field("a").Text);
        Assert.Equal("2", session.Field("b").Text);
    }

    [Fact]
    public void Unclosed_document_stops_at_the_end_without_dropping_earlier_fields()
    {
        var session = SessionFixture.Scan("<root><a>1</a>");

        Assert.Equal(ScanProgress.Stopped, session.Progress);
        Assert.Equal("1", session.Field("a").Text);
        Assert.NotNull(session.Error);
    }
}
