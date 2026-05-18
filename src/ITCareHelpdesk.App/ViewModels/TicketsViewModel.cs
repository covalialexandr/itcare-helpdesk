using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ITCareHelpdesk.App.Models;
using ITCareHelpdesk.App.Services;

namespace ITCareHelpdesk.App.ViewModels;

// ============================================================
// TicketsViewModel
// ============================================================
// ViewModel-ul paginii principale de operatiuni: lista cu toate tichetele active.
// Cea mai complexa pagina, contine: filtre multiple, integrare cu CreateTicket modal,
// integrare cu Detail drawer, comanda de inchidere rapida.
//
// CONCEPTE CHEIE:
//
// 1. Lista master (_all) + lista filtrata (Items)
//    Citim toate tichetele active o data din baza de date. Filtrele utilizatorului
//    (prioritate, status, search text, "doar ale mele") se aplica IN MEMORIE asupra
//    listei master. Asa schimbarile de filtru sunt instant — fara latenta de retea.
//
// 2. Notification chains
//    [ObservableProperty] genereaza automat metode partial void OnXChanged la fiecare
//    proprietate. Le folosim ca sa re-aplicam filtrele cand userul tasteaza sau alege
//    din dropdown.
//
// 3. Drawer integration
//    Cand userul da click pe un rand, ShowDetailAsync deschide drawer-ul lateral. Drawer-ul
//    este SINGLETON in DI (un singur drawer in toata aplicatia), iar TicketsViewModel
//    primeste o referinta la el prin Detail. Asculta evenimentul TicketChanged ca sa-si
//    refresheze lista cand drawer-ul anunta o modificare (comentariu nou, inchidere).
//
// 4. Volume de tichete intarziate
//    OverdueCount se calculeaza la load — este metrica afisata in header. Util pentru
//    operator sa vada rapid daca echipa ramane in urma cu SLA-urile.
// ============================================================
public sealed partial class TicketsViewModel : ViewModelBase
{
    private readonly TicketRepository _tickets;
    private readonly ToastService _toast;
    private readonly SessionService _session;

    // Drawer-ul de detalii este shared (singleton din DI). View-ul nostru il "imprumuta" si
    // ii expune comenzile via Detail.* binding.
    public TicketDetailViewModel Detail { get; }

    private List<Ticket> _all = new();

    public ObservableCollection<Ticket> Items { get; } = new();
    public ObservableCollection<string> Priorities { get; } = new() { "Toate", "CRITICAL", "HIGH", "MEDIUM", "LOW" };
    public ObservableCollection<string> Statuses   { get; } = new() { "Toate", "OPEN", "IN_PROGRESS", "PENDING", "RESOLVED", "CLOSED" };

    [ObservableProperty] private string _selectedPriority = "Toate";
    [ObservableProperty] private string _selectedStatus = "Toate";
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private bool _showOnlyMine;
    [ObservableProperty] private Ticket? _selected;

    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private int _overdueCount;

    public TicketsViewModel(TicketRepository tickets, ToastService toast, SessionService session,
                            TicketDetailViewModel detail)
    {
        _tickets = tickets;
        _toast = toast;
        _session = session;
        Detail = detail;
        // Cand drawer-ul anunta ca tichetul s-a schimbat (comentariu adaugat, inchis), refresh la lista
        Detail.TicketChanged += async (_, _) => await LoadAsync();
        _ = LoadAsync();
    }

    // Apelat din View cand utilizatorul da click pe un rand. Deschide drawer-ul.
    public async Task ShowDetailAsync(Ticket? ticket)
    {
        if (ticket is null) return;
        await Detail.OpenAsync(ticket.TichetId);
    }

    // Re-aplica filtrul cand una din proprietatile observate se schimba.
    // Folosim hookurile generate de [ObservableProperty] in loc sa scriem partial OnXChanged.
    partial void OnSelectedPriorityChanged(string value) => ApplyFilters();
    partial void OnSelectedStatusChanged(string value)   => ApplyFilters();
    partial void OnSearchTextChanged(string value)       => ApplyFilters();
    partial void OnShowOnlyMineChanged(bool value)       => ApplyFilters();

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        BusyMessage = "Aducem tichetele...";
        try
        {
            _all = await _tickets.GetActiveAsync();
            ApplyFilters();
            OverdueCount = _all.Count(t => t.SlaDepasit);
        }
        catch
        {
            _toast.ShowError("Eroare", "Nu am putut incarca tichetele.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilters()
    {
        IEnumerable<Ticket> q = _all;

        if (SelectedPriority != "Toate")
            q = q.Where(t => t.Prioritate.Equals(SelectedPriority, System.StringComparison.OrdinalIgnoreCase));

        if (SelectedStatus != "Toate")
            q = q.Where(t => t.Status.Equals(SelectedStatus, System.StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var s = SearchText.Trim();
            q = q.Where(t =>
                t.NumarTichet.Contains(s, System.StringComparison.OrdinalIgnoreCase) ||
                t.Titlu.Contains(s, System.StringComparison.OrdinalIgnoreCase) ||
                t.Client.Contains(s, System.StringComparison.OrdinalIgnoreCase));
        }

        // "Doar ale mele" — match pe numele complet al tehnicianului asignat.
        // Daca user-ul curent nu este tehnician (ex: admin), filtrul e no-op.
        if (ShowOnlyMine && _session.CurrentUser?.NumeComplet is { } myName)
            q = q.Where(t => t.Tehnician == myName);

        Items.Clear();
        foreach (var t in q) Items.Add(t);
        TotalCount = Items.Count;
    }

    [RelayCommand]
    private async Task CloseTicketAsync(Ticket? ticket)
    {
        if (ticket is null) return;
        // Inchidem cu valori implicite — UI mai elaborat (dialog cu rating + ore) ar fi next step.
        await _tickets.CloseTicketAsync(
            tichetId: ticket.TichetId,
            note: "Inchis din UI fara dialog suplimentar.",
            rating: null,
            oreLucrate: ticket.OreLucrate,
            inchisDe: _session.CurrentUser?.UserId);

        _toast.ShowSuccess("Tichet inchis", $"{ticket.NumarTichet} a fost inchis.");
        await LoadAsync();
    }

    // Re-incarca lista dupa ce dialogul de creare s-a inchis cu un id valid.
    // ViewModel-ul nu deschide direct fereastra (asta e responsabilitatea View-ului),
    // dar expune metoda care primeste rezultatul ca sa decuplam VM de Window.
    public async Task OnTicketCreated(int newTicketId)
    {
        if (newTicketId <= 0) return;
        await LoadAsync();
    }
}
