using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BigJsonXml.Desktop;

/// <summary>
/// One row in the results window. The mark is the matched stretch inside the field text.
/// The path can change while a list is still being counted. Body text may arrive after a placeholder.
/// </summary>
public sealed class FindResult : INotifyPropertyChanged
{
    public FindResult(Hit hit, string text, int markStart, int markLength, string path, bool ready = true)
    {
        Hit = hit;
        Text = text;
        MarkStart = markStart;
        MarkLength = markLength;
        Path = path;
        IsReady = ready;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void RevisePath(string path)
    {
        if (Path == path)
            return;
        Path = path;
        OnPropertyChanged(nameof(Path));
    }

    public void ReviseBody(string text, int markStart, int markLength, string path)
    {
        var changed = !IsReady
            || Text != text
            || MarkStart != markStart
            || MarkLength != markLength
            || Path != path;
        Text = text;
        MarkStart = markStart;
        MarkLength = markLength;
        Path = path;
        IsReady = true;
        if (!changed)
            return;
        OnPropertyChanged(nameof(Text));
        OnPropertyChanged(nameof(MarkStart));
        OnPropertyChanged(nameof(MarkLength));
        OnPropertyChanged(nameof(Path));
        OnPropertyChanged(nameof(Before));
        OnPropertyChanged(nameof(Match));
        OnPropertyChanged(nameof(After));
        OnPropertyChanged(nameof(IsReady));
    }

    public Hit Hit { get; }

    public string Text { get; private set; }

    public int MarkStart { get; private set; }

    public int MarkLength { get; private set; }

    public string Path { get; private set; }

    /// <summary>False while the row is a cheap placeholder waiting for ReadHitText.</summary>
    public bool IsReady { get; private set; }

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
