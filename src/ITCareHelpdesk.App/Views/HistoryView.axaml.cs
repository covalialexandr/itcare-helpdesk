using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ITCareHelpdesk.App.ViewModels;

namespace ITCareHelpdesk.App.Views;

// ============================================================
// HistoryView.axaml.cs (code-behind)
// ============================================================
// Code-behind pentru pagina "Istoric" — timeline cronologic cu linia verticala si puncte cyan.
// Aici am pus un singur handler de UI: OnDaysClick, pentru segmented control-ul cu "7 / 14 / 30 zile".
//
// De ce un handler in code-behind si nu un Command pe ViewModel?
// Butonul transmite valoarea sa (Tag-ul) ca parametru. Pentru o singura comanda cu 3 butoane
// distincte, este mai simplu sa avem un handler care citeste Tag-ul si seteaza VM.DaysBack
// direct, decat sa cream un CommandParameter complex pentru fiecare buton.
// ============================================================
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
