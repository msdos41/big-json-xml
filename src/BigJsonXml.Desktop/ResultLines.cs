using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace BigJsonXml.Desktop;

/// <summary>
/// The open document's hit group. A later document is another group.
/// </summary>
public sealed class ResultDocument : INotifyPropertyChanged
{
    private bool _expanded = true;
    private int _matchCount;
    private int _listedCount = -1;

    public ResultDocument(string path) => Path = path;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Path { get; }

    public bool IsExpanded => _expanded;

    public int MatchCount => _matchCount;

    public string Glyph => _expanded ? "\u2212" : "+";

    public string MatchLabel
    {
        get
        {
            if (_listedCount >= 0 && _matchCount > _listedCount)
            {
                return "Showing first "
                    + _listedCount.ToString(CultureInfo.InvariantCulture)
                    + " of "
                    + _matchCount.ToString(CultureInfo.InvariantCulture)
                    + " matches";
            }

            return _matchCount == 1
                ? "1 match"
                : _matchCount.ToString(CultureInfo.InvariantCulture) + " matches";
        }
    }

    internal void SetExpanded(bool expanded)
    {
        if (_expanded == expanded)
            return;
        _expanded = expanded;
        OnPropertyChanged(nameof(IsExpanded));
        OnPropertyChanged(nameof(Glyph));
    }

    internal void SetMatchCount(int count) => SetMatchCount(count, count);

    internal void SetMatchCount(int hits, int listed)
    {
        if (_matchCount == hits && _listedCount == listed)
            return;
        _matchCount = hits;
        _listedCount = listed;
        OnPropertyChanged(nameof(MatchCount));
        OnPropertyChanged(nameof(MatchLabel));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public abstract class ResultLine
{
}

public sealed class ResultDocumentLine : ResultLine
{
    public ResultDocumentLine(ResultDocument document) => Document = document;

    public ResultDocument Document { get; }
}

public sealed class ResultHitLine : ResultLine, INotifyPropertyChanged
{
    private bool _current;

    public ResultHitLine(FindResult result) => Result = result;

    public event PropertyChangedEventHandler? PropertyChanged;

    public FindResult Result { get; }

    public bool IsCurrent => _current;

    internal void SetCurrent(bool current)
    {
        if (_current == current)
            return;
        _current = current;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCurrent)));
    }
}

/// <summary>Paints a hit row white, or blue when it is the current hit.</summary>
public sealed class ResultChromeConverter : IValueConverter
{
    public static readonly ResultChromeConverter Instance = new();

    private static readonly IBrush Row = Brush.Parse("#FFFFFF");
    private static readonly IBrush Current = Brush.Parse("#316AC5");

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var current = value is true;
        if (parameter is string kind && kind == "back")
            return current ? Current : Row;
        return current ? Brushes.White : Brushes.Black;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>ColumnDefinition.Width needs a pixel GridLength, not a bare double.</summary>
public sealed class PixelWidthConverter : IValueConverter
{
    public static readonly PixelWidthConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var width = value is double number ? number : 0;
        return new GridLength(width, GridUnitType.Pixel);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
