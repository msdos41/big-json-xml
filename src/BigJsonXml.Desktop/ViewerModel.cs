using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace BigJsonXml.Desktop;

public sealed class ViewerModel : INotifyPropertyChanged, IDisposable
{
    public const int ScanSliceMilliseconds = 32;
    public const int ScanIdleMilliseconds = 16;
    public const double FoundColumnMin = 64;
    public const double FoundColumnMax = 4000;
    public const double FoundColumnPadding = 8;
    private ViewerSession? _session;
    private string _findWhat = "";
    private string _fieldName = "";
    private string _parentElementName = "";
    private string _grandfatherElementName = "";
    private string _parentAttributeName = "";
    private bool _matchCase = true;
    private bool _elementName = true;
    private bool _attributeName = true;
    private bool _value = true;
    private bool _startsWith;
    private bool _entireCell;
    private int _resultWidth = 40;
    private int _builtWidth = -1;
    private string _status = "No document";
    private string _error = "";
    private bool _scanFractionShown;
    private double _scanFraction;
    private string _remainingTime = "";
    private long? _scanElapsed;
    private long _scanStartedAt;
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
    private FindResult? _current;
    private double _foundColumnWidth = 40 * ViewerSession.Advance + FoundColumnPadding;
    private IReadOnlyList<SurfaceRow> _rows = [];
    private readonly ObservableCollection<FindResult> _results = [];
    private readonly ObservableCollection<ResultDocument> _documents = [];
    private readonly ObservableCollection<ResultLine> _visible = [];
    private readonly ObservableCollection<string> _findHistory = [];
    private CellSelection? _selection;
    private bool _findWindowOpen;
    private bool _resultsWindowOpen;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool FindWindowOpen => _findWindowOpen;

    public bool ResultsWindowOpen => _resultsWindowOpen;

    public void ShowFind()
    {
        if (_findWindowOpen)
            return;
        _findWindowOpen = true;
        OnPropertyChanged(nameof(FindWindowOpen));
    }

    public void CloseFind()
    {
        var findWasOpen = _findWindowOpen;
        var resultsWereOpen = _resultsWindowOpen;
        _findWindowOpen = false;
        _resultsWindowOpen = false;
        if (findWasOpen)
            OnPropertyChanged(nameof(FindWindowOpen));
        if (resultsWereOpen)
            OnPropertyChanged(nameof(ResultsWindowOpen));
    }

    public void CloseResults()
    {
        if (!_resultsWindowOpen)
            return;
        _resultsWindowOpen = false;
        OnPropertyChanged(nameof(ResultsWindowOpen));
    }

    public string FindWhat
    {
        get => _findWhat;
        set => Set(ref _findWhat, value);
    }

    public IReadOnlyList<string> FindHistory => _findHistory;

    public void UseFindHistory(string sequence) => FindWhat = sequence;

    public string FieldName
    {
        get => _fieldName;
        set => Set(ref _fieldName, value);
    }

    public string ParentElementName
    {
        get => _parentElementName;
        set => Set(ref _parentElementName, value);
    }

    public string GrandfatherElementName
    {
        get => _grandfatherElementName;
        set => Set(ref _grandfatherElementName, value);
    }

    public string ParentAttributeName
    {
        get => _parentAttributeName;
        set => Set(ref _parentAttributeName, value);
    }

    public bool MatchCase
    {
        get => _matchCase;
        set => Set(ref _matchCase, value);
    }

    public bool ElementName
    {
        get => _elementName;
        set => Set(ref _elementName, value);
    }

    public bool AttributeName
    {
        get => _attributeName;
        set => Set(ref _attributeName, value);
    }

    public bool Value
    {
        get => _value;
        set => Set(ref _value, value);
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

    public bool IsAdjustingResults => _adjustingResults;

    public double FoundColumnWidth => _foundColumnWidth;

    public void SetFoundColumnWidth(double width)
    {
        width = Math.Clamp(width, FoundColumnMin, FoundColumnMax);
        if (Math.Abs(_foundColumnWidth - width) < 0.5)
            return;
        _foundColumnWidth = width;
        OnPropertyChanged(nameof(FoundColumnWidth));
    }

    public void ApplyFoundColumnWidth()
    {
        var characters = Math.Max(1, (int)((_foundColumnWidth - FoundColumnPadding) / ViewerSession.Advance));
        ResultWidth = characters;
    }

    public FindResult? CurrentResult
    {
        get => _current;
        private set
        {
            Set(ref _current, value);
            MarkCurrent();
        }
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

    public bool ScanFractionShown
    {
        get => _scanFractionShown;
        private set => Set(ref _scanFractionShown, value);
    }

    public double ScanFraction
    {
        get => _scanFraction;
        private set => Set(ref _scanFraction, value);
    }

    public string RemainingTime
    {
        get => _remainingTime;
        private set => Set(ref _remainingTime, value);
    }

    public void SetScanElapsed(long milliseconds)
    {
        _scanElapsed = milliseconds;
        if (_session is null)
            return;
        if (_refreshing)
        {
            RemainingTime = ScanRemainingTime();
            return;
        }

        Refresh();
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

    public ObservableCollection<ResultDocument> ResultDocuments => _documents;

    public ObservableCollection<ResultLine> VisibleResults => _visible;

    public CellSelection? Selection
    {
        get => _selection;
        private set => Set(ref _selection, value);
    }

    public void OpenPath(string path)
    {
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1 << 20, FileOptions.SequentialScan);
        Open(new StreamByteSource(stream), path);
    }

    public void Open(IByteSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Open(source, "");
    }

    private void Open(IByteSource source, string documentPath)
    {
        _session?.Dispose();
        _session = ViewerSession.Open(source);
        _findHistory.Clear();
        _scrollX = 0;
        _scrollY = 0;
        Selection = null;
        _builtWidth = -1;
        _documents.Clear();
        _documents.Add(new ResultDocument(documentPath));
        ReplaceResults();
        _scanStartedAt = Environment.TickCount64;
        _scanElapsed = null;
        Refresh();
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
        RememberFindWhat();
        if (_documents.Count > 0)
            _documents[0].SetExpanded(true);
        _session.Search(new TextSearch
        {
            FindWhat = _findWhat,
            MatchCase = _matchCase,
            FieldName = _fieldName,
            ParentElementName = _parentElementName,
            GrandfatherElementName = _grandfatherElementName,
            ParentAttributeName = _parentAttributeName,
            ElementName = _elementName,
            AttributeName = _attributeName,
            Value = _value,
            StartsWith = _startsWith,
            EntireCell = _entireCell,
        });
        CurrentResult = null;
        Tick();
        OpenResults();
    }

    public void FindNext()
    {
        if (_session is null)
            return;
        var index = CurrentIndex();
        while (_session.Hits.Count <= index + 1 && _session.Pump(512))
        {
        }

        Refresh();
        if (index + 1 >= _results.Count)
            return;
        var result = _results[index + 1];
        ExpandResults();
        _adjustingResults = true;
        try
        {
            CurrentResult = result;
        }
        finally
        {
            _adjustingResults = false;
        }

        Reveal(result.Hit);
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
        if (result is not null)
            ExpandResults();
        CurrentResult = result;
        if (result is null)
            ClearMark();
        else
            Reveal(result.Hit);
    }

    public void ToggleResultDocument(ResultDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        document.SetExpanded(!document.IsExpanded);
        _adjustingResults = true;
        try
        {
            SyncVisibleHits();
        }
        finally
        {
            _adjustingResults = false;
        }
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
                PublishScan();
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
            SyncVisibleHits();
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
            var currentHit = _current?.Hit;
            _results.Clear();
            if (_session is not null)
            {
                foreach (var hit in next)
                    _results.Add(Describe(hit));
            }

            _builtWidth = _resultWidth;
            SyncVisibleHits();
            if (currentHit is not null)
            {
                FindResult? again = null;
                for (var i = 0; i < _results.Count; i++)
                {
                    if (!ReferenceEquals(_results[i].Hit, currentHit))
                        continue;
                    again = _results[i];
                    break;
                }

                if (again is not null)
                    CurrentResult = again;
            }
        }
        finally
        {
            _adjustingResults = false;
        }

        OnPropertyChanged(nameof(ResultCount));
    }

    private void OpenResults()
    {
        _resultsWindowOpen = true;
        OnPropertyChanged(nameof(ResultsWindowOpen));
    }

    private void ExpandResults()
    {
        if (_documents.Count == 0 || _documents[0].IsExpanded)
            return;
        _documents[0].SetExpanded(true);
        _adjustingResults = true;
        try
        {
            SyncVisibleHits();
        }
        finally
        {
            _adjustingResults = false;
        }
    }

    private void SyncVisibleHits()
    {
        if (_documents.Count == 0)
        {
            if (_visible.Count > 0)
                _visible.Clear();
            return;
        }

        var document = _documents[0];
        document.SetMatchCount(_results.Count);
        if (_visible.Count == 0 || _visible[0] is not ResultDocumentLine group || !ReferenceEquals(group.Document, document))
        {
            _visible.Clear();
            _visible.Add(new ResultDocumentLine(document));
        }

        if (!document.IsExpanded)
        {
            while (_visible.Count > 1)
                _visible.RemoveAt(_visible.Count - 1);
            MarkCurrent();
            return;
        }

        var shown = _visible.Count - 1;
        var prefix = shown <= _results.Count;
        for (var i = 0; prefix && i < shown; i++)
        {
            if (_visible[i + 1] is not ResultHitLine hit || !ReferenceEquals(hit.Result, _results[i]))
                prefix = false;
        }

        if (!prefix)
        {
            while (_visible.Count > 1)
                _visible.RemoveAt(_visible.Count - 1);
            shown = 0;
        }

        for (var i = shown; i < _results.Count; i++)
            _visible.Add(new ResultHitLine(_results[i]));
        MarkCurrent();
    }

    private void MarkCurrent()
    {
        for (var i = 0; i < _visible.Count; i++)
        {
            if (_visible[i] is ResultHitLine hit)
                hit.SetCurrent(ReferenceEquals(hit.Result, _current));
        }
    }

    private FindResult Describe(Hit hit)
    {
        var text = _session!.ReadHitText(hit, _resultWidth);
        return new FindResult(hit, text.Text, text.MarkStart, text.MarkLength, _session.FormatPath(hit.Path));
    }

    private void RememberFindWhat()
    {
        var sequence = _findWhat;
        if (sequence.Length == 0)
            return;
        for (var i = 0; i < _findHistory.Count; i++)
        {
            if (!string.Equals(_findHistory[i], sequence, StringComparison.Ordinal))
                continue;
            if (i == 0)
                return;
            _findHistory.RemoveAt(i);
            break;
        }

        _findHistory.Insert(0, sequence);
        while (_findHistory.Count > 20)
            _findHistory.RemoveAt(_findHistory.Count - 1);
    }

    private int CurrentIndex()
    {
        if (_current is null)
            return -1;
        for (var i = 0; i < _results.Count; i++)
        {
            if (ReferenceEquals(_results[i], _current))
                return i;
        }

        return -1;
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

    private void PublishScan()
    {
        var progress = _session!.Progress;
        var length = _session.DocumentLength;
        var reached = _session.BytesReached;
        var show = _session.LengthIsFixed
            && length > 0
            && progress is ScanProgress.Scanning or ScanProgress.Stopped;
        var fraction = 0d;
        if (show && length > 0)
        {
            fraction = (double)reached / length;
            if (fraction < 0)
                fraction = 0;
            else if (fraction > 1)
                fraction = 1;
        }

        ScanFraction = fraction;
        ScanFractionShown = show;
        RemainingTime = ScanRemainingTime();
        Error = _session.Error is null
            ? ""
            : $"{_session.Error.Message} (line {_session.Error.Line}, column {_session.Error.Column})";
        Status = progress switch
        {
            ScanProgress.Scanning => "Scanning",
            ScanProgress.Completed => "Complete",
            _ => "Stopped",
        };
    }

    private string ScanRemainingTime()
    {
        var progress = _session!.Progress;
        var length = _session.DocumentLength;
        var reached = _session.BytesReached;
        var show = _session.LengthIsFixed
            && length > 0
            && progress is ScanProgress.Scanning or ScanProgress.Stopped;
        var elapsed = _scanElapsed ?? Environment.TickCount64 - _scanStartedAt;
        if (elapsed < 0)
            elapsed = 0;
        if (progress != ScanProgress.Scanning || !show || elapsed < 1000 || reached <= 0)
            return "";
        return RemainingText(length, reached, elapsed);
    }

    private static string RemainingText(long length, long reached, long elapsed)
    {
        var ahead = length - reached;
        if (ahead <= 0 || reached <= 0)
            return "";
        var quotient = (decimal)ahead * elapsed / reached;
        if (quotient <= 0)
            return "";
        if (quotient < 1000)
            return "<1s remaining";
        var seconds = (long)decimal.Ceiling(quotient / 1000m);
        return seconds.ToString(CultureInfo.InvariantCulture) + "s remaining";
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
