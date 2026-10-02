using Avalonia.Controls;
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

    protected override void OnClosed(EventArgs e)
    {
        DataContext = null;
        base.OnClosed(e);
    }
}
