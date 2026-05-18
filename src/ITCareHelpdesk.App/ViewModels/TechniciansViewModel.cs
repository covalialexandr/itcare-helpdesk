using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ITCareHelpdesk.App.Models;
using ITCareHelpdesk.App.Services;

namespace ITCareHelpdesk.App.ViewModels;

// ============================================================
// TechniciansViewModel
// ============================================================
// ViewModel pentru pagina "Tehnicieni" — galerie de "trading cards" cu echipa IT.
// Fiecare card afiseaza: cod tehnician, nume, specializare, nivel (Junior/Mid/Senior/Lead),
// si trei metrici cheie (tichete rezolvate, active, timp mediu rezolvare) plus rating mediu.
//
// Datele vin din procedura SQL sp_GetStatisticiTehnicieni, care agrega LIVE pe tabela
// Tichete — adica daca un tehnician rezolva un tichet acum, refresh la pagina arata
// numarul actualizat. Nu cache-uim agregarile (am putea pe viitor pentru performanta).
//
// State minim — doar lista si selectia. Filtrele de tip "doar active" sau "ordonare
// pe rating" se pot adauga ulterior cu impact minim pe arhitectura.
//
// Eroarea de DB se inghite cu un toast — pagina ramane goala, dar restul aplicatiei nu
// pica. Comportament fail-safe.
// ============================================================
public sealed partial class TechniciansViewModel : ViewModelBase
{
    private readonly StatsRepository _stats;
    private readonly ToastService _toast;

    public ObservableCollection<Technician> Items { get; } = new();
    [ObservableProperty] private Technician? _selected;

    public TechniciansViewModel(StatsRepository stats, ToastService toast)
    {
        _stats = stats;
        _toast = toast;
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        BusyMessage = "Aducem statistici tehnicieni...";
        try
        {
            Items.Clear();
            foreach (var t in await _stats.GetTechniciansAsync()) Items.Add(t);
        }
        catch
        {
            _toast.ShowError("Eroare", "Nu am putut incarca echipa.");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
