namespace BigJsonXml.Desktop;

public static class EmptyChangelog
{
    public static IReadOnlyList<string> History { get; } = Sections(ReadEmbedded());

    public static IReadOnlyList<string> Sections(string changelog)
    {
        var sections = new List<string>();
        var lines = new List<string>();
        var found = false;
        using var reader = new StringReader(changelog);
        while (reader.ReadLine() is { } line)
        {
            if (IsVersion(line))
            {
                if (found)
                    sections.Add(Join(lines));
                lines.Clear();
                found = true;
            }

            if (found)
                lines.Add(line);
        }

        if (found)
            sections.Add(Join(lines));
        return sections;
    }

    static string ReadEmbedded()
    {
        using var stream = typeof(EmptyChangelog).Assembly.GetManifestResourceStream("BigJsonXml.Desktop.CHANGELOG.md");
        if (stream is null)
            return "";
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    static string Join(List<string> lines)
    {
        var end = lines.Count;
        while (end > 0 && lines[end - 1].Length == 0)
            end--;
        return string.Join('\n', lines.Take(end));
    }

    static bool IsVersion(string line) =>
        line.StartsWith("##", StringComparison.Ordinal)
        && line.Length > 2
        && char.IsWhiteSpace(line[2]);
}
