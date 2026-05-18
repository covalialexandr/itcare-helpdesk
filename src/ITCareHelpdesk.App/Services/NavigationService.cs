using System;
using CommunityToolkit.Mvvm.ComponentModel;
using ITCareHelpdesk.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace ITCareHelpdesk.App.Services;

public enum AppPage
{
    Dashboard,
    Tickets,
    Clients,
    Technicians,
    Assets,
    Reports,
    History
}

// Centralizam navigarea ca MainWindow sa nu cunoasca toate ViewModels-urile.
// Schimbarea paginii e doar o pereche (enum, factory) — usor de extins.
public sealed partial class NavigationService : ObservableObject
{
    private readonly IServiceProvider _services;

    [ObservableProperty] private ViewModelBase? _currentView;
    [ObservableProperty] private AppPage _currentPage = AppPage.Dashboard;

    public NavigationService(IServiceProvider services) => _services = services;

    public void NavigateTo(AppPage page)
    {
        CurrentPage = page;
        // Folosim ServiceProvider ca sa cerem ViewModel-ul transient — primim instanta noua de
        // fiecare data, deci paginile incep "curate" la fiecare vizita.
        CurrentView = page switch
        {
            AppPage.Dashboard   => _services.GetRequiredService<DashboardViewModel>(),
            AppPage.Tickets     => _services.GetRequiredService<TicketsViewModel>(),
            AppPage.Clients     => _services.GetRequiredService<ClientsViewModel>(),
            AppPage.Technicians => _services.GetRequiredService<TechniciansViewModel>(),
            AppPage.Assets      => _services.GetRequiredService<AssetsViewModel>(),
            AppPage.Reports     => _services.GetRequiredService<ReportsViewModel>(),
            AppPage.History     => _services.GetRequiredService<HistoryViewModel>(),
            _                   => _services.GetRequiredService<DashboardViewModel>()
        };
    }
}
