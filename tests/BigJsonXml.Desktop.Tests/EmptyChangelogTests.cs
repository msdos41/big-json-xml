namespace BigJsonXml.Desktop.Tests;

public class EmptyChangelogTests
{
    [Fact]
    public void The_latest_version_is_the_top_section()
    {
        var changelog = string.Join("\r\n",
        [
            "# Changelog",
            "",
            "## 0.2.0",
            "",
            "### Features",
            "",
            "- newest note",
            "",
            "## [0.1.0] - 2026-01-01",
            "",
            "- older note",
            "",
        ]);

        Assert.Equal(string.Join('\n',
        [
            "## 0.2.0",
            "",
            "### Features",
            "",
            "- newest note",
        ]), EmptyChangelog.Latest(changelog));
    }

    [Fact]
    public void The_shipped_notes_are_the_top_section_of_the_changelog_file()
    {
        var notes = EmptyChangelog.Latest(ReadChangelog());
        Assert.Equal(notes, EmptyChangelog.Text);
        Assert.StartsWith("## ", notes);
    }

    static string ReadChangelog()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var path = Path.Combine(dir.FullName, "CHANGELOG.md");
            if (File.Exists(path))
                return File.ReadAllText(path);
            dir = dir.Parent;
        }

        throw new FileNotFoundException("CHANGELOG.md");
    }
}
