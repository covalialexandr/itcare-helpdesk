using System;
using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ITCareHelpdesk.App.Services;

// Tipurile de notificari suportate de aplicatie.
// Le folosim pentru stilizare diferita:
// culori, iconite, durata...
//
public enum ToastKind
{
    Info,
    Success,
    Warning,
    Error,
    Otp
}

// Model simplu pentru un toast afisat in UI.
// ObservableObject permite actualizare automata in interfata
// cand proprietatile isi schimba valoarea.
public partial class Toast : ObservableObject
{
    // Tipul notificarii (success/error/etc)
    public ToastKind Kind { get; init; }

    // Titlul toast-ului
    [ObservableProperty]
    private string _title = "";

    // Mesajul principal afisat utilizatorului
    [ObservableProperty]
    private string _message = "";

    // Folosit pentru animatia de fade-out.
    // Cand devine false -> UI poate porni animatia de disparitie.
    [ObservableProperty]
    private bool _isVisible = true;

    // ID unic pentru fiecare toast.
    // Ajuta la identificare in lista si evita conflicte.
    public Guid Id { get; } = Guid.NewGuid();
}

// Service global pentru gestionarea notificarilor toast.
//
// Am ales un serviciu dedicat in loc sa afisam MessageBox-uri,
// deoarece toast-urile sunt:
// - mai moderne
// - non-blocking
// - mai rapide pentru UX
//
// ObservableCollection actualizeaza automat UI-ul
// cand adaugam sau eliminam notificari.
public sealed class ToastService
{
    // Lista observabila pe care UI-ul o asculta.
    // MainWindow/LoginWindow fac binding la aceasta colectie.
    public ObservableCollection<Toast> Toasts { get; } = new();

    // Metode helper pentru fiecare tip de notificare
    // in acest mod evitam repetarea codului prin aplicatie.
    public void ShowInfo(string title, string message) =>
        Show(new Toast
        {
            Kind = ToastKind.Info,
            Title = title,
            Message = message
        });

    public void ShowSuccess(string title, string message) =>
        Show(new Toast
        {
            Kind = ToastKind.Success,
            Title = title,
            Message = message
        });

    public void ShowWarning(string title, string message) =>
        Show(new Toast
        {
            Kind = ToastKind.Warning,
            Title = title,
            Message = message
        });

    public void ShowError(string title, string message) =>
        Show(new Toast
        {
            Kind = ToastKind.Error,
            Title = title,
            Message = message
        });

    // Toast special pentru OTP.
    //
    // In aplicatia reala codul ar fi trimis prin:
    // - SMS
    // - email
    // - autentificator
    //
    // Pentru demo/simularea proiectului il afisam direct in UI.
    // Durata este mai mare deoarece utilizatorul trebuie
    // sa aiba timp sa citeasca si sa introduca codul.
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

    // Metoda centrala care afiseaza toast-ul.
    // Toate notificarile trec prin aici.
    private void Show(Toast toast, int durationSeconds = 5)
    {
        // Adaugam toast-ul pe UI thread.
        // Important deoarece ObservableCollection este legata de interfata.
        Dispatcher.UIThread.Post(() =>
            Toasts.Add(toast));

        // Timer pentru auto-inchidere.
        //
        // DispatcherTimer este mai sigur pentru UI
        // decat System.Timers.Timer deoarece ruleaza direct
        // pe thread-ul interfetei grafice.
        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(durationSeconds)
        };

        timer.Tick += (_, _) =>
        {
            timer.Stop();

            // Declansam fade-out-ul.
            // UI-ul poate avea animatie pe IsVisible=false.
            toast.IsVisible = false;

            // Mic delay pentru ca animatia sa apuce sa ruleze.
            // Daca eliminam instant toast-ul, animatia nu se vede.
            var cleanup = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(400)
            };

            cleanup.Tick += (_, _) =>
            {
                cleanup.Stop();

                // Eliminam complet toast-ul din colectie.
                Toasts.Remove(toast);
            };

            cleanup.Start();
        };

        timer.Start();
    }
}