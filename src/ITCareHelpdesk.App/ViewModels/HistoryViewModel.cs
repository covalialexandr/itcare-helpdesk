using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ITCareHelpdesk.App.Models;
using ITCareHelpdesk.App.Services;

namespace ITCareHelpdesk.App.ViewModels;

// ============================================================
// HistoryViewModel
// ============================================================
// ViewModel pentru pagina "Istoric" — timeline cronologic al activitatii pe tichete.
// Vede toate comentariile, schimbarile de status, asignarile facute in intervalul ales.
//
// Filtru pe interval — 7, 14, sau 30 de zile inapoi. La schimbarea valorii DaysBack,
// se re-incarca lista automat (hook-ul OnDaysBackChanged generat de [ObservableProperty]).
// Asta ofera feedback instant la apasarea butoanelor "7 zile / 14 / 30" din UI.
//
// Datele vin de la procedura sp_GetIstoricActivitate care formateaza data ca string
// romanesc — UI-ul nu trebuie sa stie de formatare data.
//
// Filtre suplimentare (pe client sau pe un anumit tichet) sunt suportate la nivel de
// repository, dar UI-ul curent doar foloseste filtrul de zile. Putem expune si celelalte
// daca avem timp pentru un toolbar mai bogat.
// ============================================================
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
