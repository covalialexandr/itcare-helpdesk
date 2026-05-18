using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ITCareHelpdesk.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace ITCareHelpdesk.App.Views;

public partial class TicketsView : UserControl
{
    public TicketsView()
    {
        InitializeComponent();
    }

    // Deschidem dialogul ca modal pe MainWindow (Owner = MainWindow). Asteptam inchiderea
    // si daca s-a creat un tichet, refresh la lista.
    private async void OnCreateTicketClick(object? sender, RoutedEventArgs e)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop
            || desktop.MainWindow is null
            || DataContext is not TicketsViewModel vm)
            return;

        var dialog = new CreateTicketWindow
        {
            DataContext = App.Services.GetRequiredService<CreateTicketViewModel>()
        };

        // ShowDialog blocheaza UI-ul pana la inchidere — exact ce vrem la modal.
        await dialog.ShowDialog(desktop.MainWindow);

        if (dialog.CreatedTicketId is int id)
            await vm.OnTicketCreated(id);
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
