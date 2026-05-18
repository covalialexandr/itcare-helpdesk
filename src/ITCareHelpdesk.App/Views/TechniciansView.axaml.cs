using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ITCareHelpdesk.App.Views;

// ============================================================
// TechniciansView.axaml.cs (code-behind)
// ============================================================
// Code-behind minimal pentru pagina Tehnicieni. UI-ul (galerie de "trading cards" cu
// metrici) este definit declarativ in TechniciansView.axaml. Logica de incarcare a datelor
// sta in TechniciansViewModel.
// ============================================================
public partial class TechniciansView : UserControl
{
    public TechniciansView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
