using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ITCareHelpdesk.App.Models;
using ITCareHelpdesk.App.Services;

namespace ITCareHelpdesk.App.ViewModels;

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
