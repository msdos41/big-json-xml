namespace BigJsonXml.Desktop.Tests;

public class EmptyChangelogTests
{
    [Fact]
    public void Every_version_is_kept_and_the_title_is_not()
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

        Assert.Equal(
        [
            "## 0.2.0\n\n### Features\n\n- newest note",
            "## [0.1.0] - 2026-01-01\n\n- older note",
        ], EmptyChangelog.Sections(changelog));
    }

    [Fact]
    public void The_shipped_notes_are_every_version_in_the_changelog_file()
    {
        var notes = EmptyChangelog.Sections(ReadChangelog());
        Assert.Equal(notes, EmptyChangelog.History);
        Assert.NotEmpty(notes);
        Assert.All(notes, section => Assert.StartsWith("## ", section));
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
