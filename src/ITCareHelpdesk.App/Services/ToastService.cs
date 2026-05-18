using System;
using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ITCareHelpdesk.App.Services;

public enum ToastKind { Info, Success, Warning, Error, Otp }

public partial class Toast : ObservableObject
{
    public ToastKind Kind { get; init; }
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _message = "";
    [ObservableProperty] private bool _isVisible = true;
    public Guid Id { get; } = Guid.NewGuid();
}

// Toast-urile traiesc intr-o coada observata de MainWindow / LoginWindow.
// Le ascundem dupa N secunde printr-un Timer pe UI thread (Dispatcher).
// Am evitat Snackbar from Material — voiam control complet pe animatie si stack-uire.
public sealed class ToastService
{
    public ObservableCollection<Toast> Toasts { get; } = new();

    public void ShowInfo(string title, string message) => Show(new Toast { Kind = ToastKind.Info, Title = title, Message = message });
    public void ShowSuccess(string title, string message) => Show(new Toast { Kind = ToastKind.Success, Title = title, Message = message });
    public void ShowWarning(string title, string message) => Show(new Toast { Kind = ToastKind.Warning, Title = title, Message = message });
    public void ShowError(string title, string message) => Show(new Toast { Kind = ToastKind.Error, Title = title, Message = message });

    // Toast special pentru OTP simulat — ramane mai mult timp si are stil distinct
    public void ShowOtp(string code, string identifier)
    {
        var toast = new Toast
        {
            Kind = ToastKind.Otp,
            Title = "Cod OTP simulat",
            Message = $"Cod pentru {identifier}: {code}\n(in productie ar fi trimis pe SMS/email)"
        };
        Show(toast, durationSeconds: 30);
    }

    private void Show(Toast toast, int durationSeconds = 5)
    {
        Dispatcher.UIThread.Post(() => Toasts.Add(toast));

        // Folosim DispatcherTimer ca sa fim siguri ca remove-ul se intampla pe UI thread
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(durationSeconds) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            toast.IsVisible = false;
            // mic delay ca animatia de fade-out sa aiba timp sa termine
            var cleanup = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            cleanup.Tick += (_, _) =>
            {
                cleanup.Stop();
                Toasts.Remove(toast);
            };
            cleanup.Start();
        };
        timer.Start();
    }
}
