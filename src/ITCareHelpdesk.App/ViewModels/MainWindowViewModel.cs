using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ITCareHelpdesk.App.Services;

namespace ITCareHelpdesk.App.ViewModels;

// MainWindowViewModel orchestreaza shell-ul: sidebar nav + content host + user pill.
// Expune NavigationService direct ca View-ul sa poata observa CurrentView prin binding.
public sealed partial class MainWindowViewModel : ViewModelBase
{
    public NavigationService Nav { get; }
    public SessionService Session { get; }
    public ToastService Toasts { get; }

    [ObservableProperty] private string _searchQuery = "";

    public ObservableCollection<NavItem> NavItems { get; }

    public MainWindowViewModel(NavigationService nav, SessionService session, ToastService toasts)
    {
        Nav = nav;
        Session = session;
        Toasts = toasts;

        // Tinem mapping-ul aici in loc de XAML ca sa avem o sursa unica de adevar:
        // titlu + glyph + cheia paginii. Daca adaugam o pagina noua, schimbam un singur loc.
        NavItems = new ObservableCollection<NavItem>
        {
            new("Dashboard",      "▣", AppPage.Dashboard),
            new("Tichete",        "❖", AppPage.Tickets),
            new("Clienti",        "◉", AppPage.Clients),
            new("Tehnicieni",     "◈", AppPage.Technicians),
            new("Asset-uri",      "◊", AppPage.Assets),
            new("Rapoarte",       "▤", AppPage.Reports),
            new("Istoric",        "◷", AppPage.History),
        };

        // Aterizam pe Dashboard la pornire — orice altceva e contraintuitiv pentru un operator.
        Nav.NavigateTo(AppPage.Dashboard);
    }

    [RelayCommand]
    private void NavigateTo(NavItem? item)
    {
        if (item is null) return;
        Nav.NavigateTo(item.Page);
    }

    [RelayCommand]
    private void SignOut()
    {
        // Sesiunea piere, dar fereastra principala ramane sa fie inchisa de App
        // (sau redirectata catre Login). Aici doar curatam state-ul.
        Session.SignOut();
        Toasts.ShowInfo("Sesiune incheiata", "Te-ai delogat. Pe data viitoare!");
    }
}

// NavItem-ul este minimal: titlu, glyph si pagina destinatie.
// Glyph-urile sunt din blocul Unicode "Geometric Shapes" — evitam dependinta de un font de iconite.
public sealed record NavItem(string Title, string Glyph, AppPage Page);
