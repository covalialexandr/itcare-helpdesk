using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ITCareHelpdesk.App.Views;

public partial class ClientsView : UserControl
{
    public ClientsView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
