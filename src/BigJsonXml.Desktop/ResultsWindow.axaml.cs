using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace BigJsonXml.Desktop;

public partial class ResultsWindow : Window
{
    public ResultsWindow()
    {
        InitializeComponent();
    }

    private void ResultSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not ViewerModel model)
            return;
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is FindResult result)
            model.ChooseResult(result);
        else if (e.RemovedItems.Count > 0)
            model.ChooseResult(null);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key != Key.Escape)
            return;
        if (ResultsList.SelectedItem is not null)
            ResultsList.SelectedItem = null;
        else if (DataContext is ViewerModel model)
            model.ClearMark();
        e.Handled = true;
    }
}
