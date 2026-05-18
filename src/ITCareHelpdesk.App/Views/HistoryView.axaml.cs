using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ITCareHelpdesk.App.ViewModels;

namespace ITCareHelpdesk.App.Views;

public partial class HistoryView : UserControl
{
    public HistoryView()
    {
        InitializeComponent();
    }

    private void OnDaysClick(object? sender, RoutedEventArgs e)
    {
        // Tag-ul butonului tine numarul de zile ca string in XAML; il parsam si setam in VM.
        if (sender is Button { Tag: string s } && int.TryParse(s, out var days) && DataContext is HistoryViewModel vm)
            vm.DaysBack = days;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
