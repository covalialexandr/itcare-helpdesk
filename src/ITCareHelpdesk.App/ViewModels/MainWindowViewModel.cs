using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ITCareHelpdesk.App.Services;

namespace ITCareHelpdesk.App.ViewModels;

// ============================================================
// MainWindowViewModel
// ============================================================
// ViewModel-ul "shell-ului" — fereastra principala dupa autentificare. Nu contine logica
// de business, ci orchestreaza:
//   1. Sidebar-ul cu cele 7 nav items (Dashboard, Tichete, Clienti, etc.)
//   2. Content host-ul care afiseaza pagina curenta
//   3. User-pill-ul din coltul stanga jos cu numele + rolul + buton sign-out
//   4. Search palette-ul din top bar (placeholder pentru viitor Ctrl+K)
//   5. Toast queue (notificarile colt dreapta-jos)
//
// EXPUNERE DE SERVICII direct ca proprietati:
//   Nav     -> NavigationService — schimba pagina curenta cand userul da click pe sidebar
//   Session -> SessionService — afiseaza userul logat in pill
//   Toasts  -> ToastService — coada de notificari
//
// Aceasta abordare permite XAML-ului sa faca binding direct: "{Binding Session.CurrentUser.NumeComplet}".
// Trade-off: ViewModel-ul devine usor "thin", dar in schimb pastram totul declarativ in XAML
// fara middleware-uri inutile.
//
// NavItem: record local cu Titlu + Glyph (caracter Unicode) + AppPage enum. Tinem mapping-ul
// aici in loc de XAML ca sa avem o sursa UNICA — daca adaugam o pagina noua, schimbam un singur loc.
// ============================================================
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
