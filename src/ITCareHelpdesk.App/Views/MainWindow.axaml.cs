using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ITCareHelpdesk.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace ITCareHelpdesk.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Selectam primul element din nav la Loaded ca sa nu avem un "highlight gol" la pornire.
        // Folosim SelectedIndex dupa ce templated children sunt instantiati — altfel binding-ul rateaza.
        Loaded += (_, _) =>
        {
            if (this.FindControl<ListBox>("NavListBox") is { } list && list.Items.Count > 0)
                list.SelectedIndex = 0;
        };
    }

    // ============================================================
    // Sidebar navigation
    // ============================================================
    private void OnNavSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0 || DataContext is not MainWindowViewModel vm) return;
        if (e.AddedItems[0] is NavItem item)
            vm.NavigateToCommand.Execute(item);
    }

    // ============================================================
    // Sign-out: revine la LoginWindow fara restart
    // ============================================================
    private void OnSignOutClick(object? sender, RoutedEventArgs e)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return;

        var login = new LoginWindow
        {
            DataContext = App.Services.GetRequiredService<LoginViewModel>()
        };
        desktop.MainWindow = login;
        login.Show();
        Close();
    }

    // ============================================================
    // Custom window chrome
    // ============================================================
    private void OnDragChromeAreaPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            // Double-click pe drag area = maximizare/restaurare. Cuvant-cheie: ClickCount.
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
                return;
            }
            BeginMoveDrag(e);
        }
    }

    private void OnMinimizeClick(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximizeClick(object? sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
