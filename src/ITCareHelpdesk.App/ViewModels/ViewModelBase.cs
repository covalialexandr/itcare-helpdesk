using CommunityToolkit.Mvvm.ComponentModel;

namespace ITCareHelpdesk.App.ViewModels;

// ============================================================
// ViewModelBase
// ============================================================
// Clasa de baza pe care toate ViewModels din aplicatie o mostenesc.
//
// Mostenire: ObservableObject din CommunityToolkit.Mvvm — care prin source generators
// implementeaza automat INotifyPropertyChanged (mecanismul prin care UI-ul "observa"
// schimbarile de proprietati). Asta inseamna ca scriem [ObservableProperty] pe un camp
// privat si toolkit-ul genereaza un property public + notifications, fara boilerplate.
//
// Proprietati comune mostenite de toate ViewModels:
//   IsBusy       - true cat timp ruleaza o operatie async (load, save, export);
//                  UI-ul afiseaza un overlay/spinner cand asta e true
//   BusyMessage  - textul afisat pe overlay ("Aducem date...", "Inchidem tichetul...")
//
// "partial" — keyword-ul C# care permite source generator-ului sa extinda clasa cu cod
// auto-generat in timpul compilarii.
// ============================================================
public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _busyMessage;
}
