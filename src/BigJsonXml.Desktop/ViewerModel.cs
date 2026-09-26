using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BigJsonXml.Desktop;

public sealed class ViewerModel : INotifyPropertyChanged, IDisposable
{
    private ViewerSession? _session;
    private string _query = "";
    private string _status = "No document";
    private string _error = "";
    private double _scrollX;
    private double _scrollY;
    private double _scrollMaxX;
    private double _scrollMaxY;
    private double _contentWidth = 800;
    private double _contentHeight = 600;
    private double _viewportWidth = 800;
    private double _viewportHeight = 600;
    private bool _refreshing;
    private bool _adjustingHits;
    private IReadOnlyList<SurfaceRow> _rows = [];
    private readonly ObservableCollection<Hit> _hits = [];

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Query
    {
        get => _query;
        set => Set(ref _query, value);
    }

    public string Status
    {
        get => _status;
        private set => Set(ref _status, value);
    }

    public string Error
    {
        get => _error;
        private set => Set(ref _error, value);
    }

    public double ScrollX
    {
        get => _scrollX;
        set => Move(ref _scrollX, value, nameof(ScrollX));
    }

    public double ScrollY
    {
        get => _scrollY;
        set => Move(ref _scrollY, value, nameof(ScrollY));
    }

    public double ScrollMaxX
    {
        get => _scrollMaxX;
        private set => Set(ref _scrollMaxX, value);
    }

    public double ScrollMaxY
    {
        get => _scrollMaxY;
        private set => Set(ref _scrollMaxY, value);
    }

    public double ContentWidth
    {
        get => _contentWidth;
        private set => Set(ref _contentWidth, value);
    }

    public double ContentHeight
    {
        get => _contentHeight;
        private set => Set(ref _contentHeight, value);
    }

    public double ViewportWidth
    {
        get => _viewportWidth;
        private set => Set(ref _viewportWidth, value);
    }

    public double ViewportHeight
    {
        get => _viewportHeight;
        private set => Set(ref _viewportHeight, value);
    }

    public IReadOnlyList<SurfaceRow> Rows
    {
        get => _rows;
        private set => Set(ref _rows, value);
    }

    public IReadOnlyList<Hit> Hits => _hits;

    public void OpenPath(string path)
    {
        _session?.Dispose();
        _session = ViewerSession.Open(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
        _scrollX = 0;
        _scrollY = 0;
        Tick();
    }

    public void Tick()
    {
        if (_session is null)
            return;
        var start = Environment.TickCount64;
        var worked = false;
        do
        {
            if (!_session.Pump(2048))
                break;
            worked = true;
        }
        while (Environment.TickCount64 - start < 16);

        if (worked)
            Refresh();
    }

    public void SetViewportSize(double width, double height)
    {
        if (width <= 1 || height <= 1)
            return;
        _viewportWidth = width;
        _viewportHeight = height;
        _session?.SetViewport(_scrollX, _scrollY, _viewportWidth, _viewportHeight);
        Refresh();
    }

    public void Search()
    {
        _session?.Search(_query);
        Tick();
    }

    public void Reveal(Hit hit)
    {
        if (_session is null)
            return;
        _session.Reveal(hit);
        _scrollX = _session.ViewportX;
        _scrollY = _session.ViewportY;
        Refresh();
        OnPropertyChanged(nameof(ScrollX));
        OnPropertyChanged(nameof(ScrollY));
    }

    public void ChooseHit(Hit? hit)
    {
        if (_adjustingHits || _session is null)
            return;
        if (hit is null)
            ClearMark();
        else
            Reveal(hit);
    }

    public void ClearMark()
    {
        if (_session is null)
            return;
        _session.ClearMark();
        Refresh();
    }

    public void Toggle(SurfaceRow row)
    {
        if (_session is null)
            return;
        if (row.CanExpand)
        {
            if (row.IsExpanded)
                _session.Collapse(row.Path);
            else
                _session.Expand(row.Path);
        }
        else if (row.CanGrowValue || row.Kind == SurfaceKind.ValueLine)
        {
            if (_session.ReadScalar(row.Path).IsFullValueExpanded)
                _session.CollapseFullValue(row.Path);
            else
                _session.ExpandFullValue(row.Path);
        }

        Refresh();
    }

    public void DragWidth(SurfaceRow row, double width)
    {
        if (_session is null || row.WidthTable is null)
            return;
        switch (row.Axis)
        {
            case WidthAxis.RecordName:
                _session.SetRecordNameWidth(row.WidthTable, width);
                break;
            case WidthAxis.RecordValue:
                _session.SetRecordValueWidth(row.WidthTable, width);
                break;
            case WidthAxis.ListColumn when row.WidthColumn is not null:
                _session.SetColumnWidth(row.WidthTable, row.WidthColumn, width);
                break;
            default:
                return;
        }

        Refresh();
    }

    public void Dispose() => _session?.Dispose();

    private void Move(ref double field, double value, string name)
    {
        value = Math.Max(0, value);
        if (Math.Abs(field - value) < 0.5)
            return;
        field = value;
        OnPropertyChanged(name);
        if (_refreshing || _session is null)
            return;
        _session.SetViewport(_scrollX, _scrollY, _viewportWidth, _viewportHeight);
        Refresh();
    }

    private void Refresh()
    {
        if (_session is null || _refreshing)
            return;
        _refreshing = true;
        _session.SetViewport(_scrollX, _scrollY, _viewportWidth, _viewportHeight);
        var frame = _session.ReadSurface();
        Rows = frame.Rows;
        SyncHits();
        ContentWidth = frame.ContentWidth;
        ContentHeight = frame.ContentHeight;
        ScrollMaxX = Math.Max(0, frame.ContentWidth - _viewportWidth);
        ScrollMaxY = Math.Max(0, frame.ContentHeight - _viewportHeight);
        Error = _session.Error is null
            ? ""
            : $"{_session.Error.Message} (line {_session.Error.Line}, column {_session.Error.Column})";
        Status = _session.Progress switch
        {
            ScanProgress.Scanning => "Scanning",
            ScanProgress.Completed => "Complete",
            _ => "Stopped",
        };
        _refreshing = false;
    }

    private void SyncHits()
    {
        var next = _session!.Hits;
        if (IsPrefix(next))
        {
            for (var i = _hits.Count; i < next.Count; i++)
                _hits.Add(next[i]);
            return;
        }

        // Replacing the list raises selection changes that are not a new choice.
        _adjustingHits = true;
        try
        {
            _hits.Clear();
            foreach (var hit in next)
                _hits.Add(hit);
        }
        finally
        {
            _adjustingHits = false;
        }
    }

    private bool IsPrefix(IReadOnlyList<Hit> next)
    {
        if (_hits.Count > next.Count)
            return false;
        for (var i = 0; i < _hits.Count; i++)
        {
            if (!ReferenceEquals(_hits[i], next[i]))
                return false;
        }

        return true;
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        OnPropertyChanged(name);
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
