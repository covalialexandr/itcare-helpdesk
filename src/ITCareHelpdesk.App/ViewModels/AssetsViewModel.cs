using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ITCareHelpdesk.App.Models;
using ITCareHelpdesk.App.Services;

namespace ITCareHelpdesk.App.ViewModels;

public sealed partial class AssetsViewModel : ViewModelBase
{
    private readonly AssetRepository _assets;
    private readonly ToastService _toast;
    private System.Collections.Generic.List<Asset> _all = new();

    public ObservableCollection<Asset> Items { get; } = new();
    public ObservableCollection<string> Types { get; } = new() { "Toate" };
    [ObservableProperty] private string _selectedType = "Toate";
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private Asset? _selected;

    public AssetsViewModel(AssetRepository assets, ToastService toast)
    {
        _assets = assets;
        _toast = toast;
        _ = LoadAsync();
    }

    partial void OnSelectedTypeChanged(string value) => ApplyFilter();
    partial void OnSearchTextChanged(string value)   => ApplyFilter();

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        BusyMessage = "Aducem asset-urile...";
        try
        {
            _all = await _assets.GetAllAsync();

            // Populam optiunile de tip din date — nu le hardcodam ca pot aparea tipuri noi
            var types = _all.Select(a => a.Tip).Distinct().OrderBy(t => t).ToList();
            Types.Clear();
            Types.Add("Toate");
            foreach (var t in types) Types.Add(t);

            ApplyFilter();
        }
        catch
        {
            _toast.ShowError("Eroare", "Nu am putut incarca asset-urile.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilter()
    {
        var q = _all.AsEnumerable();
        if (SelectedType != "Toate")
            q = q.Where(a => a.Tip == SelectedType);
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var s = SearchText.Trim();
            q = q.Where(a =>
                a.CodAsset.Contains(s, System.StringComparison.OrdinalIgnoreCase) ||
                a.Denumire.Contains(s, System.StringComparison.OrdinalIgnoreCase) ||
                a.Client.Contains(s, System.StringComparison.OrdinalIgnoreCase) ||
                (a.Producator ?? "").Contains(s, System.StringComparison.OrdinalIgnoreCase));
        }
        Items.Clear();
        foreach (var a in q) Items.Add(a);
    }
}
