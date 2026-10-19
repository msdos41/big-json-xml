using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace BigJsonXml.Desktop;

public partial class FindWindow : Window
{
    public FindWindow()
    {
        InitializeComponent();
    }

    private void FindAllClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ViewerModel model)
            model.FindAll();
    }

    private void FindNextClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ViewerModel model)
            model.FindNext();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.O && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            MainWindow.RequestOpen(this);
            e.Handled = true;
            return;
        }

        if (e.Key != Key.F || !e.KeyModifiers.HasFlag(KeyModifiers.Control))
            return;
        MainWindow.RequestFind(this);
        e.Handled = true;
    }

    protected override void OnClosed(EventArgs e)
    {
        DataContext = null;
        base.OnClosed(e);
    }
}
