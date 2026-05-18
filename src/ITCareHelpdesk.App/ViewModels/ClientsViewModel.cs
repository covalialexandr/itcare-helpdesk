using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ITCareHelpdesk.App.Models;
using ITCareHelpdesk.App.Services;

namespace ITCareHelpdesk.App.ViewModels;

// Clientii sunt liste mai mici (~zeci), deci nu ne batem capul cu virtualizare. Putem
// permite filtru live fara DB rountrip.
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
