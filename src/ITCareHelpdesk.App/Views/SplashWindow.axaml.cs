using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ITCareHelpdesk.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace ITCareHelpdesk.App.Views;

public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is SplashViewModel vm)
            {
                vm.Completed += OnCompleted;
                vm.Failed    += OnFailed;
                await vm.RunAsync();
            }
        };
    }

    private void OnCompleted(object? sender, EventArgs e)
    {
        // Dupa splash mutam fereastra principala la LoginWindow.
        // ATENTIE: ordinea conteaza — intai aratam login-ul, apoi inchidem splash-ul
        // ca aplicatia sa nu se considere "fara ferestre" si sa iasa.
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var login = new LoginWindow
            {
                DataContext = App.Services.GetRequiredService<LoginViewModel>()
            };
            desktop.MainWindow = login;
            login.Show();
            Close();
        }
    }

    private void OnFailed(object? sender, string error)
    {
        // doar logam pentru moment — UI-ul deja afiseaza eroarea
    }

    private void OnCloseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
