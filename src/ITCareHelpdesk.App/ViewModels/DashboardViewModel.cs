using System; // tipuri de baza si exceptii
using System.Collections.ObjectModel; // colectii care actualizeaza UI automat
using System.Threading.Tasks; // suport async/await
using CommunityToolkit.Mvvm.ComponentModel; // proprietati generate automat
using CommunityToolkit.Mvvm.Input; // comenzi generate automat
using ITCareHelpdesk.App.Models; // modele aplicatie
using ITCareHelpdesk.App.Services; // servicii aplicatie
using System.Linq;

namespace ITCareHelpdesk.App.ViewModels;

// Dashboard-ul incarca paralel KPI-uri + top tehnicieni + tichete critice + categorii + charts.
// Fiecare apel are propriul try/catch ca un singur SP rupt sa NU blanchete toata pagina.
// Trade-off: pagina poate sa fie partial populata daca un SP cade, dar e infinit mai bine decat
// "totul e 0" cu zero indiciu in UI.

public sealed partial class DashboardViewModel : ViewModelBase
{
    private readonly StatsRepository _stats; // acces statistici dashboard
    private readonly TicketRepository _tickets; // acces tichete
    private readonly ToastService _toast; // afisare notificari

    [ObservableProperty] private DashboardKpi _kpi = new(0, 0, 0, 0, 0, 0); // valori KPI initiale

    public ObservableCollection<Technician> TopTehnicieni { get; } = new(); // top tehnicieni
    public ObservableCollection<Ticket> TicheteCritice { get; } = new(); // tichete urgente
    public ObservableCollection<CategoryStat> CategoriiStats { get; } = new(); // statistici categorii
    public ObservableCollection<StatusBucket> StatusDistribution { get; } = new(); // distributie statusuri
    public ObservableCollection<DailyResolved> DailyResolvedData { get; } = new(); // rezolvate pe zile

    // Heatmap NU foloseste ObservableCollection: HeatmapChart-ul are AffectsRender pe Values,
    // care detecteaza doar SCHIMBARI de referinta (nu Clear+Add pe colectia existenta).
    // Solutia: tinem un array si-l reasignam la fiecare load — binding-ul vede o referinta noua
    // si re-deseneaza automat.
    [ObservableProperty] private HeatmapCell[] _heatmapData = Array.Empty<HeatmapCell>();

    public DashboardViewModel(StatsRepository stats, TicketRepository tickets, ToastService toast)
    {
        _stats = stats; // salvam repository statistici
        _tickets = tickets; // salvam repository tichete
        _toast = toast; // salvam serviciu notificari

        _ = LoadAsync(); // incarcare automata la pornire
    }

    [RelayCommand] // genereaza LoadCommand
    private async Task LoadAsync()
    {
        IsBusy = true; // porneste loader
        BusyMessage = "Sincronizam date..."; // mesaj incarcare

        // Le rulam paralel dar fiecare cu propriul handler ca o singura cadere
        // sa nu afecteze restul. Tot Task.WhenAll dar acum cu wrap individual.

        await Task.WhenAll(
            SafeLoadKpiAsync(), // incarca KPI
            SafeLoadTopAsync(), // incarca top tehnicieni
            SafeLoadCriticalAsync(), // incarca urgente
            SafeLoadCategoriesAsync(), // incarca categorii
            SafeLoadStatusDistAsync(), // incarca distributie
            SafeLoadDailyResolvedAsync(), // incarca istoric
            SafeLoadHeatmapAsync() // incarca heatmap
        );

        IsBusy = false; // opreste loader
    }

    private async Task SafeLoadKpiAsync()
    {
        try
        {
            Kpi = await _stats.GetKpiAsync(); // citeste KPI
        }
        catch (Exception ex)
        {
            _toast.ShowError("KPI", ex.Message); // eroare KPI
        }
    }

    private async Task SafeLoadTopAsync()
    {
        try
        {
            var data = await _stats.GetTopTechniciansAsync(5); // primii 5 tehnicieni

            TopTehnicieni.Clear(); // goleste lista veche

            foreach (var t in data)
                TopTehnicieni.Add(t); // adauga rezultate
        }
        catch (Exception ex)
        {
            _toast.ShowError("Top tehnicieni", ex.Message); // mesaj eroare
        }
    }

    private async Task SafeLoadCriticalAsync()
    {
        try
        {
            var data = await _tickets.GetCriticalAsync(4); // top 4 urgente

            TicheteCritice.Clear(); // reset lista

            foreach (var t in data)
                TicheteCritice.Add(t); // adauga tichet
        }
        catch (Exception ex)
        {
            _toast.ShowError("Tichete critice", ex.Message); // eroare
        }
    }

    private async Task SafeLoadCategoriesAsync()
    {
        try
        {
            var data = await _stats.GetCategoryStatsAsync(); // statistici categorii

            CategoriiStats.Clear(); // reset lista

            foreach (var c in data)
                CategoriiStats.Add(c); // adauga categorie
        }
        catch (Exception ex)
        {
            _toast.ShowError("Categorii", ex.Message); // mesaj eroare
        }
    }

    private async Task SafeLoadStatusDistAsync()
    {
        try
        {
            var data = await _stats.GetStatusDistributionAsync(); // distributie status

            StatusDistribution.Clear(); // reset date

            foreach (var s in data)
                StatusDistribution.Add(s); // adauga element
        }
        catch (Exception ex)
        {
            _toast.ShowError("Distributie status", ex.Message); // eroare
        }
    }

    private async Task SafeLoadDailyResolvedAsync()
    {
        try
        {
            var data = await _stats.GetResolvedByDayAsync(7); // ultimele 7 zile

            DailyResolvedData.Clear(); // sterge lista veche

            foreach (var d in data)
                DailyResolvedData.Add(d); // adauga zi
        }
        catch (Exception ex)
        {
            _toast.ShowError("Rezolvate pe zi", ex.Message); // eroare
        }
    }

    private async Task SafeLoadHeatmapAsync()
    {
        try
        {
            var data = await _stats.GetHeatmapAsync(); // date heatmap

            // Convertim lista in array pentru binding
            HeatmapData = data.ToArray();
        }
        catch (Exception ex)
        {
            _toast.ShowError("Heatmap", ex.Message); // eroare heatmap
        }
    }
}