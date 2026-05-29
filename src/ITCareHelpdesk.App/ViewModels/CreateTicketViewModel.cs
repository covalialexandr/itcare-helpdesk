using System; // functii si tipuri de baza
using System.Collections.ObjectModel; // colectii observabile pentru UI
using System.Threading.Tasks; // suport metode async
using CommunityToolkit.Mvvm.ComponentModel; // proprietati generate automat
using CommunityToolkit.Mvvm.Input; // comenzi generate automat
using ITCareHelpdesk.App.Models; // modele aplicatie
using ITCareHelpdesk.App.Services; // servicii aplicatie

namespace ITCareHelpdesk.App.ViewModels;

// CreateTicketViewModel — formularul de creare tichet, expus ca dialog modal.
// Lncarcam paralel clienti/categorii/tehnicieni la pornire ca utilizatorul sa nu astepte
// de fiecare data cand schimba un dropdown.

public sealed partial class CreateTicketViewModel : ViewModelBase
{
    private readonly TicketRepository _tickets; // operatii cu tichete
    private readonly ClientRepository _clients; // acces clienti
    private readonly CategoryRepository _categories; // acces categorii
    private readonly StatsRepository _stats; // statistici tehnicieni
    private readonly SessionService _session; // utilizator logat
    private readonly ToastService _toast; // notificari aplicatie
    private readonly AiSuggestionService _ai; // serviciu AI

    // Daca cheia AI lipseste, ascundem butonul "Sugereaza" — nu vrem un buton care nu face nimic
    public bool AiEnabled => _ai.IsConfigured; // verifica daca AI e activ
    [ObservableProperty] private string? _aiSuggestion; // sugestie generata de AI

    // Tichete similare gasite de AI — afisate intr-un panou expandabil sub formular
    public ObservableCollection<SimilarTicketDisplay> SimilarTickets { get; } = new(); // rezultate similare
    [ObservableProperty] private bool _hasSimilarResults; // exista rezultate similare

    public ObservableCollection<Client> ClientOptions { get; } = new(); // clienti dropdown
    public ObservableCollection<CategoryOption> CategoryOptions { get; } = new(); // categorii dropdown
    public ObservableCollection<TechnicianOption> TechnicianOptions { get; } = new(); // tehnicieni dropdown
    public ObservableCollection<string> Priorities { get; } = new() { "CRITICAL", "HIGH", "MEDIUM", "LOW" }; // prioritati disponibile
    public ObservableCollection<string> Types { get; } = new() { "INCIDENT", "REQUEST", "CHANGE", "PROBLEM" }; // tipuri disponibile

    [ObservableProperty] private string _titlu = ""; // titlu tichet
    [ObservableProperty] private string _descriere = ""; // descriere tichet
    [ObservableProperty] private Client? _selectedClient; // client ales
    [ObservableProperty] private CategoryOption? _selectedCategory; // categorie aleasa
    [ObservableProperty] private TechnicianOption? _selectedTechnician; // tehnician ales
    [ObservableProperty] private string _selectedPriority = "MEDIUM"; // prioritate selectata
    [ObservableProperty] private string _selectedType = "INCIDENT"; // tip selectat
    [ObservableProperty] private string? _errorMessage; // mesaj eroare

    // Eveniment ascultat de Window pentru a se inchide cu rezultat dupa submit reusit.
    public event EventHandler<int>? TicketCreated; // transmite id nou tichet

    public CreateTicketViewModel(
        TicketRepository tickets,
        ClientRepository clients,
        CategoryRepository categories,
        StatsRepository stats,
        SessionService session,
        ToastService toast,
        AiSuggestionService ai)
    {
        _tickets = tickets; // salvam repository tichete
        _clients = clients; // salvam repository clienti
        _categories = categories; // salvam categorii
        _stats = stats; // salvam statistici
        _session = session; // utilizator activ
        _toast = toast; // notificari UI
        _ai = ai; // AI service

        _ = LoadOptionsAsync(); // incarcare date initiale
    }

    [RelayCommand] // genereaza automat comanda
    private async Task FindSimilarAsync()
    {
        SimilarTickets.Clear(); // sterge rezultate vechi
        HasSimilarResults = false; // ascunde panou rezultate

        if (!_ai.IsConfigured)
        {
            ErrorMessage = "AI nu este configurat. Verifica Ai:Provider in appsettings.json (sau seteaza Ai:OllamaBaseUrl / Ai:AnthropicApiKey).";
            return; // iesim daca AI lipseste
        }

        if (string.IsNullOrWhiteSpace(Titlu) || Titlu.Length < 6)
        {
            ErrorMessage = "Scrie macar titlul ca AI sa stie ce sa caute.";
            return; // validare titlu
        }

        IsBusy = true; // porneste loader
        BusyMessage = "Cautam tichete similare in arhiva..."; // mesaj incarcare

        try
        {
            var closed = await _tickets.GetClosedAsync(40); // ultimele tichete inchise

            var matches = await _ai.FindSimilarAsync(Titlu, Descriere, closed); // cautare AI

            if (matches is null || matches.Count == 0)
            {
                _toast.ShowInfo("Tichete similare", "Niciun rezultat — pare un caz nou."); // nimic gasit
                return;
            }

            // Mapam ID-urile inapoi la tichetele complete ca sa avem titlu + numar + categorie de afisat
            foreach (var m in matches.Take(3)) // luam doar primele 3
            {
                var full = closed.FirstOrDefault(t => t.TichetId == m.Id); // cautam tichetul complet

                if (full is null) continue; // ignoram daca lipseste

                SimilarTickets.Add(new SimilarTicketDisplay(
                    NumarTichet: full.NumarTichet,
                    Titlu: full.Titlu,
                    Categorie: full.Categorie,
                    Scor: m.Scor,
                    Motiv: m.Motiv)); // adaugam rezultat
            }

            HasSimilarResults = SimilarTickets.Count > 0; // afiseaza panou
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Eroare AI similar: {ex.Message}"; // eroare AI
        }
        finally
        {
            IsBusy = false; // opreste loader
        }
    }

    [RelayCommand] // genereaza automat comanda
    private async Task SuggestAsync()
    {
        AiSuggestion = null; // reset sugestie

        if (!_ai.IsConfigured)
        {
            ErrorMessage = "AI nu este configurat. Verifica Ai:Provider in appsettings.json (sau seteaza Ai:OllamaBaseUrl / Ai:AnthropicApiKey).";
            return; // verificare AI
        }

        if (string.IsNullOrWhiteSpace(Titlu) || Titlu.Length < 6)
        {
            ErrorMessage = "Scrie cel putin titlul (6+ caractere) inainte sa ceri sugestie.";
            return; // validare titlu
        }

        IsBusy = true; // porneste loader
        BusyMessage = "Cerem sugestia AI..."; // mesaj incarcare

        try
        {
            var sug = await _ai.SuggestAsync(Titlu, Descriere, CategoryOptions); // cere sugestie

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
                    SelectedCategory = c; // seteaza categoria
                    break;
                }
            }

            if (!string.IsNullOrWhiteSpace(sug.Prioritate))
            {
                var priority = sug.Prioritate.ToUpperInvariant(); // transforma textul

                if (Priorities.Contains(priority))
                    SelectedPriority = priority; // seteaza prioritatea
            }

            AiSuggestion = sug.Motiv; // motiv AI
            _toast.ShowSuccess("Sugestie AI", "Categorie si prioritate pre-completate."); // mesaj succes
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Eroare AI: {ex.Message}"; // mesaj eroare
        }
        finally
        {
            IsBusy = false; // opreste loader
        }
    }

    private async Task LoadOptionsAsync()
    {
        IsBusy = true; // porneste loader
        BusyMessage = "Pregatim formularul..."; // mesaj incarcare

        try
        {
            // Paralelizam apelurile catre DB ca formularul sa apara rapid
            var clientsTask = _clients.GetAllAsync(); // incarcare clienti
            var catsTask    = _categories.GetAllAsync(); // incarcare categorii
            var techsTask   = _stats.GetTechniciansAsync(); // incarcare tehnicieni

            await Task.WhenAll(clientsTask, catsTask, techsTask); // asteapta toate apelurile

            foreach (var c in await clientsTask) ClientOptions.Add(c); // adauga clienti
            foreach (var c in await catsTask) CategoryOptions.Add(c); // adauga categorii

            // Pentru tehnicieni mapam la TechnicianOption
            foreach (var t in await techsTask)
                TechnicianOptions.Add(new TechnicianOption(0, t.Nume, t.Specializare)); // adauga tehnician
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Nu am putut incarca optiunile: {ex.Message}"; // eroare incarcare
        }
        finally
        {
            IsBusy = false; // opreste loader
        }
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        ErrorMessage = null; // reset eroare

        if (string.IsNullOrWhiteSpace(Titlu) || Titlu.Length < 6)
        {
            ErrorMessage = "Titlul este obligatoriu (minim 6 caractere).";
            return;
        }

        if (SelectedClient is null) { ErrorMessage = "Selecteaza clientul."; return; } // validare client
        if (SelectedCategory is null) { ErrorMessage = "Selecteaza o categorie."; return; } // validare categorie

        IsBusy = true; // porneste loader
        BusyMessage = "Cream tichetul..."; // mesaj incarcare

        try
        {
            var newId = await _tickets.OpenTicketAsync(
                titlu: Titlu.Trim(), // elimina spatii
                descriere: string.IsNullOrWhiteSpace(Descriere) ? null : Descriere.Trim(), // verifica descriere
                clientId: SelectedClient.ClientId, // id client
                categorieId: SelectedCategory.CategorieId, // id categorie
                prioritate: SelectedPriority, // prioritate
                tip: SelectedType, // tip
                tehnicianId: null, // neasignat
                createdBy: _session.CurrentUser?.UserId); // utilizator curent

            _toast.ShowSuccess("Tichet creat", $"Numar nou: #{newId}"); // mesaj succes
            TicketCreated?.Invoke(this, newId); // trimite eveniment
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Eroare la creare: {ex.Message}"; // mesaj eroare
        }
        finally
        {
            IsBusy = false; // opreste loader
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        // Inchiderea efectiva e gestionata de Window care asculta acest event la None argument
        TicketCreated?.Invoke(this, -1); // inchidere fara salvare
    }
}

// Reprezentare de afisare pentru un tichet similar gasit de AI.
public sealed record SimilarTicketDisplay(
    string NumarTichet, // numar tichet
    string Titlu, // titlu tichet
    string Categorie, // categorie tichet
    int Scor, // scor similaritate
    string Motiv); // explicatie AI