using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ITCareHelpdesk.App.Views;

// ============================================================
// ReportsView.axaml.cs (code-behind)
// ============================================================
// Code-behind minimal pentru "Studio Rapoarte". Cele 3 carduri masive (PDF/Word/Excel)
// + butoanele de export sunt declarate in ReportsView.axaml. Logica de chemare a
// ReportService + alegere cale fisier sta in ReportsViewModel.
//
// File picker-ul nativ (dialogul "Save As" al Windows-ului) este invocat din ReportsViewModel
// via Avalonia.Platform.Storage.StorageProvider — corect arhitectural: view-ul nu stie de filesystem.
// ============================================================
public partial class ReportsView : UserControl
{
    public ReportsView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
