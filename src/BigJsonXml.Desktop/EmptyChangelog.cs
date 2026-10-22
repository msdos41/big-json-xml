namespace BigJsonXml.Desktop;

public static class EmptyChangelog
{
    public static readonly string Text = Load();

    public static string Latest(string changelog)
    {
        var lines = new List<string>();
        var found = false;
        using var reader = new StringReader(changelog);
        while (reader.ReadLine() is { } line)
        {
            if (IsVersion(line))
            {
                if (found)
                    break;
                found = true;
            }

            if (found)
                lines.Add(line);
        }

        while (lines.Count > 0 && lines[^1].Length == 0)
            lines.RemoveAt(lines.Count - 1);
        return found ? string.Join('\n', lines) : "";
    }

    static string Load()
    {
        using var stream = typeof(EmptyChangelog).Assembly.GetManifestResourceStream("BigJsonXml.Desktop.CHANGELOG.md");
        if (stream is null)
            return "";
        using var reader = new StreamReader(stream);
        return Latest(reader.ReadToEnd());
    }

    static bool IsVersion(string line) =>
        line.StartsWith("##", StringComparison.Ordinal)
        && line.Length > 2
        && char.IsWhiteSpace(line[2]);
}
