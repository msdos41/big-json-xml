using Avalonia;
using System.Text;

namespace BigJsonXml.Desktop;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args is ["--check", var path])
            return SessionCheck.Run(path);
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>().UsePlatformDetect();
}

public static class SessionCheck
{
    public static int Run(string path)
    {
        using var session = ViewerSession.Open(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
        session.PumpAll();
        var record = session.ReadRecord(DocumentPath.Root);
        foreach (var field in record.Fields)
        {
            if (field.Kind != FieldKind.List)
                continue;
            var list = DocumentPath.Root.Field(field.Name);
            session.Expand(list);
            _ = session.ReadList(list, 0, Math.Min(1, field.ListCount ?? 0));
            break;
        }

        session.Search(new TextSearch { FindWhat = "a" });
        session.PumpAll();
        if (session.Hits.Count > 0)
            session.Reveal(session.Hits[0]);
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine($"{session.Progress} fields={record.Fields.Count} hits={session.Hits.Count}");
        return 0;
    }
}
