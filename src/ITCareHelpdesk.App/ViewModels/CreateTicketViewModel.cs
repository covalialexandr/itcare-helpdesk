using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ITCareHelpdesk.App.Models;
using ITCareHelpdesk.App.Services;

namespace ITCareHelpdesk.App.ViewModels;

// CreateTicketViewModel — formularul de creare tichet, expus ca dialog modal.
// Lncarcam paralel clienti/categorii/tehnicieni la pornire ca utilizatorul sa nu astepte
// de fiecare data cand schimba un dropdown.
public sealed partial class CreateTicketViewModel : ViewModelBase
{
    private readonly TicketRepository _tickets;
    private readonly ClientRepository _clients;
    private readonly CategoryRepository _categories;
    private readonly StatsRepository _stats;
    private readonly SessionService _session;
    private readonly ToastService _toast;
    private readonly AiSuggestionService _ai;

    // Daca cheia AI lipseste, ascundem butonul "Sugereaza" — nu vrem un buton care nu face nimic
    public bool AiEnabled => _ai.IsConfigured;
    [ObservableProperty] private string? _aiSuggestion;

    public ObservableCollection<Client> ClientOptions { get; } = new();
    public ObservableCollection<CategoryOption> CategoryOptions { get; } = new();
    public ObservableCollection<TechnicianOption> TechnicianOptions { get; } = new();
    public ObservableCollection<string> Priorities { get; } = new() { "CRITICAL", "HIGH", "MEDIUM", "LOW" };
    public ObservableCollection<string> Types { get; } = new() { "INCIDENT", "REQUEST", "CHANGE", "PROBLEM" };

    [ObservableProperty] private string _titlu = "";
    [ObservableProperty] private string _descriere = "";
    [ObservableProperty] private Client? _selectedClient;
    [ObservableProperty] private CategoryOption? _selectedCategory;
    [ObservableProperty] private TechnicianOption? _selectedTechnician;
    [ObservableProperty] private string _selectedPriority = "MEDIUM";
    [ObservableProperty] private string _selectedType = "INCIDENT";
    [ObservableProperty] private string? _errorMessage;

    // Eveniment ascultat de Window pentru a se inchide cu rezultat dupa submit reusit.
    public event EventHandler<int>? TicketCreated; // arg = noul tichet_id

    public CreateTicketViewModel(
        TicketRepository tickets,
        ClientRepository clients,
        CategoryRepository categories,
        StatsRepository stats,
        SessionService session,
        ToastService toast,
        AiSuggestionService ai)
    {
        _tickets = tickets;
        _clients = clients;
        _categories = categories;
        _stats = stats;
        _session = session;
        _toast = toast;
        _ai = ai;
        _ = LoadOptionsAsync();
    }

    [RelayCommand]
    private async Task SuggestAsync()
    {
        AiSuggestion = null;
        if (!_ai.IsConfigured)
        {
            ErrorMessage = "AI nu este configurat. Adauga AnthropicApiKey in appsettings.json.";
            return;
        }
        if (string.IsNullOrWhiteSpace(Titlu) || Titlu.Length < 6)
        {
            ErrorMessage = "Scrie cel putin titlul (6+ caractere) inainte sa ceri sugestie.";
            return;
        }

        IsBusy = true;
        BusyMessage = "Cerem sugestia AI...";
        try
        {
            var sug = await _ai.SuggestAsync(Titlu, Descriere, CategoryOptions);
            if (sug is null)
            {
                ErrorMessage = "AI nu a returnat o sugestie utilizabila.";
                return;
            }

            // Aplicam sugestia in dropdown-uri
            foreach (var c in CategoryOptions)
            {
                if (c.CategorieId == sug.CategorieId)
                {
                    SelectedCategory = c;
                    break;
                }
            }
            if (!string.IsNullOrWhiteSpace(sug.Prioritate))
            {
                var priority = sug.Prioritate.ToUpperInvariant();
                if (Priorities.Contains(priority))
                    SelectedPriority = priority;
            }

            AiSuggestion = sug.Motiv;
            _toast.ShowSuccess("Sugestie AI", "Categorie si prioritate pre-completate.");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Eroare AI: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadOptionsAsync()
    {
        IsBusy = true;
        BusyMessage = "Pregatim formularul...";
        try
        {
            // Paralelizam apelurile catre DB ca formularul sa apara rapid
            var clientsTask = _clients.GetAllAsync();
            var catsTask    = _categories.GetAllAsync();
            var techsTask   = _stats.GetTechniciansAsync();

            await Task.WhenAll(clientsTask, catsTask, techsTask);

            foreach (var c in await clientsTask) ClientOptions.Add(c);
            foreach (var c in await catsTask) CategoryOptions.Add(c);

            // Pentru tehnicieni mapam la TechnicianOption — modelul "greu" Technician
            // are 12 campuri din care formularul are nevoie de 3.
            foreach (var t in await techsTask)
                TechnicianOptions.Add(new TechnicianOption(0, t.Nume, t.Specializare));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Nu am putut incarca optiunile: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(Titlu) || Titlu.Length < 6)
        {
            ErrorMessage = "Titlul este obligatoriu (minim 6 caractere).";
            return;
        }
        if (SelectedClient is null) { ErrorMessage = "Selecteaza clientul."; return; }
        if (SelectedCategory is null) { ErrorMessage = "Selecteaza o categorie."; return; }

        IsBusy = true;
        BusyMessage = "Cream tichetul...";
        try
        {
            // tehnician_id null = tichet neasignat; SP-ul accepta NULL si lasa pe operator sa-l atribuie ulterior.
            // TechnicianOption-ul nostru nu poarta TehnicianId real momentan (vine de la GetStatisticiTehnicieni
            // care nu intoarce ID-uri); pentru demo lasam ca null si gestionarii vor asigna manual din lista.
            var newId = await _tickets.OpenTicketAsync(
                titlu: Titlu.Trim(),
                descriere: string.IsNullOrWhiteSpace(Descriere) ? null : Descriere.Trim(),
                clientId: SelectedClient.ClientId,
                categorieId: SelectedCategory.CategorieId,
                prioritate: SelectedPriority,
                tip: SelectedType,
                tehnicianId: null,
                createdBy: _session.CurrentUser?.UserId);

            _toast.ShowSuccess("Tichet creat", $"Numar nou: #{newId}");
            TicketCreated?.Invoke(this, newId);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Eroare la creare: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        // Inchiderea efectiva e gestionata de Window care asculta acest event la None argument
        TicketCreated?.Invoke(this, -1);
    }
}
