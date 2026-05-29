using System.Collections.ObjectModel; // colectii care actualizeaza automat UI
using System.Linq; // functii LINQ (Where, Select etc.)
using System.Threading.Tasks; // suport async/await
using CommunityToolkit.Mvvm.ComponentModel; // proprietati generate automat
using CommunityToolkit.Mvvm.Input; // comenzi generate automat
using ITCareHelpdesk.App.Models; // modele aplicatie
using ITCareHelpdesk.App.Services; // servicii reutilizabile

namespace ITCareHelpdesk.App.ViewModels;

// ============================================================
// AssetsViewModel
// ============================================================
// ViewModel pentru pagina "Asset-uri" — inventarul echipamentelor IT al clientilor.
//
// Filtrul de tip echipament este DINAMIC: dropdown-ul se populeaza din valorile distincte
// existente in baza la load. Daca apare un tip nou de asset in DB (ex. "Drona"), apare
// automat in dropdown la urmatorul refresh. NU hardcodam tipurile in cod.
//
// Filtrarea (atat pe tip cat si pe cautare text liber) se face CLIENT-SIDE in memoria
// aplicatiei — _all retine lista master, Items contine sub-setul afisat. Search match-uieste
// pe cod_asset, denumire, client si producator deodata.
//
// Selected nu este folosit momentan pentru navigare; pe viitor poate deschide un detail
// panel similar cu cel de tichete.
// ============================================================

public sealed partial class AssetsViewModel : ViewModelBase
{
    private readonly AssetRepository _assets; // acces date asset-uri din DB
    private readonly ToastService _toast; // afisare notificari
    private System.Collections.Generic.List<Asset> _all = new(); // lista completa din baza

    public ObservableCollection<Asset> Items { get; } = new(); // lista afisata in UI
    public ObservableCollection<string> Types { get; } = new() { "Toate" }; // optiuni dropdown

    [ObservableProperty] private string _selectedType = "Toate"; // tip selectat
    [ObservableProperty] private string _searchText = ""; // text introdus la cautare
    [ObservableProperty] private Asset? _selected; // asset selectat

    public AssetsViewModel(AssetRepository assets, ToastService toast)
    {
        _assets = assets; // salvam repository
        _toast = toast; // salvam serviciu notificari
        _ = LoadAsync(); // incarcare automata la pornire
    }

    partial void OnSelectedTypeChanged(string value) => ApplyFilter(); // reaplica filtrul
    partial void OnSearchTextChanged(string value)   => ApplyFilter(); // cautare live

    [RelayCommand] // genereaza automat LoadCommand
    private async Task LoadAsync()
    {
        IsBusy = true; // porneste loader
        BusyMessage = "Aducem asset-urile..."; // mesaj incarcare

        try
        {
            _all = await _assets.GetAllAsync(); // luam toate datele

            // Populam optiunile de tip din date — nu le hardcodam ca pot aparea tipuri noi
            var types = _all.Select(a => a.Tip).Distinct().OrderBy(t => t).ToList(); // tipuri unice sortate

            Types.Clear(); // golim lista
            Types.Add("Toate"); // optiune default

            foreach (var t in types) Types.Add(t); // adaugam tipurile gasite

            ApplyFilter(); // aplicam filtre
        }
        catch
        {
            _toast.ShowError("Eroare", "Nu am putut incarca asset-urile."); // mesaj eroare
        }
        finally
        {
            IsBusy = false; // oprim loader
        }
    }

    private void ApplyFilter()
    {
        var q = _all.AsEnumerable(); // pornim din lista completa

        if (SelectedType != "Toate")
            q = q.Where(a => a.Tip == SelectedType); // filtru dupa tip

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var s = SearchText.Trim(); // elimina spatii

            q = q.Where(a =>
                a.CodAsset.Contains(s, System.StringComparison.OrdinalIgnoreCase) || // cauta cod
                a.Denumire.Contains(s, System.StringComparison.OrdinalIgnoreCase) || // cauta denumire
                a.Client.Contains(s, System.StringComparison.OrdinalIgnoreCase) || // cauta client
                (a.Producator ?? "").Contains(s, System.StringComparison.OrdinalIgnoreCase)); // cauta producator
        }

        Items.Clear(); // reset rezultate

        foreach (var a in q) Items.Add(a); // adauga rezultate filtrate
    }
}