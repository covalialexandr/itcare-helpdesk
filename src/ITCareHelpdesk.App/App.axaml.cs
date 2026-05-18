using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ITCareHelpdesk.App.Services;
using ITCareHelpdesk.App.ViewModels;
using ITCareHelpdesk.App.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITCareHelpdesk.App;

public partial class App : Application
{
    // DI container expus static — practica controversata, dar pentru o aplicatie desktop
    // monolitica de marime medie ramane cel mai simplu mod sa accesam serviciile
    // din ViewModels fara sa avem dependency injection complet pe ReactiveUI.
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        ConfigureServices();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // SplashWindow este primul lucru pe care il vede utilizatorul. ShutdownMode = OnLastWindowClose
            // ca sa nu inchidem aplicatia cand splash-ul dispare si ne mutam la login.
            desktop.ShutdownMode = ShutdownMode.OnLastWindowClose;

            var splash = new SplashWindow
            {
                DataContext = Services.GetRequiredService<SplashViewModel>()
            };
            desktop.MainWindow = splash;
            splash.Show();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var services = new ServiceCollection();

        services.AddSingleton<IConfiguration>(config);

        // Servicii infra — singleton pentru ca tin un connection pool intern
        services.AddSingleton<DatabaseService>();
        services.AddSingleton<AuthService>();
        services.AddSingleton<SessionService>();
        services.AddSingleton<OtpService>();
        services.AddSingleton<ReportService>();
        services.AddSingleton<NavigationService>();
        services.AddSingleton<ToastService>();

        // Repositori-uri pe entitate — singleton e ok pentru ca sunt stateless
        services.AddSingleton<TicketRepository>();
        services.AddSingleton<ClientRepository>();
        services.AddSingleton<StatsRepository>();
        services.AddSingleton<AssetRepository>();
        services.AddSingleton<HistoryRepository>();
        services.AddSingleton<CategoryRepository>();

        // ViewModels — transient ca sa avem state proaspat la fiecare navigare
        services.AddTransient<SplashViewModel>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<TicketsViewModel>();
        services.AddTransient<ClientsViewModel>();
        services.AddTransient<TechniciansViewModel>();
        services.AddTransient<AssetsViewModel>();
        services.AddTransient<ReportsViewModel>();
        services.AddTransient<HistoryViewModel>();
        services.AddTransient<CreateTicketViewModel>();

        Services = services.BuildServiceProvider();
    }
}
