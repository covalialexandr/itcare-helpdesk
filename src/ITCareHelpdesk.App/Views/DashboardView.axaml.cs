using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ITCareHelpdesk.App.Views;

// ============================================================
// DashboardView.axaml.cs (code-behind)
// ============================================================
// Code-behind minimal pentru pagina Dashboard. Tot UI-ul si bindings-urile sunt definite
// in DashboardView.axaml (fisierul XAML); code-behind-ul de aici doar instantiaza
// componenta — nu contine logica.
//
// MVVM strict: nicio logica de business in code-behind. Tot ce reactioneaza la actiuni
// utilizator sta in DashboardViewModel.cs (LoadCommand, etc.). Asta tine pagina testabila
// si separata de UI-ul concret.
// ============================================================
public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
    }

    // Apel la parser-ul AXAML — incarca template-ul vizual definit in DashboardView.axaml
    // si rezolva toate bindings-urile catre DataContext (DashboardViewModel).
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
