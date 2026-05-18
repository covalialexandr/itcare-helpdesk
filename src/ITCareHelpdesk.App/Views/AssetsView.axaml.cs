using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ITCareHelpdesk.App.Views;

// ============================================================
// AssetsView.axaml.cs (code-behind)
// ============================================================
// Code-behind minimal pentru pagina Asset-uri. Tabelul cu echipamentele (cu pill-uri
// pentru status si garantie), filtrele pe tip si search box-ul sunt declarate in
// AssetsView.axaml. Logica de incarcare + filtrare client-side sta in AssetsViewModel.
// ============================================================
public partial class AssetsView : UserControl
{
    public AssetsView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
