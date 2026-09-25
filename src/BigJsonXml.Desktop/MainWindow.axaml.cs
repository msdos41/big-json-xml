using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace BigJsonXml.Desktop;

public partial class MainWindow : Window
{
    private readonly ViewerModel _model = new();
    private readonly DispatcherTimer _timer;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _model;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        _timer.Tick += (_, _) => _model.Tick();
        _timer.Start();
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

    private void SearchClicked(object? sender, RoutedEventArgs e) => _model.Search();

    private void HitSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is Hit hit)
            _model.Reveal(hit);
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        _model.Dispose();
        base.OnClosed(e);
    }
}
