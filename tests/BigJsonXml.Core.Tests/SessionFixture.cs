using System.Text;

namespace BigJsonXml.Tests;

internal static class SessionFixture
{
    public static ViewerSession Scan(string xml)
    {
        var source = new MemoryByteSource();
        source.Append(Encoding.UTF8.GetBytes(xml));
        source.Complete();
        var session = ViewerSession.Open(source);
        session.PumpAll();
        return session;
    }

    public static FieldView Field(this ViewerSession session, string name) =>
        session.ReadRecord(DocumentPath.Root).Fields.Single(field => field.Name == name);
}
