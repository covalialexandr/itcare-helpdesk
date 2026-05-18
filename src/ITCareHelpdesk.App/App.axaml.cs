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

// ============================================================
// App.axaml.cs
// ============================================================
// Inima aplicatiei la pornire. Aici se intampla trei lucruri cruciale:
//
//   1. INITIALIZE — incarca tema (Dark) si paleta (Palette.axaml + DarkTheme.axaml + Controls.axaml)
//      Apelat de Avalonia la initializare; XAML parser-ul citeste App.axaml si construieste
//      structura de stiluri globale.
//
//   2. ConfigureServices — construieste containerul de Dependency Injection.
//      Aici se inregistreaza TOATE serviciile si toate ViewModels. La runtime, cand un VM
//      cere DatabaseService (de exemplu), containerul i-l furnizeaza din aceasta colectie.
//      Distinctia singleton/transient:
//         - SINGLETON  (servicii infrastructura: DB, Auth, Toast) — o singura instanta pe app
//         - TRANSIENT  (ViewModels) — instanta noua la fiecare cerere, ca state-ul sa fie "proaspat"
//                     la fiecare navigare
//
//   3. OnFrameworkInitializationCompleted — momentul cand UI-ul este gata sa primeasca o fereastra.
//      Aici deschidem SplashWindow ca prima fereastra (verifica DB-ul, apoi trece la Login).
//      ShutdownMode = OnLastWindowClose — daca splash-ul se inchide si Login-ul e deschis,
//      aplicatia ramane in viata.
//
// COMPROMIS DE DESIGN: containerul DI este expus ca proprietate STATICA (App.Services).
// E o practica oarecum controversata in mediile enterprise — dar pentru o aplicatie
// desktop monolitica de marime medie, e cel mai simplu mod de a-l accesa din locuri unde
// nu avem injection direct (ex. cand deschidem dinamic CreateTicketWindow dintr-un click handler).
// In schimb evitam un ServiceLocator pattern explicit, si totul ramane testabil prin substitution.
// ============================================================
public partial class App : Application
{
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
        services.AddSingleton<AiSuggestionService>();

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
        services.AddSingleton<TicketDetailViewModel>(); // singleton — un singur drawer in toata aplicatia

        Services = services.BuildServiceProvider();
    }
}
