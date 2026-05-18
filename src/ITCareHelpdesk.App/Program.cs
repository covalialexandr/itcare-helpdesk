using System;
using Avalonia;
using Avalonia.ReactiveUI;

namespace ITCareHelpdesk.App;

// ============================================================
// Program.cs — PUNCTUL DE INTRARE AL APLICATIEI
// ============================================================
// Cand Windows lanseaza ITCareHelpdesk.exe, prima functie executata este Program.Main.
// E echivalentul unui "int main()" din C/C++ — Windows da control-ul aici.
//
// Trei responsabilitati:
//
//   1. STAThread — atribut obligatoriu pentru aplicatii Windows Forms/WPF/Avalonia.
//      Garanteaza ca firul principal (UI thread) este "Single-Threaded Apartment", cerinta a
//      sub-sistemului COM al Windows-ului pentru a interactiona cu controale UI native.
//
//   2. Try/Catch global — daca aplicatia explodeaza la pornire (DB inaccesibil, fisier .axaml
//      cu eroare de parse, dependinta lipsa), prindem exceptia si o scriem intr-un fisier de
//      crash log local. Fara asta, aplicatia ar muri tacut in fata utilizatorului care nu ar
//      avea unde sa caute cauza.
//
//   3. BuildAvaloniaApp — configureaza runtime-ul:
//      - UsePlatformDetect: alege automat backend-ul grafic potrivit (Win32, X11, macOS)
//      - WithInterFont: inregistreaza fontul Inter (pachet Avalonia.Fonts.Inter) ca disponibil global
//      - LogToTrace: debug logs catre System.Diagnostics
//      - UseReactiveUI: integrare cu CommunityToolkit.Mvvm si Reactive Extensions
//
// StartWithClassicDesktopLifetime intra in bucla de mesaje a Windows si lanseaza fereastra
// principala configurata din App.axaml.cs.
// ============================================================
internal static class Program
{
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
