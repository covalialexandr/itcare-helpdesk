using System.Collections.ObjectModel; // colectii ce actualizeaza automat UI
using System.Linq; // functii LINQ
using System.Threading.Tasks; // suport async/await
using CommunityToolkit.Mvvm.ComponentModel; // proprietati automate
using CommunityToolkit.Mvvm.Input; // comenzi automate
using ITCareHelpdesk.App.Models; // modele aplicatie
using ITCareHelpdesk.App.Services; // servicii aplicatie

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
    private readonly ClientRepository _clients; // acces la date clienti
    private readonly ToastService _toast; // notificari eroare/info

    private System.Collections.Generic.List<Client> _all = new(); // lista completa din DB

    public ObservableCollection<Client> Items { get; } = new(); // lista afisata
    [ObservableProperty] private string _searchText = ""; // text cautare
    [ObservableProperty] private Client? _selected; // client selectat

    public ClientsViewModel(ClientRepository clients, ToastService toast)
    {
        _clients = clients; // salvam repository
        _toast = toast; // salvam serviciu toast
        _ = LoadAsync(); // incarcam automat datele
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter(); // cautare live

    [RelayCommand] // genereaza automat LoadCommand
    private async Task LoadAsync()
    {
        IsBusy = true; // porneste loader
        BusyMessage = "Aducem clientii..."; // mesaj incarcare

        try
        {
            _all = await _clients.GetAllAsync(); // citim clienti din DB

            ApplyFilter(); // aplicam filtrare
        }
        catch
        {
            _toast.ShowError("Eroare", "Nu am putut incarca clientii."); // mesaj eroare
        }
        finally
        {
            IsBusy = false; // oprim loader
        }
    }

    private void ApplyFilter()
    {
        Items.Clear(); // golim rezultatele anterioare

        var q = _all.AsEnumerable(); // pornim din lista completa

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var s = SearchText.Trim(); // elimina spatii inutile

            q = q.Where(c =>
                c.NumeCompanie.Contains(s, System.StringComparison.OrdinalIgnoreCase) || // cauta dupa nume
                (c.Oras ?? "").Contains(s, System.StringComparison.OrdinalIgnoreCase) || // cauta dupa oras
                (c.Industrie ?? "").Contains(s, System.StringComparison.OrdinalIgnoreCase)); // cauta dupa industrie
        }

        foreach (var c in q) Items.Add(c); // adauga rezultate filtrate
    }
}