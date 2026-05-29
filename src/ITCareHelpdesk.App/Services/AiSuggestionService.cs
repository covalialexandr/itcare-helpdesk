using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ITCareHelpdesk.App.Models;
using Microsoft.Extensions.Configuration;

namespace ITCareHelpdesk.App.Services;

// AiSuggestionService - Punctul unic de contact intre UI si "creierul" LLM.
// Aceasta clasa mascheaza complexitatea din spatele interogarii modelelor de AI.
// Indiferent daca folosim Anthropic in cloud sau Ollama local, restul aplicatiei
// apeleaza aceleasi metode asincrone, fara sa stie ce provider ruleaza de fapt.
public sealed class AiSuggestionService
{
    // Enumerare interna pentru a pastra starea providerului selectat la runtime.
    private enum AiProvider { Anthropic, Ollama }

    // Clientul de HTTP injectat, folosit pentru a trimite request-urile catre API-uri.
    private readonly HttpClient _http;
    
    // Strategia activa stabilita la pornirea aplicatiei.
    private readonly AiProvider _provider;

    // Configul specific pentru Anthropic (cheia de acces si identificatorul modelului).
    private readonly string? _anthropicKey;
    private readonly string _anthropicModel;

    // Configul specific pentru Ollama (adresa URL a serverului local si modelul incarcat).
    private readonly string _ollamaBaseUrl;
    private readonly string _ollamaModel;

    // Limita maxima de tokeni (cuvinte/fragmente) generati pentru raspunsurile standard.
    private readonly int _maxTokens;

    // Proprietate booleana interogata de interfata grafica.
    // Daca lipsesc cheile sau adresele necesare, UI-ul va ascunde complet butoanele de AI.
    public bool IsConfigured => _provider switch
    {
        AiProvider.Anthropic => !string.IsNullOrWhiteSpace(_anthropicKey),
        AiProvider.Ollama    => !string.IsNullOrWhiteSpace(_ollamaBaseUrl),
        _ => false
    };

    // Constructorul clasei unde extragem setarile din appsettings.json si pregatim serviciul.
    public AiSuggestionService(IConfiguration config, HttpClient httpClient)
    {
        // Salvam instanta de HttpClient primita prin Dependency Injection pentru a evita problemele de socket-uri.
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

        // Citim valoarea din configurare. Daca lipseste, punem implicit "anthropic" ca fallback sigur.
        var providerStr = (config["Ai:Provider"] ?? "anthropic").Trim().ToLowerInvariant();
        _provider = providerStr == "ollama" ? AiProvider.Ollama : AiProvider.Anthropic;

        // Incarcam datele pentru serviciul Anthropic din cloud.
        _anthropicKey   = config["Ai:AnthropicApiKey"];
        _anthropicModel = config["Ai:Model"] ?? "claude-3-5-haiku-latest";

        // Incarcam datele pentru serviciul local Ollama.
        // Curatam '/' de la finalul URL-ului pentru a nu crea path-uri invalide la concatenare (ex: http://127.0.0.1:11434//api/chat).
        _ollamaBaseUrl = (config["Ai:OllamaBaseUrl"] ?? "http://127.0.0.1:11434").TrimEnd('/');
        _ollamaModel   = config["Ai:OllamaModel"] ?? "llama3.2:3b";

        // Incercam sa parsam numarul maxim de tokeni. Daca valoarea e invalida, setam un default de 256.
        _maxTokens = int.TryParse(config["Ai:MaxTokens"], out var n) ? n : 256;
    }

    // =========================================================================
    // ---------- API PUBLIC (Metodele apelate din ViewModels / UI) ------------
    // =========================================================================

    // Metoda care analizeaza istoricul unui tichet si genereaza un rezumat executiv.
    public async Task<string?> SummarizeHistoryAsync(
        string numarTichet,
        string titluTichet,
        IEnumerable<Models.HistoryEntry> history)
    {
        // Daca configuratia este incompleta, oprim executia si returnam null imediat.
        if (!IsConfigured) return null;

        // Convertim colectia in lista pentru a evita multiplele iterari (Deferred Execution).
        var entries = history.ToList();
        if (entries.Count == 0) return "Nu exista activitate inregistrata pe acest tichet.";

        // Mapam fiecare intrare din istoric intr-un string structurat si le unim cu caractere de linie noua.
        // Generam un format standard pe care modelul de AI il poate citi ca pe o axa a timpului.
        var timeline = string.Join("\n", entries.Select(h =>
            $"[{h.DataActivitate}] {h.TipActivitate} de {h.EfectuatDe}: {h.Mesaj}" +
            (h.StatusVechi != null ? $" (status: {h.StatusVechi} -> {h.StatusNou})" : "")));

        // System Prompt: Setam regulile de comportament ale robotului. Il obligam sa nu puna introduceri inutile.
        var systemPrompt =
            "Esti un asistent care sumarizeaza istoricul unui tichet de helpdesk IT in 2-3 paragrafe scurte. " +
            "Spune-i operatorului ce s-a intamplat, ce s-a incercat, unde e blocajul, care e stadiul actual. " +
            "Foloseste limbaj profesionist in romana. NU include preambul - treci direct la continut. Maxim 200 cuvinte.";

        // User Prompt: Datele variabile efective (contextul tichetului curent).
        var userPrompt = $"Tichet: {numarTichet}\nTitlu: {titluTichet}\n\nIstoric:\n{timeline}";

        // Apelam motorul de AI. Setam temperatura la 0.3 (vrem text cursiv, dar legat strict de fapte).
        return await CallLlmAsync(systemPrompt, userPrompt, maxTokens: 400, jsonMode: false, temperature: 0.3);
    }

    // Metoda care compara un tichet proaspat deschis cu o lista de tichete rezolvate deja in trecut.
    public async Task<List<SimilarTicketRef>?> FindSimilarAsync(
        string titluNou,
        string? descriereNoua,
        IEnumerable<Models.Ticket> candidateClosed)
    {
        // Validari de siguranta preliminare.
        if (!IsConfigured || string.IsNullOrWhiteSpace(titluNou)) return null;

        // Luam doar primele 40 de tichete din lista transmisa pentru a nu depasi fereastra de context a LLM-ului.
        var candidates = candidateClosed.Take(40).ToList();
        if (candidates.Count == 0) return new List<SimilarTicketRef>();

        // Construim o lista text compacta cu tichetele vechi (ID, Categorie, Prioritate, Titlu).
        var listText = string.Join("\n", candidates.Select(t =>
            $"#{t.TichetId} [{t.Categorie}/{t.Prioritate}] {t.NumarTichet}: {t.Titlu}"));

        // System Prompt: Cerem explicit un format JSON specific si dictam logica de selectie a similaritatilor.
        var systemPrompt =
            "Esti un asistent pentru un helpdesk IT. Vei primi titlul + descrierea unui tichet NOU " +
            "si o lista de tichete INCHISE in trecut. Sarcina ta: identifica TOP 3 tichete inchise " +
            "care sunt cele mai asemanatoare (acelasi tip de problema). " +
            "Raspunde DOAR cu JSON valid in formatul exact: " +
            "{\"similar\":[{\"id\":<int>, \"scor\":<0-100>, \"motiv\":\"<scurta explicatie>\"}]}. " +
            "Daca nu gasesti nicio similaritate, returneaza array gol.";

        var userPrompt =
            $"TICHET NOU\nTitlu: {titluNou}\nDescriere: {descriereNoua ?? "(fara)"}\n\n" +
            $"TICHETE INCHISE DISPONIBILE:\n{listText}";

        // Temperatura 0.1 (mod determinist, aproape zero creativitate) pentru a pastra consistenta structurii JSON structurate.
        var raw = await CallLlmAsync(systemPrompt, userPrompt, maxTokens: 500, jsonMode: true, temperature: 0.1);
        if (string.IsNullOrWhiteSpace(raw)) return new List<SimilarTicketRef>();

        try
        {
            // Curatam string-ul brut primit de la AI si incercam sa il mapam pe obiectele C#.
            var parsed = JsonSerializer.Deserialize<SimilarResponse>(CleanJson(raw));
            return parsed?.Similar ?? new List<SimilarTicketRef>();
        }
        catch
        {
            // Daca modelul o ia pe aratura si JSON-ul e invalid, returnam o lista goala ca sa nu blocam restul aplicatiei.
            return new List<SimilarTicketRef>();
        }
    }

    // Metoda ce ofera sugestii rapide de categorisire si prioritate pentru un tichet nou-venit.
    public async Task<AiTicketSuggestion?> SuggestAsync(
        string titlu,
        string? descriere,
        IEnumerable<CategoryOption> availableCategories)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(titlu)) return null;

        // Generam lista text a categoriilor disponibile in sistem pentru ca AI-ul sa aleaga strict de acolo.
        var catList = string.Join("\n", availableCategories);
        
        var systemPrompt =
            "Esti un asistent care clasifica tichete IT pentru un helpdesk. " +
            "Pe baza titlului si descrierii, alegi UNA din categoriile date si propui o prioritate. " +
            "Prioritati posibile: CRITICAL, HIGH, MEDIUM, LOW. " +
            "Raspunzi DOAR cu JSON valid in formatul: " +
            "{\"categorie_id\":<int>, \"prioritate\":\"<string>\", \"motiv\":\"<scurta explicatie in romana>\"}. " +
            "Fara markdown, fara comentarii extra.\n\n" +
            "Categorii disponibile (id - nume):\n" + catList;

        var userPrompt = $"Titlu: {titlu}\nDescriere: {descriere ?? "(fara descriere)"}";

        var raw = await CallLlmAsync(systemPrompt, userPrompt, maxTokens: _maxTokens, jsonMode: true, temperature: 0.1);
        if (string.IsNullOrWhiteSpace(raw)) return null;

        try
        {
            // Parsam rezultatul curatat direct in record-ul C# dedicat.
            return JsonSerializer.Deserialize<AiTicketSuggestion>(CleanJson(raw));
        }
        catch
        {
            // Daca parsarea esueaza, aplicam un fallback: pastram textul brut trimis de AI in campul de motiv,
            // iar ID-ul categoriei devine 0 (ceea ce semnalizeaza aplicatiei ca maparea automata a esuat).
            return new AiTicketSuggestion(0, "MEDIUM", raw);
        }
    }

    // =========================================================================
    // ---------- DISPATCHER SI INTEGRARI PROVIDERI (Logica Interna) -----------
    // =========================================================================

    // Rutierul intern: analizeaza ce provider este configurat si trimite treaba mai departe.
    private Task<string?> CallLlmAsync(
        string systemPrompt,
        string userPrompt,
        int maxTokens,
        bool jsonMode,
        double temperature)
    {
        return _provider switch
        {
            AiProvider.Ollama    => CallOllamaAsync(systemPrompt, userPrompt, maxTokens, jsonMode, temperature),
            AiProvider.Anthropic => CallAnthropicAsync(systemPrompt, userPrompt, maxTokens, jsonMode, temperature),
            _ => Task.FromResult<string?>(null)
        };
    }

    // Implementarea apelului catre serviciul in cloud Anthropic (Claude).
    private async Task<string?> CallAnthropicAsync(string systemPrompt, string userPrompt, int maxTokens, bool jsonMode, double temperature)
    {
        // Construim payload-ul cerut de documentatia oficiala Anthropic Messages API.
        var request = new AnthropicRequest
        {
            Model = _anthropicModel,
            MaxTokens = maxTokens,
            System = systemPrompt,
            Temperature = temperature, // Am mapat temperatura si aici pentru a asigura stabilitate.
            Messages = new[] { new AnthropicMessage { Role = "user", Content = userPrompt } }
        };

        // Pregatim manual request-ul HTTP de tip POST catre endpoint-ul lor public.
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
        {
            Content = JsonContent.Create(request) // Serializare automata a obiectului in format JSON.
        };
        
        // Adaugam headerele obligatorii cerute de Anthropic pentru autentificare si versiune.
        req.Headers.Add("x-api-key", _anthropicKey);
        req.Headers.Add("anthropic-version", "2023-06-01");

        // Trimitem request-ul la server asincron.
        using var resp = await _http.SendAsync(req);
        var raw = await resp.Content.ReadAsStringAsync();
        
        // Daca serverul ne intoarce eroare (ex: cod 401, 429 sau 500), aruncam o exceptie explicita.
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Anthropic API a returnat {(int)resp.StatusCode}: {raw}");

        // Parsam documentul JSON raspuns si extragem nodul text din array-ul de raspuns primit.
        using var doc = JsonDocument.Parse(raw);
        var content = doc.RootElement.GetProperty("content");
        return content.GetArrayLength() > 0 ? content[0].GetProperty("text").GetString() : null;
    }

    // Implementarea apelului catre serverul local Ollama.
    private async Task<string?> CallOllamaAsync(
        string systemPrompt,
        string userPrompt,
        int maxTokens,
        bool jsonMode,
        double temperature)
    {
        // Pregatim payload-ul pentru endpoint-ul `/api/chat` din Ollama.
        var payload = new OllamaChatRequest
        {
            Model = _ollamaModel,
            Stream = false, // Dezactivam streaming-ul (vrem raspunsul complet intr-un singur pachet HTTP).
            KeepAlive = "10m", // Cerem serverului sa tina modelul incarcat in RAM timp de 10 minute pentru viitoarele tichete.
            Format = jsonMode ? "json" : null, // Daca jsonMode e true, Ollama garanteaza ca textul generat va fi JSON valid.
            Messages = new[]
            {
                new OllamaMessage { Role = "system", Content = systemPrompt },
                new OllamaMessage { Role = "user",   Content = userPrompt }
            },
            Options = new OllamaOptions
            {
                Temperature = temperature,
                NumPredict = maxTokens
            }
        };

        // Cream conexiunea HTTP catre instanta locala (ex: http://127.0.0.1:11434/api/chat).
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{_ollamaBaseUrl}/api/chat")
        {
            Content = JsonContent.Create(payload)
        };

        using var resp = await _http.SendAsync(req);
        var raw = await resp.Content.ReadAsStringAsync();
        
        // Daca Ollama da eroare, oferim indicatii operatorului/dezvoltatorului in mesajul de exceptie.
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Ollama a raspuns cu cod eroare {(int)resp.StatusCode}. Asigura-te ca ruleaza 'ollama serve'.");

        // Extragem valoarea text din raspunsul structurat Ollama: json.message.content.
        using var doc = JsonDocument.Parse(raw);
        if (!doc.RootElement.TryGetProperty("message", out var msgEl)) return null;
        if (!msgEl.TryGetProperty("content", out var contentEl)) return null;
        return contentEl.GetString();
    }

    // ---------- HELPER DE CURATARE TEXT ----------

    // Functie utila in special pentru modelele care nu respecta intotdeauna instructiunile
    // si lasa coduri de markdown sau texte inainte/dupa blocul JSON (ex: ```json { ... } ```).
    // Aceasta metoda cauta prima si ultima acolada, izoland doar obiectul JSON curat.
    private static string CleanJson(string raw)
    {
        var s = raw.Trim();
        var first = s.IndexOf('{');
        var last = s.LastIndexOf('}');
        
        // Daca am gasit o pereche valida de acolade, extragem subsirul dintre ele, inclusiv acoladele.
        if (first >= 0 && last > first)
            return s.Substring(first, last - first + 1);
        
        return s; // Daca nu s-au gasit acolade, returnam textul nemodificat (va crapa controlat la deserializare).
    }

    // =========================================================================
    // ---------- DATA TRANSFER OBJECTS (Clase de mapare structuri JSON) -------
    // =========================================================================

    // Structura de date trimisa catre API-ul Anthropic.
    private sealed class AnthropicRequest
    {
        [JsonPropertyName("model")]       public string Model { get; init; } = "";
        [JsonPropertyName("max_tokens")]  public int MaxTokens { get; init; }
        [JsonPropertyName("system")]      public string System { get; init; } = "";
        [JsonPropertyName("temperature")] public double Temperature { get; init; } = 1.0;
        [JsonPropertyName("messages")]    public AnthropicMessage[] Messages { get; init; } = Array.Empty<AnthropicMessage>();
    }
    
    private sealed class AnthropicMessage
    {
        [JsonPropertyName("role")]    public string Role { get; init; } = "";
        [JsonPropertyName("content")] public string Content { get; init; } = "";
    }

    // Structura de date trimisa catre API-ul Ollama.
    private sealed class OllamaChatRequest
    {
        [JsonPropertyName("model")]      public string Model { get; init; } = "";
        [JsonPropertyName("messages")]   public OllamaMessage[] Messages { get; init; } = Array.Empty<OllamaMessage>();
        [JsonPropertyName("stream")]     public bool Stream { get; init; }
        [JsonPropertyName("format")]     public string? Format { get; init; }
        [JsonPropertyName("keep_alive")] public string KeepAlive { get; init; } = "5m";
        [JsonPropertyName("options")]    public OllamaOptions Options { get; init; } = new();
    }
    
    private sealed class OllamaMessage
    {
        [JsonPropertyName("role")]    public string Role { get; init; } = "";
        [JsonPropertyName("content")] public string Content { get; init; } = "";
    }
    
    private sealed class OllamaOptions
    {
        [JsonPropertyName("temperature")] public double Temperature { get; init; } = 0.2;
        [JsonPropertyName("num_predict")] public int NumPredict { get; init; } = 256;
    }
}

// Record imutabil pentru parsarea raspunsului de clasificare primit de la AI.
public sealed record AiTicketSuggestion(
    [property: JsonPropertyName("categorie_id")] int CategorieId,
    [property: JsonPropertyName("prioritate")]   string Prioritate,
    [property: JsonPropertyName("motiv")]        string Motiv);

// Clasa wrapper necesara pentru a mapa proprietatea radacina de tip array numita "similar".
public sealed class SimilarResponse
{
    [JsonPropertyName("similar")]
    public List<SimilarTicketRef> Similar { get; set; } = new();
}

// Record imutabil pentru fiecare element din lista de tichete similare gasite de AI.
public sealed record SimilarTicketRef(
    [property: JsonPropertyName("id")]    int Id,
    [property: JsonPropertyName("scor")]  int Scor,
    [property: JsonPropertyName("motiv")] string Motiv);