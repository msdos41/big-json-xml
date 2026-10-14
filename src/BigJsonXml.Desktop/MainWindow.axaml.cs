using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace BigJsonXml.Desktop;

public partial class MainWindow : Window
{
    public static FilePickerFileType[] OpenFileTypes { get; } =
    [
        new("XML") { Patterns = ["*.xml"] },
        new("JSON") { Patterns = ["*.json"] },
        new("All files") { Patterns = ["*"] },
    ];

    private readonly ViewerModel _model = new();
    private readonly DispatcherTimer _timer;
    private FindWindow? _find;
    private ResultsWindow? _results;
    private bool _findAlive;
    private bool _resultsAlive;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _model;
        _model.PropertyChanged += OnModelChanged;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ViewerModel.ScanIdleMilliseconds) };
        _timer.Tick += (_, _) => OnTimerTick();
        _timer.Start();

        AddHandler(InputElement.PointerMovedEvent, (_, _) => NoteInteraction(), RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(InputElement.PointerPressedEvent, (_, _) => NoteInteraction(), RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(InputElement.PointerWheelChangedEvent, (_, _) => NoteInteraction(), RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(InputElement.KeyDownEvent, (_, _) => NoteInteraction(), RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(InputElement.KeyUpEvent, (_, _) => NoteInteraction(), RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private void OnTimerTick()
    {
        _model.Tick();
        var desired = TimeSpan.FromMilliseconds(_model.CurrentScanIdleMilliseconds);
        if (_timer.Interval != desired)
            _timer.Interval = desired;
    }

    public void NoteInteraction()
    {
        _model.NoteUserInteraction();
        var desired = TimeSpan.FromMilliseconds(_model.CurrentScanIdleMilliseconds);
        if (_timer.Interval != desired)
            _timer.Interval = desired;
    }

    private void FindClicked(object? sender, RoutedEventArgs e) => ShowFindFromUi();

    public void ShowFindFromUi()
    {
        _model.ShowFind();
        if (_find is { IsVisible: true })
            _find.Activate();
        else
            ApplyFindWindow(true);
    }

    public static void RequestFind(Visual visual)
    {
        var window = TopLevel.GetTopLevel(visual) as Window;
        while (window is not null)
        {
            if (window is MainWindow main)
            {
                main.ShowFindFromUi();
                return;
            }

            window = window.Owner as Window;
        }
    }

    private async void OpenClicked(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open",
            AllowMultiple = false,
            FileTypeFilter = OpenFileTypes,
        });
        var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
        if (path is not null)
            _model.OpenPath(path);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        NoteInteraction();
        base.OnPointerMoved(e);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        NoteInteraction();
        base.OnPointerPressed(e);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        NoteInteraction();
        base.OnPointerWheelChanged(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        NoteInteraction();
        base.OnKeyUp(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        NoteInteraction();
        base.OnKeyDown(e);
        if (e.Key == Key.F && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            ShowFindFromUi();
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Escape)
            return;
        _model.ClearMark();
        e.Handled = true;
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        _model.PropertyChanged -= OnModelChanged;
        CloseFindWindow();
        CloseResultsWindow();
        _model.Dispose();
        base.OnClosed(e);
    }

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewerModel.FindWindowOpen))
            ApplyFindWindow(_model.FindWindowOpen);
        else if (e.PropertyName == nameof(ViewerModel.ResultsWindowOpen))
            ApplyResultsWindow(_model.ResultsWindowOpen);
    }

    private void ApplyFindWindow(bool open)
    {
        if (!open)
        {
            CloseFindWindow();
            return;
        }

        if (_findAlive && _find is { IsVisible: true })
            return;

        DetachFind();
        _find = new FindWindow { DataContext = _model };
        _find.Closed += OnFindClosed;
        _findAlive = true;
        _find.Show(this);
        _find.Position = new PixelPoint(Position.X + 32, Position.Y + 72);
    }

    private void ApplyResultsWindow(bool open)
    {
        if (!open)
        {
            CloseResultsWindow();
            return;
        }

        if (_resultsAlive && _results is { IsVisible: true })
            return;

        DetachResults();
        _results = new ResultsWindow { DataContext = _model };
        _results.Closed += OnResultsClosed;
        _resultsAlive = true;
        _results.Show(_findAlive && _find is { IsVisible: true } ? _find : this);
        _results.Position = new PixelPoint(Position.X + 420, Position.Y + 72);
    }

    private void DetachFind()
    {
        if (_find is null)
            return;
        _find.Closed -= OnFindClosed;
        _findAlive = false;
        _find = null;
    }

    private void DetachResults()
    {
        if (_results is null)
            return;
        _results.Closed -= OnResultsClosed;
        _resultsAlive = false;
        _results = null;
    }

    private void CloseFindWindow()
    {
        if (!_findAlive || _find is null)
            return;
        _findAlive = false;
        _find.Close();
    }

    private void CloseResultsWindow()
    {
        if (!_resultsAlive || _results is null)
            return;
        _resultsAlive = false;
        _results.Close();
    }

    private void OnFindClosed(object? sender, EventArgs e)
    {
        if (sender is not FindWindow window || !ReferenceEquals(_find, window))
            return;
        window.Closed -= OnFindClosed;
        _findAlive = false;
        _find = null;
        _model.CloseFind();
    }

    private void OnResultsClosed(object? sender, EventArgs e)
    {
        if (sender is not ResultsWindow window || !ReferenceEquals(_results, window))
            return;
        window.Closed -= OnResultsClosed;
        _resultsAlive = false;
        _results = null;
        _model.CloseResults();
    }
}
