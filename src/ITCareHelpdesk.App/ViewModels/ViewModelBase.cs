using CommunityToolkit.Mvvm.ComponentModel;

namespace ITCareHelpdesk.App.ViewModels;

// ObservableObject din CommunityToolkit aduce INotifyPropertyChanged complet din source generators.
// Tinem un base class minim ca sa avem un loc pentru extensii cross-cutting (busy state, logger).
public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _busyMessage;
}
