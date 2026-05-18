using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ITCareHelpdesk.App.Models;
using ITCareHelpdesk.App.Services;

namespace ITCareHelpdesk.App.ViewModels;

// Tichetele active sunt cele care opereaza zilnic — viewul nostru e o lista cu filtre.
// Filtrele se aplica client-side dupa ce am incarcat o singura data; tinem si master-list-ul (_all)
// pentru a putea reseta filtru fara round-trip nou la DB.
public sealed partial class TicketsViewModel : ViewModelBase
{
    private readonly TicketRepository _tickets;
    private readonly ToastService _toast;
    private readonly SessionService _session;

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

    public TicketsViewModel(TicketRepository tickets, ToastService toast, SessionService session)
    {
        _tickets = tickets;
        _toast = toast;
        _session = session;
        _ = LoadAsync();
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
