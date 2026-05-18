using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ITCareHelpdesk.App.Models;
using ITCareHelpdesk.App.Services;

namespace ITCareHelpdesk.App.ViewModels;

// Dashboard-ul incarca paralel KPI-uri + top tehnicieni + tichete critice + categorii + charts.
// Fiecare apel are propriul try/catch ca un singur SP rupt sa NU blanchete toata pagina.
// Trade-off: pagina poate sa fie partial populata daca un SP cade, dar e infinit mai bine decat
// "totul e 0" cu zero indiciu in UI.
public sealed partial class DashboardViewModel : ViewModelBase
{
    private readonly StatsRepository _stats;
    private readonly TicketRepository _tickets;
    private readonly ToastService _toast;

    [ObservableProperty] private DashboardKpi _kpi = new(0, 0, 0, 0, 0, 0);
    public ObservableCollection<Technician> TopTehnicieni { get; } = new();
    public ObservableCollection<Ticket> TicheteCritice { get; } = new();
    public ObservableCollection<CategoryStat> CategoriiStats { get; } = new();
    public ObservableCollection<StatusBucket> StatusDistribution { get; } = new();
    public ObservableCollection<DailyResolved> DailyResolvedData { get; } = new();

    public DashboardViewModel(StatsRepository stats, TicketRepository tickets, ToastService toast)
    {
        _stats = stats;
        _tickets = tickets;
        _toast = toast;
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        BusyMessage = "Sincronizam date...";

        // Le rulam paralel dar fiecare cu propriul handler ca o singura cadere
        // sa nu afecteze restul. Tot Task.WhenAll dar acum cu wrap individual.
        await Task.WhenAll(
            SafeLoadKpiAsync(),
            SafeLoadTopAsync(),
            SafeLoadCriticalAsync(),
            SafeLoadCategoriesAsync(),
            SafeLoadStatusDistAsync(),
            SafeLoadDailyResolvedAsync()
        );

        IsBusy = false;
    }

    private async Task SafeLoadKpiAsync()
    {
        try { Kpi = await _stats.GetKpiAsync(); }
        catch (Exception ex) { _toast.ShowError("KPI", ex.Message); }
    }

    private async Task SafeLoadTopAsync()
    {
        try
        {
            var data = await _stats.GetTopTechniciansAsync(5);
            TopTehnicieni.Clear();
            foreach (var t in data) TopTehnicieni.Add(t);
        }
        catch (Exception ex) { _toast.ShowError("Top tehnicieni", ex.Message); }
    }

    private async Task SafeLoadCriticalAsync()
    {
        try
        {
            var data = await _tickets.GetCriticalAsync(4);
            TicheteCritice.Clear();
            foreach (var t in data) TicheteCritice.Add(t);
        }
        catch (Exception ex) { _toast.ShowError("Tichete critice", ex.Message); }
    }

    private async Task SafeLoadCategoriesAsync()
    {
        try
        {
            var data = await _stats.GetCategoryStatsAsync();
            CategoriiStats.Clear();
            foreach (var c in data) CategoriiStats.Add(c);
        }
        catch (Exception ex) { _toast.ShowError("Categorii", ex.Message); }
    }

    private async Task SafeLoadStatusDistAsync()
    {
        try
        {
            var data = await _stats.GetStatusDistributionAsync();
            StatusDistribution.Clear();
            foreach (var s in data) StatusDistribution.Add(s);
        }
        catch (Exception ex) { _toast.ShowError("Distributie status", ex.Message); }
    }

    private async Task SafeLoadDailyResolvedAsync()
    {
        try
        {
            var data = await _stats.GetResolvedByDayAsync(7);
            DailyResolvedData.Clear();
            foreach (var d in data) DailyResolvedData.Add(d);
        }
        catch (Exception ex) { _toast.ShowError("Rezolvate pe zi", ex.Message); }
    }
}
