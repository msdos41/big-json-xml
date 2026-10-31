using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;

namespace BigJsonXml.Desktop;

public sealed class DocumentTab : INotifyPropertyChanged, IDisposable
{
    public const double CircleSize = 10;
    public const double CloseSize = 18;
    public const double Pad = 8;
    public const double Gap = 6;
    public const double ArrowButtonWidth = 28;
    public const double ArrowSpan = ArrowButtonWidth * 2;

    private static IBrush? _scanning;
    private static IBrush? _complete;
    private static IBrush? _stopped;
    private static IBrush? _inactive;
    private static IBrush? _edge;

    private readonly ViewerSession _session;
    private bool _disposed;
    private bool _active;
    private bool _fullyVisible = true;
    private double _width;
    private double _left;
    private ScanProgress _progress = ScanProgress.Scanning;

    internal DocumentTab(ViewerSession session, string path, string label)
    {
        _session = session;
        Path = path;
        Label = label;
        _width = EstimateWidth(label);
        Results = new FindResultList(this);
        Visible = new VirtualResultLines(this);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    internal ViewerSession Session => _session;

    public string Path { get; }

    public string Label { get; }

    public bool IsActive => _active;

    public bool FullyVisible => _fullyVisible;

    public double Width => _width;

    public double Left => _left;

    public ScanProgress Progress => _progress;

    public static IBrush ScanningBrush => _scanning ??= new SolidColorBrush(Color.Parse("#E6B800"));

    public static IBrush CompleteBrush => _complete ??= new SolidColorBrush(Color.Parse("#2E8B57"));

    public static IBrush StoppedBrush => _stopped ??= new SolidColorBrush(Color.Parse("#C42B2B"));

    public static IBrush InactiveBackground => _inactive ??= new SolidColorBrush(Color.Parse("#F3F4F6"));

    public static IBrush ActiveEdgeBrush => _edge ??= new SolidColorBrush(Color.Parse("#1A56C4"));

    public IBrush CircleBrush => _progress switch
    {
        ScanProgress.Scanning => ScanningBrush,
        ScanProgress.Completed => CompleteBrush,
        _ => StoppedBrush,
    };

    public IBrush Background => _active ? Brushes.White : InactiveBackground;

    public IBrush TopEdge => _active ? ActiveEdgeBrush : Brushes.Transparent;

    internal readonly FindResultList Results;

    internal readonly ObservableCollection<ResultDocument> Documents = [];

    internal readonly VirtualResultLines Visible;

    internal FindResult? Current;

    internal int BuiltWidth = -1;

    internal int CurrentHitIndex = -1;

    internal bool Searched;

    internal double ScrollX;

    internal double ScrollY;

    internal CellSelection? Selection;

    internal long ScanStartedAt;

    internal long? ScanElapsed;

    public static double EstimateWidth(string label)
    {
        return Pad + (label.Length * 8d) + Gap + CircleSize + Gap + CloseSize + Pad;
    }

    internal void NoteProgress()
    {
        var progress = _session.Progress;
        if (progress == _progress)
            return;
        _progress = progress;
        OnPropertyChanged(nameof(Progress));
        OnPropertyChanged(nameof(CircleBrush));
    }

    internal void SetActive(bool value)
    {
        if (_active == value)
            return;
        _active = value;
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(Background));
        OnPropertyChanged(nameof(TopEdge));
    }

    internal void SetFullyVisible(bool value)
    {
        if (_fullyVisible == value)
            return;
        _fullyVisible = value;
        OnPropertyChanged(nameof(FullyVisible));
    }

    internal void SetWidth(double value)
    {
        if (Math.Abs(_width - value) < 0.5)
            return;
        _width = value;
        OnPropertyChanged(nameof(Width));
    }

    internal void SetLeft(double value)
    {
        if (Math.Abs(_left - value) < 0.5)
            return;
        _left = value;
        OnPropertyChanged(nameof(Left));
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _session.Dispose();
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
