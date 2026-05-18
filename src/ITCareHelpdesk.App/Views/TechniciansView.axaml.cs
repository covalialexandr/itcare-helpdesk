using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ITCareHelpdesk.App.Views;

public partial class TechniciansView : UserControl
{
    public TechniciansView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
