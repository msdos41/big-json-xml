using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace BigJsonXml.Desktop;

public partial class MainWindow : Window
{
    private readonly ViewerModel _model = new();
    private readonly DispatcherTimer _timer;
    private FindWindow? _find;
    private ResultsWindow? _results;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _model;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ViewerModel.ScanIdleMilliseconds) };
        _timer.Tick += (_, _) => _model.Tick();
        _timer.Start();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (_find is not null)
            return;
        _find = new FindWindow { DataContext = _model };
        _results = new ResultsWindow { DataContext = _model };
        _find.Show(this);
        _results.Show(this);
        _find.Position = new PixelPoint(Position.X + 32, Position.Y + 72);
        _results.Position = new PixelPoint(Position.X + 420, Position.Y + 72);
    }

    private async void OpenClicked(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open XML",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("XML") { Patterns = ["*.xml"] },
                new FilePickerFileType("All files") { Patterns = ["*"] },
            ],
        });
        var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
        if (path is not null)
            _model.OpenPath(path);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key != Key.Escape)
            return;
        _model.ClearMark();
        e.Handled = true;
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        _find?.Close();
        _results?.Close();
        _model.Dispose();
        base.OnClosed(e);
    }
}
