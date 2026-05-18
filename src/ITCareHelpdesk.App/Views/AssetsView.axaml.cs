using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ITCareHelpdesk.App.Views;

public partial class AssetsView : UserControl
{
    public AssetsView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
