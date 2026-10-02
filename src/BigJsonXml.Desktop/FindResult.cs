using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BigJsonXml.Desktop;

/// <summary>
/// One row in the results window. The mark is the matched stretch inside the field text.
/// The path can change while a list is still being counted.
/// </summary>
public sealed class FindResult : INotifyPropertyChanged
{
    public FindResult(Hit hit, string text, int markStart, int markLength, string path)
    {
        Hit = hit;
        Text = text;
        MarkStart = markStart;
        MarkLength = markLength;
        Path = path;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void RevisePath(string path)
    {
        if (Path == path)
            return;
        Path = path;
        OnPropertyChanged(nameof(Path));
    }

    public Hit Hit { get; }

    public string Text { get; }

    public int MarkStart { get; }

    public int MarkLength { get; }

    public string Path { get; private set; }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public string Before => MarkStart <= 0 ? "" : Text[..Math.Min(MarkStart, Text.Length)];

    public string Match
    {
        get
        {
            var start = Math.Clamp(MarkStart, 0, Text.Length);
            var length = Math.Clamp(MarkLength, 0, Text.Length - start);
            return Text.Substring(start, length);
        }
    }

    public string After
    {
        get
        {
            var start = Math.Clamp(MarkStart, 0, Text.Length);
            var length = Math.Clamp(MarkLength, 0, Text.Length - start);
            return Text[(start + length)..];
        }
    }
}
