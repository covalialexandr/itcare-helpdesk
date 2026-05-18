using System;
using Avalonia;
using Avalonia.ReactiveUI;

namespace ITCareHelpdesk.App;

internal static class Program
{
    // Avalonia cere ca initializarea sa fie pe acest pattern static — atribuim
    // exceptiile globale aici ca sa nu cadem in tacere daca explodeaza ceva in startup.
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            // log brut pe disk — daca crap-uie in productie macar avem ce sa citim
            System.IO.File.AppendAllText(
                "itcare_crash.log",
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n\n");
            throw;
        }
    }

    // Configuratia low-level a runtime-ului Avalonia. Activam ReactiveUI
    // pentru ca lucram cu CommunityToolkit.Mvvm + observabile pe partea de UI.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
}
