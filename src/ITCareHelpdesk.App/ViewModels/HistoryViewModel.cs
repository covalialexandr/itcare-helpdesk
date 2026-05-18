using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ITCareHelpdesk.App.Models;
using ITCareHelpdesk.App.Services;

namespace ITCareHelpdesk.App.ViewModels;

public sealed partial class HistoryViewModel : ViewModelBase
{
    private readonly HistoryRepository _history;
    private readonly ToastService _toast;

    public ObservableCollection<HistoryEntry> Items { get; } = new();
    [ObservableProperty] private int _daysBack = 7;
    [ObservableProperty] private string? _ticketNumberFilter;

    public HistoryViewModel(HistoryRepository history, ToastService toast)
    {
        _history = history;
        _toast = toast;
        _ = LoadAsync();
    }

    partial void OnDaysBackChanged(int value) => _ = LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        BusyMessage = "Aducem istoricul...";
        try
        {
            Items.Clear();
            var data = await _history.GetAsync(clientId: null, tichetId: null, days: DaysBack);
            foreach (var e in data) Items.Add(e);
        }
        catch
        {
            _toast.ShowError("Eroare", "Nu am putut incarca istoricul.");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
