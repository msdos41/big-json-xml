using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BigJsonXml.Desktop;

public sealed class ViewerModel : INotifyPropertyChanged, IDisposable
{
    public const int ScanSliceMilliseconds = 32;
    public const int ScanIdleMilliseconds = 16;
    private ViewerSession? _session;
    private string _findWhat = "";
    private bool _matchCase = true;
    private bool _startsWith;
    private bool _entireCell;
    private int _resultWidth = 40;
    private int _builtWidth = -1;
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
    private bool _rebuild;
    private bool _adjustingResults;
    private IReadOnlyList<SurfaceRow> _rows = [];
    private readonly ObservableCollection<FindResult> _results = [];
    private CellSelection? _selection;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool FindWindowOpen => true;

    public bool ResultsWindowOpen => true;

    public string FindWhat
    {
        get => _findWhat;
        set => Set(ref _findWhat, value);
    }

    public bool MatchCase
    {
        get => _matchCase;
        set => Set(ref _matchCase, value);
    }

    public bool StartsWith
    {
        get => _startsWith;
        set => Set(ref _startsWith, value);
    }

    public bool EntireCell
    {
        get => _entireCell;
        set => Set(ref _entireCell, value);
    }

    public int ResultWidth
    {
        get => _resultWidth;
        set
        {
            if (value < 1)
                value = 1;
            if (_resultWidth == value)
                return;
            _resultWidth = value;
            OnPropertyChanged();
            if (_session is not null && !_refreshing)
                Refresh();
        }
    }

    public int ResultCount => _results.Count;

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

    public IReadOnlyList<FindResult> Results => _results;

    public CellSelection? Selection
    {
        get => _selection;
        private set => Set(ref _selection, value);
    }

    public void OpenPath(string path)
    {
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1 << 20, FileOptions.SequentialScan);
        Open(new StreamByteSource(stream));
    }

    public void Open(IByteSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _session?.Dispose();
        _session = ViewerSession.Open(source);
        _scrollX = 0;
        _scrollY = 0;
        Selection = null;
        _builtWidth = -1;
        ReplaceResults();
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
            if (!_session.Pump(512))
                break;
            worked = true;
        }
        while (Environment.TickCount64 - start < ScanSliceMilliseconds);

        if (worked)
            Refresh();
    }

    public void SetViewportSize(double width, double height)
    {
        if (width <= 1 || height <= 1)
            return;
        if (Math.Abs(_viewportWidth - width) < 0.5 && Math.Abs(_viewportHeight - height) < 0.5)
            return;
        _viewportWidth = width;
        _viewportHeight = height;
        if (_refreshing)
        {
            _rebuild = true;
            return;
        }

        _session?.SetViewport(_scrollX, _scrollY, _viewportWidth, _viewportHeight);
        Refresh();
    }

    public void FindAll()
    {
        if (_session is null)
            return;
        _session.Search(new TextSearch
        {
            FindWhat = _findWhat,
            MatchCase = _matchCase,
            StartsWith = _startsWith,
            EntireCell = _entireCell,
        });
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

    public void ChooseResult(FindResult? result)
    {
        if (_adjustingResults || _session is null)
            return;
        if (result is null)
            ClearMark();
        else
            Reveal(result.Hit);
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

    public void SetSelection(CellSelection? selection) => Selection = selection;

    public string? SelectedText()
    {
        if (_selection is not { Length: > 0 } selection || _session is null)
            return null;
        if (selection.Scalar)
            return _session.ReadFullValueSlice(selection.Path, selection.Start, selection.Length);
        if (selection.Label is null || selection.Start > selection.Label.Length)
            return null;
        var end = Math.Min(selection.End, selection.Label.Length);
        if (end <= selection.Start)
            return null;
        return selection.Label[selection.Start..end];
    }

    public int ScalarLength(DocumentPath path) => _session?.ScalarLength(path) ?? 0;

    public void SelectAll()
    {
        if (_selection is null || _session is null)
            return;
        if (_selection.Scalar)
            SetSelection(_selection with { Start = 0, End = _session.ScalarLength(_selection.Path) });
        else if (_selection.Label is not null)
            SetSelection(_selection with { Start = 0, End = _selection.Label.Length });
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
        if (_session is null)
            return;
        if (_refreshing)
        {
            _rebuild = true;
            return;
        }

        _session.SetViewport(_scrollX, _scrollY, _viewportWidth, _viewportHeight);
        Refresh();
    }

    private void Refresh()
    {
        if (_session is null || _refreshing)
            return;
        _refreshing = true;
        try
        {
            for (var pass = 0; pass < 4; pass++)
            {
                _rebuild = false;
                _scrollX = Math.Max(0, _scrollX);
                _scrollY = Math.Max(0, _scrollY);
                var frame = ReadFrame();
                var maxX = Math.Max(0, frame.ContentWidth - _viewportWidth);
                var maxY = Math.Max(0, frame.ContentHeight - _viewportHeight);
                var x = Math.Min(_scrollX, maxX);
                var y = Math.Min(_scrollY, maxY);
                var clamped = Math.Abs(x - _scrollX) >= 0.5 || Math.Abs(y - _scrollY) >= 0.5;
                if (clamped)
                {
                    _scrollX = x;
                    _scrollY = y;
                    frame = ReadFrame();
                    maxX = Math.Max(0, frame.ContentWidth - _viewportWidth);
                    maxY = Math.Max(0, frame.ContentHeight - _viewportHeight);
                }

                Rows = frame.Rows;
                SyncResults();
                ContentWidth = frame.ContentWidth;
                ContentHeight = frame.ContentHeight;
                if (clamped)
                {
                    OnPropertyChanged(nameof(ScrollX));
                    OnPropertyChanged(nameof(ScrollY));
                }

                ScrollMaxX = maxX;
                ScrollMaxY = maxY;
                Error = _session.Error is null
                    ? ""
                    : $"{_session.Error.Message} (line {_session.Error.Line}, column {_session.Error.Column})";
                Status = _session.Progress switch
                {
                    ScanProgress.Scanning => "Scanning",
                    ScanProgress.Completed => "Complete",
                    _ => "Stopped",
                };
                if (!_rebuild)
                    break;
            }
        }
        finally
        {
            _refreshing = false;
        }
    }

    private SurfaceFrame ReadFrame()
    {
        _session!.SetViewport(_scrollX, _scrollY, _viewportWidth, _viewportHeight);
        return _session.ReadSurface();
    }

    private void SyncResults()
    {
        var next = _session!.Hits;
        if (_resultWidth == _builtWidth && IsPrefix(next))
        {
            for (var i = 0; i < _results.Count; i++)
                _results[i].RevisePath(_session.FormatPath(next[i].Path));
            var grown = false;
            for (var i = _results.Count; i < next.Count; i++)
            {
                _results.Add(Describe(next[i]));
                grown = true;
            }

            if (grown)
                OnPropertyChanged(nameof(ResultCount));
            return;
        }

        ReplaceResults(next);
    }

    private void ReplaceResults(IReadOnlyList<Hit>? next = null)
    {
        next ??= _session?.Hits ?? [];
        // Replacing the list raises selection changes that are not a new choice.
        _adjustingResults = true;
        try
        {
            _results.Clear();
            if (_session is not null)
            {
                foreach (var hit in next)
                    _results.Add(Describe(hit));
            }

            _builtWidth = _resultWidth;
        }
        finally
        {
            _adjustingResults = false;
        }

        OnPropertyChanged(nameof(ResultCount));
    }

    private FindResult Describe(Hit hit)
    {
        var text = _session!.ReadHitText(hit, _resultWidth);
        return new FindResult(hit, text.Text, text.MarkStart, text.MarkLength, _session.FormatPath(hit.Path));
    }

    private bool IsPrefix(IReadOnlyList<Hit> next)
    {
        if (_results.Count > next.Count)
            return false;
        for (var i = 0; i < _results.Count; i++)
        {
            if (!ReferenceEquals(_results[i].Hit, next[i]))
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
