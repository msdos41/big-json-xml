using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace BigJsonXml.Desktop;

public static class CellText
{
    public const double FontSize = 13;

    public static readonly Typeface Face = new("Segoe UI");

    public static int IndexAt(string text, double localX)
    {
        if (text.Length == 0 || localX <= 0)
            return 0;
        using var layout = Layout(text);
        if (localX >= layout.WidthIncludingTrailingWhitespace)
            return text.Length;
        var hit = layout.HitTestPoint(new Point(localX, Math.Max(1, layout.Height / 2)));
        var index = hit.CharacterHit.FirstCharacterIndex + hit.CharacterHit.TrailingLength;
        if (index < 0)
            return 0;
        return index > text.Length ? text.Length : index;
    }

    public static IReadOnlyList<Rect> Rects(string text, int start, int length)
    {
        if (length <= 0 || text.Length == 0 || start >= text.Length)
            return [];
        if (start < 0)
        {
            length += start;
            start = 0;
        }

        if (start + length > text.Length)
            length = text.Length - start;
        if (length <= 0)
            return [];
        using var layout = Layout(text);
        return layout.HitTestTextRange(start, length).ToArray();
    }

    private static TextLayout Layout(string text) => new(text, Face, FontSize, Brushes.Black);
}
