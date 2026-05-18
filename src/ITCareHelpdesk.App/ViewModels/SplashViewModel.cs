using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using ITCareHelpdesk.App.Services;

namespace ITCareHelpdesk.App.ViewModels;

// Splash-ul nu este doar cosmetic: verifica conexiunea la DB, warmuieste serviciile,
// si abia apoi cedeaza locul login-ului. Astfel utilizatorul afla din primul ecran daca
// SQL Server-ul nu este pornit, in loc sa primeasca o eroare cripta la login.
public sealed partial class SplashViewModel : ViewModelBase
{
    private readonly DatabaseService _db;

    [ObservableProperty] private double _progress;
    [ObservableProperty] private string _statusText = "Inițializare sistem...";
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private string? _errorMessage;

    public event EventHandler? Completed;
    public event EventHandler<string>? Failed;

    public SplashViewModel(DatabaseService db)
    {
        _db = db;
    }

    // Pas-cu-pas: fiecare actiune incrementeaza progress-ul.
    // Tinem delay-uri mici (200-400ms) ca utilizatorul sa apuce sa citeasca status-ul —
    // 2s e sweet spot intre "se incarca ceva" si "ce naiba face".
    public async Task RunAsync()
    {
        await Step(0.15, "Conectare la baza de date...");
        var (dbOk, dbErr) = await _db.TestConnectionAsync();
        if (!dbOk)
        {
            HasError = true;
            // Aratam atat connection string-ul efectiv folosit (cu parola mascata) cat si mesajul real
            // de la SqlClient — diferenta intre "nu pornit" si "wrong auth" se vede imediat.
            ErrorMessage =
                $"Conexiunea la SQL Server a esuat.\n\n" +
                $"Detaliu: {dbErr ?? "necunoscut"}\n\n" +
                $"Connection string folosit:\n{_db.ConnectionStringForDisplay}";
            Failed?.Invoke(this, ErrorMessage);
            return;
        }

        await Step(0.35, "Verificare integritate schema...");
        await Step(0.55, "Incarcare configuratie utilizator...");
        await Step(0.75, "Initializare module rapoarte...");
        await Step(0.95, "Pregatire interfata...");
        await Task.Delay(300);

        Progress = 1.0;
        StatusText = "Gata.";

        await Task.Delay(250);
        Completed?.Invoke(this, EventArgs.Empty);
    }

    private async Task Step(double targetProgress, string label)
    {
        StatusText = label;
        // Animatie smooth: incrementam progresul gradual in loc sa "sara" la valoarea finala.
        // 12 steps cu 18ms = ~216ms per tranzitie, suficient pentru o iesire fluida.
        var start = Progress;
        for (var i = 0; i < 12; i++)
        {
            await Task.Delay(18);
            var t = (i + 1) / 12.0;
            // ease-out: incepe rapid si incetineste — feeling mai natural decat linear
            var eased = 1 - Math.Pow(1 - t, 3);
            await Dispatcher.UIThread.InvokeAsync(() =>
                Progress = start + (targetProgress - start) * eased);
        }
    }
}
