using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace BigJsonXml.Desktop;

public sealed class ViewerModel : INotifyPropertyChanged, IDisposable
{
    public const int ScanSliceMilliseconds = 32;
    public const int ScanIdleMilliseconds = 16;
    public const int QuiescentScanSliceMilliseconds = 64;
    public const int QuiescentScanIdleMilliseconds = 4;
    public const int QuiescenceThresholdMilliseconds = 250;
    public const int ScanRefreshIntervalMilliseconds = 33;
    public const double FoundColumnMin = 64;
    public const double FoundColumnMax = 4000;
    public const double FoundColumnPadding = 8;

    public int CurrentScanSliceMilliseconds { get; internal set; } = ScanSliceMilliseconds;
    public int CurrentScanIdleMilliseconds { get; internal set; } = ScanIdleMilliseconds;
    public long LastInteractionTime { get; set; } = Environment.TickCount64;

    public void NoteUserInteraction(long timestamp = 0)
    {
        LastInteractionTime = timestamp > 0 ? timestamp : Environment.TickCount64;
        CurrentScanSliceMilliseconds = ScanSliceMilliseconds;
        CurrentScanIdleMilliseconds = ScanIdleMilliseconds;
    }

    private readonly ObservableCollection<DocumentTab> _tabs = [];
    private readonly ObservableCollection<FindResult> _emptyResults = [];
    private readonly ObservableCollection<ResultDocument> _emptyDocuments = [];
    private readonly ObservableCollection<ResultLine> _emptyLines = [];
    private ObservableCollection<FindResult> _results;
    private ObservableCollection<ResultDocument> _documents;
    private ObservableCollection<ResultLine> _visible;
    private int _active = -1;
    private string? _openFailure;
    private double _stripWidth = double.PositiveInfinity;
    private double _offset;
    private double _publishedOffset;
    private bool _arrows;
    private bool _canLeft;
    private bool _canRight;
    private long _lastScanRefreshTime;
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
    private string _status = "No document";
    private string _error = "";
    private bool _scanFractionShown;
    private double _scanFraction;
    private string _remainingTime = "";
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
    private readonly ObservableCollection<string> _findHistory = [];
    private CellSelection? _selection;
    private bool _findWindowOpen;
    private bool _resultsWindowOpen;

    public ViewerModel()
    {
        _results = _emptyResults;
        _documents = _emptyDocuments;
        _visible = _emptyLines;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<DocumentTab> Tabs => _tabs;

    public int ActiveIndex => _active;

    public DocumentTab? ActiveTab => Active;

    public bool TabArrowsVisible => _arrows;

    public bool CanMoveTabsLeft => _canLeft;

    public bool CanMoveTabsRight => _canRight;

    public double TabStripOffset => _offset;

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
        if (findWasOpen || resultsWereOpen)
        {
            StopSearches();
            ClearMarks();
        }
    }

    public void CloseResults()
    {
        if (!_resultsWindowOpen)
            return;
        _resultsWindowOpen = false;
        OnPropertyChanged(nameof(ResultsWindowOpen));
        StopSearches();
        ClearMark();
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
            if (Active is not null && !_refreshing)
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
            if (Active is { } tab)
                tab.Current = value;
            Mark(VisibleOfActive(), value);
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
        var tab = Active;
        if (tab is null)
            return;
        tab.ScanElapsed = milliseconds;
        if (_refreshing)
        {
            RemainingTime = ScanRemainingTime(tab);
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
        NoteUserInteraction();
        string full;
        try
        {
            full = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            FailOpen(ex.Message);
            return;
        }

        var existing = FindPath(full);
        if (existing is not null)
        {
            _openFailure = null;
            _active = _tabs.IndexOf(existing);
            PublishActive();
            Layout(true);
            return;
        }

        FileStream stream;
        try
        {
            stream = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1 << 20, FileOptions.SequentialScan);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            FailOpen(ex.Message);
            return;
        }

        var source = new StreamByteSource(stream);
        ViewerSession session;
        try
        {
            session = ViewerSession.Open(source);
        }
        catch
        {
            source.Dispose();
            throw;
        }

        Adopt(session, full, Path.GetFileName(full));
    }

    public void Open(IByteSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        NoteUserInteraction();
        Adopt(ViewerSession.Open(source), "", "");
    }

    public void Activate(DocumentTab tab)
    {
        ArgumentNullException.ThrowIfNull(tab);
        var index = _tabs.IndexOf(tab);
        if (index < 0)
            return;
        if (index != _active)
        {
            _openFailure = null;
            _active = index;
            PublishActive();
        }

        Layout(true);
    }

    public void Close(DocumentTab tab)
    {
        ArgumentNullException.ThrowIfNull(tab);
        var index = _tabs.IndexOf(tab);
        if (index < 0)
            return;
        var wasActive = index == _active;
        _tabs.RemoveAt(index);
        tab.Dispose();
        if (_tabs.Count == 0)
        {
            _active = -1;
            _openFailure = null;
            PublishEmpty();
            Layout(false);
            return;
        }

        if (wasActive)
        {
            _openFailure = null;
            _active = Math.Min(index, _tabs.Count - 1);
            PublishActive();
        }
        else if (index < _active)
        {
            _active--;
            OnPropertyChanged(nameof(ActiveIndex));
        }

        Layout(wasActive);
    }

    public void SetTabStripWidth(double width)
    {
        if (width <= 1 || double.IsNaN(width) || double.IsInfinity(width))
            return;
        if (!double.IsPositiveInfinity(_stripWidth) && Math.Abs(_stripWidth - width) < 0.5)
            return;
        var reveal = double.IsPositiveInfinity(_stripWidth);
        var atEnd = IsAtRightEnd();
        _stripWidth = width;
        Layout(reveal, atEnd);
    }

    public void SetTabWidth(DocumentTab tab, double width)
    {
        ArgumentNullException.ThrowIfNull(tab);
        if (width <= 0 || _tabs.IndexOf(tab) < 0 || Math.Abs(tab.Width - width) < 0.5)
            return;
        var atEnd = IsAtRightEnd();
        tab.SetWidth(width);
        Layout(false, atEnd);
    }

    public void RevealActiveTab() => Layout(true);

    public void MoveTabsLeft()
    {
        if (!_canLeft)
            return;
        DocumentTab? target = null;
        foreach (var tab in _tabs)
        {
            if (tab.Left < _offset - 0.5)
                target = tab;
        }

        if (target is null)
            return;
        _offset = target.Left;
        Layout(false);
    }

    public void MoveTabsRight()
    {
        if (!_canRight)
            return;
        var view = ViewWidth();
        foreach (var tab in _tabs)
        {
            if (tab.Left + tab.Width > _offset + view + 0.5)
            {
                _offset = tab.Left + tab.Width - view;
                break;
            }
        }

        Layout(false);
    }

    public void Tick()
    {
        var now = Environment.TickCount64;
        if (now - LastInteractionTime >= QuiescenceThresholdMilliseconds)
        {
            CurrentScanSliceMilliseconds = QuiescentScanSliceMilliseconds;
            CurrentScanIdleMilliseconds = QuiescentScanIdleMilliseconds;
        }
        else
        {
            CurrentScanSliceMilliseconds = ScanSliceMilliseconds;
            CurrentScanIdleMilliseconds = ScanIdleMilliseconds;
        }

        var active = Active;
        if (active is not null)
        {
            var start = Environment.TickCount64;
            var worked = false;
            do
            {
                if (!active.Session.Pump(512))
                    break;
                worked = true;
            }
            while (Environment.TickCount64 - start < CurrentScanSliceMilliseconds);

            if (worked)
            {
                var isScanning = active.Session.Progress == ScanProgress.Scanning;
                var hasNewHits = active.Session.Hits.Count != active.Results.Count;
                var elapsed = Environment.TickCount64 - _lastScanRefreshTime;
                if (!isScanning || hasNewHits || elapsed >= ScanRefreshIntervalMilliseconds)
                {
                    _lastScanRefreshTime = Environment.TickCount64;
                    Refresh();
                }
                else
                {
                    PublishScan(active);
                }
            }
        }

        foreach (var tab in _tabs.ToArray())
        {
            if (ReferenceEquals(tab, Active))
                continue;
            if (!tab.Session.Pump(512))
                continue;
            tab.NoteProgress();
            SyncResults(tab, false);
        }

        if (ApplyPendingFits(active))
            Refresh();
    }

    private bool ApplyPendingFits(DocumentTab? active)
    {
        var applied = false;
        foreach (var tab in _tabs.ToArray())
        {
            if (tab.Session.AdvanceFit() == FitAdvance.Applied && ReferenceEquals(tab, active))
                applied = true;
        }

        return applied;
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

        Active?.Session.SetViewport(_scrollX, _scrollY, _viewportWidth, _viewportHeight);
        Refresh();
    }

    public void FindAll()
    {
        var tab = Active;
        if (tab is null)
            return;
        NoteUserInteraction();
        RememberFindWhat();
        EnsureSearched(tab);
        tab.Session.Search(Criteria());
        CurrentResult = null;
        Refresh();
        Tick();
        OpenResults();
    }

    public void FindNext()
    {
        var tab = Active;
        if (tab is null)
            return;
        var index = CurrentIndex();
        while (tab.Session.Hits.Count <= index + 1 && tab.Session.Pump(512))
        {
        }

        Refresh();
        if (index + 1 >= _results.Count)
            return;
        var result = _results[index + 1];
        ExpandResults(tab);
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
        var tab = Active;
        if (tab is null)
            return;
        tab.Session.Reveal(hit);
        _scrollX = tab.Session.ViewportX;
        _scrollY = tab.Session.ViewportY;
        tab.ScrollX = _scrollX;
        tab.ScrollY = _scrollY;
        Refresh();
        OnPropertyChanged(nameof(ScrollX));
        OnPropertyChanged(nameof(ScrollY));
    }

    public void ChooseResult(FindResult? result)
    {
        if (_adjustingResults || Active is null)
            return;
        if (result is not null && Active is { } tab)
            ExpandResults(tab);
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
        var tab = Active;
        if (tab is null || tab.Documents.Count == 0 || !ReferenceEquals(tab.Documents[0], document))
            return;
        _adjustingResults = true;
        try
        {
            SyncVisible(tab);
        }
        finally
        {
            _adjustingResults = false;
        }
    }

    public void ClearMark()
    {
        if (Active is null)
            return;
        Active.Session.ClearMark();
        Refresh();
    }

    private void ClearMarks()
    {
        foreach (var tab in _tabs)
            tab.Session.ClearMark();
        if (Active is not null)
            Refresh();
    }

    private void StopSearches()
    {
        foreach (var tab in _tabs)
            tab.Session.CancelSearch();
    }

    public void Toggle(SurfaceRow row)
    {
        var tab = Active;
        if (tab is null)
            return;
        if (row.CanExpand)
        {
            if (row.IsExpanded)
                tab.Session.Collapse(row.Path);
            else
                tab.Session.Expand(row.Path);
        }
        else if (row.CanGrowValue || row.Kind == SurfaceKind.ValueLine)
        {
            if (tab.Session.ReadScalar(row.Path).IsFullValueExpanded)
                tab.Session.CollapseFullValue(row.Path);
            else
                tab.Session.ExpandFullValue(row.Path);
        }

        Refresh();
    }

    public void SetSelection(CellSelection? selection)
    {
        Selection = selection;
        if (Active is { } tab)
            tab.Selection = selection;
    }

    public string? SelectedText()
    {
        var tab = Active;
        if (_selection is not { Length: > 0 } selection || tab is null)
            return null;
        if (selection.Scalar)
            return tab.Session.ReadFullValueSlice(selection.Path, selection.Start, selection.Length);
        if (selection.Label is null || selection.Start > selection.Label.Length)
            return null;
        var end = Math.Min(selection.End, selection.Label.Length);
        if (end <= selection.Start)
            return null;
        return selection.Label[selection.Start..end];
    }

    public int ScalarLength(DocumentPath path) => Active?.Session.ScalarLength(path) ?? 0;

    public void SelectAll()
    {
        var tab = Active;
        if (_selection is null || tab is null)
            return;
        if (_selection.Scalar)
            SetSelection(_selection with { Start = 0, End = tab.Session.ScalarLength(_selection.Path) });
        else if (_selection.Label is not null)
            SetSelection(_selection with { Start = 0, End = _selection.Label.Length });
    }

    public void FitWidth(SurfaceRow row)
    {
        var tab = Active;
        if (tab is null || row.WidthTable is null || row.Axis == WidthAxis.None)
            return;
        tab.Session.BeginFit(row.Axis, row.WidthTable, row.WidthColumn);
        if (tab.Session.AdvanceFit() == FitAdvance.Applied)
            Refresh();
    }

    public void DragWidth(SurfaceRow row, double width)
    {
        var tab = Active;
        if (tab is null || row.WidthTable is null)
            return;
        tab.Session.CancelFit();
        switch (row.Axis)
        {
            case WidthAxis.RecordName:
                tab.Session.SetRecordNameWidth(row.WidthTable, width);
                break;
            case WidthAxis.RecordValue:
                tab.Session.SetRecordValueWidth(row.WidthTable, width);
                break;
            case WidthAxis.ListColumn when row.WidthColumn is not null:
                tab.Session.SetColumnWidth(row.WidthTable, row.WidthColumn, width);
                break;
            default:
                return;
        }

        Refresh();
    }

    public void Dispose()
    {
        foreach (var tab in _tabs.ToArray())
            tab.Dispose();
        _tabs.Clear();
        _active = -1;
    }

    private DocumentTab? Active => _active >= 0 && _active < _tabs.Count ? _tabs[_active] : null;

    private void Adopt(ViewerSession session, string path, string label)
    {
        _openFailure = null;
        var tab = new DocumentTab(session, path, label)
        {
            ScanStartedAt = Environment.TickCount64,
        };
        _tabs.Add(tab);
        _active = _tabs.Count - 1;
        PublishActive();
        Layout(true);
        Tick();
    }

    private void FailOpen(string message)
    {
        _openFailure = message;
        Error = message;
    }

    private DocumentTab? FindPath(string full)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        foreach (var tab in _tabs)
        {
            if (tab.Path.Length > 0 && string.Equals(tab.Path, full, comparison))
                return tab;
        }

        return null;
    }

    private void PublishActive()
    {
        var tab = Active;
        if (tab is null)
            return;
        foreach (var item in _tabs)
            item.SetActive(ReferenceEquals(item, tab));
        _scrollX = tab.ScrollX;
        _scrollY = tab.ScrollY;
        _selection = tab.Selection;
        OnPropertyChanged(nameof(ScrollX));
        OnPropertyChanged(nameof(ScrollY));
        OnPropertyChanged(nameof(Selection));
        OnPropertyChanged(nameof(ActiveIndex));
        OnPropertyChanged(nameof(ActiveTab));
        _adjustingResults = true;
        try
        {
            UseResults(tab.Results, tab.Documents, tab.Visible);
            _current = tab.Current;
            OnPropertyChanged(nameof(CurrentResult));
            Mark(tab.Visible, tab.Current);
        }
        finally
        {
            _adjustingResults = false;
        }

        Refresh();
    }

    private void PublishEmpty()
    {
        foreach (var item in _tabs)
            item.SetActive(false);
        _scrollX = 0;
        _scrollY = 0;
        _selection = null;
        Rows = [];
        _adjustingResults = true;
        try
        {
            UseResults(_emptyResults, _emptyDocuments, _emptyLines);
            _current = null;
            OnPropertyChanged(nameof(CurrentResult));
        }
        finally
        {
            _adjustingResults = false;
        }

        ContentWidth = 0;
        ContentHeight = 0;
        ScrollMaxX = 0;
        ScrollMaxY = 0;
        ScanFraction = 0;
        ScanFractionShown = false;
        RemainingTime = "";
        Error = "";
        Status = "No document";
        OnPropertyChanged(nameof(ScrollX));
        OnPropertyChanged(nameof(ScrollY));
        OnPropertyChanged(nameof(Selection));
        OnPropertyChanged(nameof(ActiveIndex));
        OnPropertyChanged(nameof(ActiveTab));
    }

    private void UseResults(
        ObservableCollection<FindResult> results,
        ObservableCollection<ResultDocument> documents,
        ObservableCollection<ResultLine> visible)
    {
        _results = results;
        _documents = documents;
        _visible = visible;
        OnPropertyChanged(nameof(Results));
        OnPropertyChanged(nameof(ResultDocuments));
        OnPropertyChanged(nameof(VisibleResults));
        OnPropertyChanged(nameof(ResultCount));
    }

    private void Layout(bool ensureActive, bool stickToEnd = false)
    {
        double total = 0;
        foreach (var tab in _tabs)
        {
            if (tab.Width <= 0)
                tab.SetWidth(DocumentTab.EstimateWidth(tab.Label));
            tab.SetLeft(total);
            total += tab.Width;
        }

        var arrows = !double.IsPositiveInfinity(_stripWidth) && total > _stripWidth + 0.5;
        var view = arrows ? Math.Max(1, _stripWidth - DocumentTab.ArrowSpan) : _stripWidth;
        if (!arrows)
            _offset = 0;
        else if (ensureActive && Active is { } active)
            RevealOffset(active, view, total);
        else if (stickToEnd)
            _offset = Math.Max(0, total - view);
        else
            ClampOffset(view, total);

        var canLeft = arrows && _offset > 0.5;
        var canRight = arrows && _tabs.Count > 0 && _tabs[^1].Left + _tabs[^1].Width > _offset + view + 0.5;
        foreach (var tab in _tabs)
        {
            var right = tab.Left + tab.Width;
            var fully = double.IsPositiveInfinity(view) || (tab.Left >= _offset - 0.5 && right <= _offset + view + 0.5);
            tab.SetFullyVisible(fully);
        }

        if (Math.Abs(_publishedOffset - _offset) >= 0.5)
        {
            _publishedOffset = _offset;
            OnPropertyChanged(nameof(TabStripOffset));
        }

        Set(ref _arrows, arrows, nameof(TabArrowsVisible));
        Set(ref _canLeft, canLeft, nameof(CanMoveTabsLeft));
        Set(ref _canRight, canRight, nameof(CanMoveTabsRight));
    }

    private bool IsAtRightEnd()
    {
        if (_tabs.Count == 0 || double.IsPositiveInfinity(_stripWidth))
            return false;
        double total = 0;
        foreach (var tab in _tabs)
            total += tab.Width;
        if (total <= _stripWidth + 0.5)
            return false;
        var view = Math.Max(1, _stripWidth - DocumentTab.ArrowSpan);
        return _tabs[^1].Left + _tabs[^1].Width <= _offset + view + 0.5;
    }

    private void RevealOffset(DocumentTab tab, double view, double total)
    {
        var left = tab.Left;
        var right = left + tab.Width;
        if (left < _offset)
            _offset = left;
        if (right > _offset + view)
            _offset = right - view;
        ClampOffset(view, total);
    }

    private void ClampOffset(double view, double total)
    {
        if (double.IsPositiveInfinity(view))
        {
            _offset = 0;
            return;
        }

        var max = Math.Max(0, total - view);
        if (_offset < 0)
            _offset = 0;
        if (_offset > max)
            _offset = max;
    }

    private double ViewWidth()
    {
        if (double.IsPositiveInfinity(_stripWidth))
            return _stripWidth;
        double total = 0;
        foreach (var tab in _tabs)
            total += tab.Width;
        var arrows = total > _stripWidth + 0.5;
        return arrows ? Math.Max(1, _stripWidth - DocumentTab.ArrowSpan) : _stripWidth;
    }

    private void Move(ref double field, double value, string name)
    {
        value = Math.Max(0, value);
        if (Math.Abs(field - value) < 0.5)
            return;
        field = value;
        if (Active is { } tab)
        {
            tab.ScrollX = _scrollX;
            tab.ScrollY = _scrollY;
        }

        OnPropertyChanged(name);
        if (Active is null)
            return;
        if (_refreshing)
        {
            _rebuild = true;
            return;
        }

        Active.Session.SetViewport(_scrollX, _scrollY, _viewportWidth, _viewportHeight);
        Refresh();
    }

    private void Refresh()
    {
        var tab = Active;
        if (tab is null || _refreshing)
            return;
        _refreshing = true;
        try
        {
            for (var pass = 0; pass < 4; pass++)
            {
                _rebuild = false;
                _scrollX = Math.Max(0, _scrollX);
                _scrollY = Math.Max(0, _scrollY);
                var frame = ReadFrame(tab);
                var maxX = Math.Max(0, frame.ContentWidth - _viewportWidth);
                var maxY = Math.Max(0, frame.ContentHeight - _viewportHeight);
                var x = Math.Min(_scrollX, maxX);
                var y = Math.Min(_scrollY, maxY);
                var clamped = Math.Abs(x - _scrollX) >= 0.5 || Math.Abs(y - _scrollY) >= 0.5;
                if (clamped)
                {
                    _scrollX = x;
                    _scrollY = y;
                    frame = ReadFrame(tab);
                    maxX = Math.Max(0, frame.ContentWidth - _viewportWidth);
                    maxY = Math.Max(0, frame.ContentHeight - _viewportHeight);
                }

                Rows = frame.Rows;
                SyncResults(tab, true);
                ContentWidth = frame.ContentWidth;
                ContentHeight = frame.ContentHeight;
                if (clamped)
                {
                    OnPropertyChanged(nameof(ScrollX));
                    OnPropertyChanged(nameof(ScrollY));
                }

                ScrollMaxX = maxX;
                ScrollMaxY = maxY;
                PublishScan(tab);
                if (!_rebuild)
                    break;
            }

            tab.ScrollX = _scrollX;
            tab.ScrollY = _scrollY;
        }
        finally
        {
            _refreshing = false;
        }
    }

    private SurfaceFrame ReadFrame(DocumentTab tab)
    {
        tab.Session.SetViewport(_scrollX, _scrollY, _viewportWidth, _viewportHeight);
        return tab.Session.ReadSurface();
    }

    private void SyncResults(DocumentTab tab, bool active)
    {
        var next = tab.Session.Hits;
        if (tab.BuiltWidth == _resultWidth && IsPrefix(tab, next))
        {
            for (var i = 0; i < tab.Results.Count; i++)
                tab.Results[i].RevisePath(tab.Session.FormatPath(next[i].Path));
            var grown = false;
            for (var i = tab.Results.Count; i < next.Count; i++)
            {
                if (!TryDescribe(tab, next[i], out var described))
                    break;
                tab.Results.Add(described);
                grown = true;
            }

            if (grown && active)
                OnPropertyChanged(nameof(ResultCount));
            SyncVisible(tab);
            return;
        }

        ReplaceResults(tab, next, active);
    }

    private void ReplaceResults(DocumentTab tab, IReadOnlyList<Hit> next, bool active)
    {
        var adjusting = _adjustingResults;
        if (active)
            _adjustingResults = true;
        try
        {
            var currentHit = tab.Current?.Hit;
            tab.Results.Clear();
            foreach (var hit in next)
            {
                if (!TryDescribe(tab, hit, out var described))
                    break;
                tab.Results.Add(described);
            }
            tab.BuiltWidth = _resultWidth;
            SyncVisible(tab);
            if (currentHit is null)
                return;
            FindResult? again = null;
            for (var i = 0; i < tab.Results.Count; i++)
            {
                if (!ReferenceEquals(tab.Results[i].Hit, currentHit))
                    continue;
                again = tab.Results[i];
                break;
            }

            if (again is null)
                return;
            if (active)
                CurrentResult = again;
            else
                tab.Current = again;
        }
        finally
        {
            if (active)
                _adjustingResults = adjusting;
        }

        if (active)
            OnPropertyChanged(nameof(ResultCount));
    }

    private void EnsureSearched(DocumentTab tab)
    {
        tab.Searched = true;
        if (tab.Documents.Count == 0)
            tab.Documents.Add(new ResultDocument(tab.Path));
        tab.Documents[0].SetExpanded(true);
    }

    private void OpenResults()
    {
        _resultsWindowOpen = true;
        OnPropertyChanged(nameof(ResultsWindowOpen));
    }

    private void ExpandResults(DocumentTab tab)
    {
        if (!tab.Searched || tab.Documents.Count == 0 || tab.Documents[0].IsExpanded)
            return;
        tab.Documents[0].SetExpanded(true);
        _adjustingResults = true;
        try
        {
            SyncVisible(tab);
        }
        finally
        {
            _adjustingResults = false;
        }
    }

    private void SyncVisible(DocumentTab tab)
    {
        if (!tab.Searched || tab.Documents.Count == 0)
        {
            if (tab.Visible.Count > 0)
                tab.Visible.Clear();
            return;
        }

        var document = tab.Documents[0];
        document.SetMatchCount(tab.Results.Count);
        if (tab.Visible.Count == 0 || tab.Visible[0] is not ResultDocumentLine group || !ReferenceEquals(group.Document, document))
        {
            tab.Visible.Clear();
            tab.Visible.Add(new ResultDocumentLine(document));
        }

        if (!document.IsExpanded)
        {
            while (tab.Visible.Count > 1)
                tab.Visible.RemoveAt(tab.Visible.Count - 1);
            Mark(tab.Visible, tab.Current);
            return;
        }

        var shown = tab.Visible.Count - 1;
        var prefix = shown <= tab.Results.Count;
        for (var i = 0; prefix && i < shown; i++)
        {
            if (tab.Visible[i + 1] is not ResultHitLine hit || !ReferenceEquals(hit.Result, tab.Results[i]))
                prefix = false;
        }

        if (!prefix)
        {
            while (tab.Visible.Count > 1)
                tab.Visible.RemoveAt(tab.Visible.Count - 1);
            shown = 0;
        }

        for (var i = shown; i < tab.Results.Count; i++)
            tab.Visible.Add(new ResultHitLine(tab.Results[i]));
        Mark(tab.Visible, tab.Current);
    }

    private ObservableCollection<ResultLine> VisibleOfActive() => Active?.Visible ?? _visible;

    private static void Mark(ObservableCollection<ResultLine> visible, FindResult? current)
    {
        for (var i = 0; i < visible.Count; i++)
        {
            if (visible[i] is ResultHitLine hit)
                hit.SetCurrent(ReferenceEquals(hit.Result, current));
        }
    }

    private bool TryDescribe(DocumentTab tab, Hit hit, out FindResult result)
    {
        try
        {
            var text = tab.Session.ReadHitText(hit, _resultWidth);
            result = new FindResult(hit, text.Text, text.MarkStart, text.MarkLength, tab.Session.FormatPath(hit.Path));
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentOutOfRangeException)
        {
            // The tree and the search take turns. A hit can name a cell that is
            // not readable yet. Leave it for the next refresh instead of tearing
            // the process down while the results window is closing.
            result = null!;
            return false;
        }
    }

    private TextSearch Criteria() => new()
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
    };

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

    private static bool IsPrefix(DocumentTab tab, IReadOnlyList<Hit> next)
    {
        if (tab.Results.Count > next.Count)
            return false;
        for (var i = 0; i < tab.Results.Count; i++)
        {
            if (!ReferenceEquals(tab.Results[i].Hit, next[i]))
                return false;
        }

        return true;
    }

    private void PublishScan(DocumentTab tab)
    {
        tab.NoteProgress();
        var session = tab.Session;
        var progress = session.Progress;
        var length = session.DocumentLength;
        var reached = session.BytesReached;
        var show = session.LengthIsFixed
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
        RemainingTime = ScanRemainingTime(tab);
        if (_openFailure is null)
        {
            Error = session.Error is null
                ? ""
                : $"{session.Error.Message} (line {session.Error.Line}, column {session.Error.Column})";
        }

        Status = progress switch
        {
            ScanProgress.Scanning => "Scanning",
            ScanProgress.Completed => "Complete",
            _ => "Stopped",
        };
    }

    private static string ScanRemainingTime(DocumentTab tab)
    {
        var session = tab.Session;
        var progress = session.Progress;
        var length = session.DocumentLength;
        var reached = session.BytesReached;
        var show = session.LengthIsFixed
            && length > 0
            && progress is ScanProgress.Scanning or ScanProgress.Stopped;
        var elapsed = tab.ScanElapsed ?? Environment.TickCount64 - tab.ScanStartedAt;
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
