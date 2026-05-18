using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ITCareHelpdesk.App.Models;
using Microsoft.Extensions.Configuration;

namespace ITCareHelpdesk.App.Services;

// AiSuggestionService — apeleaza Anthropic Messages API ca sa propuna categorie + prioritate pe
// baza titlului si descrierii unui tichet. Folosim HttpClient direct (zero dependente in plus
// fata de System.Net.Http care e build-in in .NET 8).
//
// Daca cheia lipseste din appsettings.json, returneaza null si UI-ul va arata graceful disabled.
public sealed class AiSuggestionService
{
    private readonly HttpClient _http;
    private readonly string? _apiKey;
    private readonly string _model;
    private readonly int _maxTokens;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    public AiSuggestionService(IConfiguration config)
    {
        _apiKey    = config["Ai:AnthropicApiKey"];
        _model     = config["Ai:Model"] ?? "claude-haiku-4-5";
        _maxTokens = int.TryParse(config["Ai:MaxTokens"], out var n) ? n : 256;

        _http = new HttpClient
        {
            BaseAddress = new Uri("https://api.anthropic.com/"),
            Timeout = TimeSpan.FromSeconds(20)
        };
    }

    public async Task<AiTicketSuggestion?> SuggestAsync(
        string titlu,
        string? descriere,
        IEnumerable<CategoryOption> availableCategories)
    {
        if (!IsConfigured) return null;
        if (string.IsNullOrWhiteSpace(titlu)) return null;

        // Construim un prompt sistem care defineste output-ul ca JSON strict — mai usor de parsat
        // decat un text liber. "Few-shot" inline cu un exemplu.
        var catList = string.Join("\n", availableCategories);
        var systemPrompt =
            "Esti un asistent care clasifica tichete IT pentru un helpdesk. " +
            "Pe baza titlului si descrierii, alegi UNA din categoriile date si propui o prioritate. " +
            "Prioritati posibile: CRITICAL, HIGH, MEDIUM, LOW. " +
            "Raspunzi DOAR cu JSON valid in formatul: " +
            "{\"categorie_id\":<int>, \"prioritate\":\"<string>\", \"motiv\":\"<scurt explicatie in romana>\"}. " +
            "Fara markdown, fara comentarii, doar JSON-ul.\n\n" +
            "Categorii disponibile (id - nume):\n" + catList;

        var userPrompt = $"Titlu: {titlu}\nDescriere: {descriere ?? "(fara descriere)"}";

        var request = new AnthropicRequest
        {
            Model = _model,
            MaxTokens = _maxTokens,
            System = systemPrompt,
            Messages = new[] { new AnthropicMessage { Role = "user", Content = userPrompt } }
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, "v1/messages")
        {
            Content = JsonContent.Create(request)
        };
        req.Headers.Add("x-api-key", _apiKey);
        req.Headers.Add("anthropic-version", "2023-06-01");

        using var resp = await _http.SendAsync(req);
        var raw = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
        {
            // Lasam exceptia in sus cu mesajul concret — handler-ul UI-ului afiseaza in toast
            throw new InvalidOperationException(
                $"Anthropic API a returnat {(int)resp.StatusCode}: {raw}");
        }

        // Parsing: extragem textul din primul content block, apoi parsam JSON-ul
        using var doc = JsonDocument.Parse(raw);
        var content = doc.RootElement.GetProperty("content");
        var text = "";
        if (content.GetArrayLength() > 0)
            text = content[0].GetProperty("text").GetString() ?? "";

        try
        {
            return JsonSerializer.Deserialize<AiTicketSuggestion>(text);
        }
        catch
        {
            // Daca modelul a deviat de la JSON pur, returnam un fallback cu motivul = textul brut
            return new AiTicketSuggestion(0, "MEDIUM", text);
        }
    }

    // ------ DTO pentru Anthropic API ------
    private sealed class AnthropicRequest
    {
        [JsonPropertyName("model")]      public string Model { get; init; } = "";
        [JsonPropertyName("max_tokens")] public int MaxTokens { get; init; }
        [JsonPropertyName("system")]     public string System { get; init; } = "";
        [JsonPropertyName("messages")]   public AnthropicMessage[] Messages { get; init; } = Array.Empty<AnthropicMessage>();
    }
    private sealed class AnthropicMessage
    {
        [JsonPropertyName("role")]    public string Role { get; init; } = "";
        [JsonPropertyName("content")] public string Content { get; init; } = "";
    }
}

// Modelul de output pe care-l asteptam de la AI
public sealed record AiTicketSuggestion(
    [property: JsonPropertyName("categorie_id")] int CategorieId,
    [property: JsonPropertyName("prioritate")]   string Prioritate,
    [property: JsonPropertyName("motiv")]        string Motiv);
