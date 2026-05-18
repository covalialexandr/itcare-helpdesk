using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ITCareHelpdesk.App.Services;

namespace ITCareHelpdesk.App.ViewModels;

// Reports = ecran "studio" — alegi raportul + formatul, dai click si primesti fisierul.
// In viitor putem extinde cu preview live; pentru moment export-ul direct e cea mai mare valoare adusa.
public sealed partial class ReportsViewModel : ViewModelBase
{
    private readonly TicketRepository _tickets;
    private readonly ReportService _reports;
    private readonly ToastService _toast;

    [ObservableProperty] private bool _exportingWord;
    [ObservableProperty] private bool _exportingExcel;
    [ObservableProperty] private bool _exportingPdf;
    [ObservableProperty] private string? _lastExportPath;

    public ReportsViewModel(TicketRepository tickets, ReportService reports, ToastService toast)
    {
        _tickets = tickets;
        _reports = reports;
        _toast = toast;
    }

    [RelayCommand]
    private async Task ExportWordAsync()
    {
        ExportingWord = true;
        try
        {
            var path = await PickSavePathAsync("Raport_Tichete", "docx");
            if (path is null) return;
            var tickets = await _tickets.GetActiveAsync();
            LastExportPath = await _reports.ExportTicketsToWordAsync(tickets, path);
            _toast.ShowSuccess("Export gata", "Fisierul Word a fost salvat.");
        }
        catch (Exception ex)
        {
            _toast.ShowError("Export Word esuat", ex.Message);
        }
        finally
        {
            ExportingWord = false;
        }
    }

    [RelayCommand]
    private async Task ExportPdfAsync()
    {
        ExportingPdf = true;
        try
        {
            var path = await PickSavePathAsync("Raport_Tichete", "pdf");
            if (path is null) return;
            var tickets = await _tickets.GetActiveAsync();
            LastExportPath = await _reports.ExportTicketsToPdfAsync(tickets, path);
            _toast.ShowSuccess("Export gata", "Fisierul PDF a fost salvat.");
        }
        catch (Exception ex)
        {
            _toast.ShowError("Export PDF esuat", ex.Message);
        }
        finally
        {
            ExportingPdf = false;
        }
    }

    [RelayCommand]
    private async Task ExportExcelAsync()
    {
        ExportingExcel = true;
        try
        {
            var path = await PickSavePathAsync("Raport_Tichete", "xlsx");
            if (path is null) return;
            var tickets = await _tickets.GetActiveAsync();
            LastExportPath = await _reports.ExportTicketsToExcelAsync(tickets, path);
            _toast.ShowSuccess("Export gata", "Fisierul Excel a fost salvat.");
        }
        catch (Exception ex)
        {
            _toast.ShowError("Export Excel esuat", ex.Message);
        }
        finally
        {
            ExportingExcel = false;
        }
    }

    // Helper: alegerea fisierului de salvat via Avalonia.Storage. Returneaza calea sau null.
    // L-am pus in ViewModel (cu acces la TopLevel) pentru ca nu vrem un dialog service intermediar
    // doar pentru un singur use-case.
    private static async Task<string?> PickSavePathAsync(string suggestedName, string extension)
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return null;

        var window = desktop.MainWindow;
        if (window is null) return null;

        var storage = TopLevel.GetTopLevel(window)?.StorageProvider;
        if (storage is null) return null;

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Salveaza raport",
            SuggestedFileName = $"{suggestedName}_{DateTime.Now:yyyyMMdd_HHmm}.{extension}",
            DefaultExtension = extension,
            FileTypeChoices = new[]
            {
                new FilePickerFileType(extension.ToUpperInvariant())
                {
                    Patterns = new[] { $"*.{extension}" }
                }
            }
        });

        return file?.TryGetLocalPath();
    }
}
