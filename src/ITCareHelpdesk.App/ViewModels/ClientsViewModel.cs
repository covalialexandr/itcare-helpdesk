using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ITCareHelpdesk.App.Models;
using ITCareHelpdesk.App.Services;

namespace ITCareHelpdesk.App.ViewModels;

// ============================================================
// ClientsViewModel
// ============================================================
// ViewModel pentru pagina "Clienti" — galeria cu toti clientii activi.
//
// Logica de filtrare: cum portofoliul tipic de clienti are zeci (nu mii), facem
// filtrarea CLIENT-SIDE — adica incarcam toata lista o data si filtrul "search" o
// reduce in memorie, fara round-trip la baza de date. Daca lista creste la 10.000+
// clienti, ar trebui sa mutam filtrarea SQL-side (sa apelam un SP cu parametri de
// cautare); momentan nu e cazul.
//
// State:
//   _all          - lista master (citita o data la load)
//   Items         - lista vizibila in UI (sub-set filtrat)
//   SearchText    - input-ul user-ului din toolbar
//   Selected      - clientul curent selectat (pentru viitor cand vom adauga detail panel)
//
// Comportament:
//   - OnSearchTextChanged se cheama automat (generat de [ObservableProperty]) la fiecare
//     keystroke; reaplica filtru pe nume, oras, industrie
//   - LoadAsync se cheama la constructor si la click pe "Refresh"
//   - In caz de eroare DB, se afiseaza un toast rosu si lista ramane goala
// ============================================================
public sealed partial class ClientsViewModel : ViewModelBase
{
    private readonly ClientRepository _clients;
    private readonly ToastService _toast;

    private System.Collections.Generic.List<Client> _all = new();

    public ObservableCollection<Client> Items { get; } = new();
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private Client? _selected;

    public ClientsViewModel(ClientRepository clients, ToastService toast)
    {
        _clients = clients;
        _toast = toast;
        _ = LoadAsync();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        BusyMessage = "Aducem clientii...";
        try
        {
            _all = await _clients.GetAllAsync();
            ApplyFilter();
        }
        catch
        {
            _toast.ShowError("Eroare", "Nu am putut incarca clientii.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilter()
    {
        Items.Clear();
        var q = _all.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var s = SearchText.Trim();
            q = q.Where(c =>
                c.NumeCompanie.Contains(s, System.StringComparison.OrdinalIgnoreCase) ||
                (c.Oras ?? "").Contains(s, System.StringComparison.OrdinalIgnoreCase) ||
                (c.Industrie ?? "").Contains(s, System.StringComparison.OrdinalIgnoreCase));
        }
        foreach (var c in q) Items.Add(c);
    }
}
