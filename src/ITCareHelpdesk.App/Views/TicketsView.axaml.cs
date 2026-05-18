using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ITCareHelpdesk.App.Models;
using ITCareHelpdesk.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace ITCareHelpdesk.App.Views;

// ============================================================
// TicketsView.axaml.cs (code-behind)
// ============================================================
// Pagina de gestiune tichete. Are MAI MULTE responsabilitati in code-behind decat o pagina
// tipica MVVM, pentru ca interactiunile sunt mai bogate:
//
//   1. OnTicketSelectionChanged — handler global pe SelectionChangedEvent. Cand userul da
//      click pe un rand de tichet in lista, deschidem drawer-ul de detalii.
//      L-am atasat global via AddHandler (in loc de a-l declara pe ListBox in XAML) pentru
//      ca XAML compiled bindings refuza uneori handler-uri de tip SelectionChanged pe ListBox
//      din motive de timing. AddHandler in code-behind e mai robust.
//
//   2. OnBackdropClick — cand userul da click pe zona intunecata din spatele drawer-ului,
//      drawer-ul se inchide. Pattern UX clasic pentru modale.
//
//   3. OnCreateTicketClick — deschide CreateTicketWindow ca modal. Asteptam inchiderea
//      modalului si daca s-a creat un tichet, refresh la lista (vm.OnTicketCreated).
//
// De ce nu sunt totul in ViewModel? Pentru ca aceste handlere au nevoie de:
//   - Acces la Application.Current.ApplicationLifetime (pentru a obtine MainWindow ca Owner)
//   - Acces la dependency injection container (App.Services.GetRequiredService<...>)
//   - Acces la dialogul ShowDialog() — un API direct UI
// Toate sunt INFRASTRUCTURE concerns, NU business logic — corect sa stea in code-behind.
// ============================================================
public partial class TicketsView : UserControl
{
    public TicketsView()
    {
        InitializeComponent();
    }

    // SelectedItem se schimba la click — deschidem drawer-ul.
    // Folosim ca handler pe SelectionChanged in XAML; il atasam via codebehind via FindControl
    // ca sa evitam un al doilea xmlns/handler in XAML.
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // Asculta selection-changed pe toate listbox-urile din view (avem unul singur de tichete)
        this.AddHandler(SelectingItemsControl.SelectionChangedEvent, OnTicketSelectionChanged);
    }

    private async void OnTicketSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not TicketsViewModel vm) return;
        if (e.AddedItems.Count == 0) return;
        if (e.AddedItems[0] is Ticket t)
            await vm.ShowDetailAsync(t);
    }

    // Click pe backdrop-ul drawer-ului inchide drawer-ul
    private void OnBackdropClick(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is TicketsViewModel vm)
            vm.Detail.CloseCommand.Execute(null);
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
