using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ITCareHelpdesk.App.Views;

// ============================================================
// ClientsView.axaml.cs (code-behind)
// ============================================================
// Code-behind pentru pagina Clienti. Logica de filtrare si state sta in ClientsViewModel.
// Aici doar incarcam template-ul AXAML — UI-ul propriu-zis (galeria de carduri) este
// definit declarativ in ClientsView.axaml.
// ============================================================
public partial class ClientsView : UserControl
{
    public ClientsView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
