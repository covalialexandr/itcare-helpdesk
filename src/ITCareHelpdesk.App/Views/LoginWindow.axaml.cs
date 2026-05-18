using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ITCareHelpdesk.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace ITCareHelpdesk.App.Views;

public partial class LoginWindow : Window
{
    // Pastram referintele la casutele OTP ca sa controlam focus-ul manual.
    // Alternativa cu KeyboardFocusManager este mai "by-the-book", dar in Avalonia 11.x
    // gestiunea focus-ului in controale dinamice (templated) este finicky.
    private TextBox?[] _otpBoxes = Array.Empty<TextBox?>();

    public LoginWindow()
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            // Cacheam casutele OTP dupa Loaded ca FindControl<T> sa returneze instantele reale.
            _otpBoxes = new[]
            {
                this.FindControl<TextBox>("OtpBox1"),
                this.FindControl<TextBox>("OtpBox2"),
                this.FindControl<TextBox>("OtpBox3"),
                this.FindControl<TextBox>("OtpBox4"),
                this.FindControl<TextBox>("OtpBox5"),
                this.FindControl<TextBox>("OtpBox6"),
            };

            // Atasam handler-ele pentru auto-advance si backspace-back
            for (var i = 0; i < _otpBoxes.Length; i++)
            {
                var idx = i;
                if (_otpBoxes[i] is { } box)
                {
                    box.AddHandler(TextBox.TextChangedEvent, (_, _) => OnOtpTextChanged(idx));
                    box.AddHandler(InputElement.KeyDownEvent, (_, e) => OnOtpKeyDown(idx, e), RoutingStrategies.Tunnel);
                }
            }

            // Asculta evenimentul de "login reusit" si treci la MainWindow
            if (DataContext is LoginViewModel vm)
                vm.SignedIn += OnSignedIn;
        };
    }

    // ============================================================
    // Tranzitie la MainWindow dupa autentificare cu succes
    // ============================================================
    private void OnSignedIn(object? sender, Models.AppUser user)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return;

        var main = new MainWindow
        {
            DataContext = App.Services.GetRequiredService<MainWindowViewModel>()
        };

        // Aratam noua fereastra mai intai si dupa o inchidem pe asta — pentru ca lifetime-ul
        // este OnLastWindowClose si nu vrem sa cada aplicatia in golul dintre ele.
        desktop.MainWindow = main;
        main.Show();
        Close();
    }

    // ============================================================
    // OTP UX — auto-advance & backspace-rewind
    // ============================================================
    private void OnOtpTextChanged(int index)
    {
        if (_otpBoxes[index] is not { } box) return;

        // Daca user-ul a lipit codul intreg in prima casuta, il distribuim
        if (index == 0 && box.Text is { Length: > 1 } pasted)
        {
            var digits = pasted.Trim();
            if (digits.Length >= 6 && DataContext is LoginViewModel vm)
            {
                vm.OtpD1 = digits[0].ToString();
                vm.OtpD2 = digits[1].ToString();
                vm.OtpD3 = digits[2].ToString();
                vm.OtpD4 = digits[3].ToString();
                vm.OtpD5 = digits[4].ToString();
                vm.OtpD6 = digits[5].ToString();
                _otpBoxes[5]?.Focus();
                return;
            }
        }

        // Auto-advance: o singura cifra introdusa => focus pe urmatoarea
        if (!string.IsNullOrEmpty(box.Text) && index < _otpBoxes.Length - 1)
            _otpBoxes[index + 1]?.Focus();
    }

    private void OnOtpKeyDown(int index, KeyEventArgs e)
    {
        if (e.Key == Key.Back && _otpBoxes[index] is { Text: null or "" } && index > 0)
        {
            // Backspace pe casuta goala => mergem pe casuta anterioara
            _otpBoxes[index - 1]?.Focus();
            e.Handled = true;
        }
    }

    // ============================================================
    // Window chrome custom (drag, minimize, close)
    // ============================================================
    private void OnDragChromeAreaPressed(object? sender, PointerPressedEventArgs e)
    {
        // Tragerea ferestrei prin zonele non-interactive — comportament natural pentru
        // ferestre borderless pe Windows. BeginMoveDrag returneaza imediat dupa ce
        // OS-ul preia controlul.
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void OnMinimizeClick(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
