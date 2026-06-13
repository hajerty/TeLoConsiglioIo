using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TeLoConsiglio.Infrastructure.Services;

public class GeminiAIService : IAIService
{
    private readonly HttpClient _http;
    private readonly ILogger<GeminiAIService> _logger;
    private readonly string? _apiKey;
    private readonly string _model;

    // Prezzi indicativi in USD per milione di token (input / output).
    // Free tier Google AI Studio: gemini-2.5-flash e flash-lite sono GRATIS entro le quote
    // (1500 req/giorno, 1M token contesto). Riferimento: https://ai.google.dev/pricing
    // Per i tier a pagamento (Gemini API paid) i prezzi sotto sono indicativi.
    private static readonly Dictionary<string, (decimal inputPerMTok, decimal outputPerMTok)> Pricing =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["gemini-2.5-flash"] = (0m, 0m),         // free tier
            ["gemini-2.5-flash-lite"] = (0m, 0m),    // free tier
            ["gemini-2.5-pro"] = (1.25m, 10m),       // tier pagato (>200k token: 2.50 / 15)
            ["gemini-1.5-flash"] = (0m, 0m),         // free tier
            ["gemini-1.5-pro"] = (1.25m, 5m),
        };

    public GeminiAIService(HttpClient http, IConfiguration cfg, ILogger<GeminiAIService> logger)
    {
        _http = http;
        _logger = logger;
        _apiKey = cfg["GEMINI_API_KEY"];
        _model = cfg["GEMINI_MODEL"] ?? "gemini-2.5-flash";
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);
    public string Model => _model;

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, int maxTokens = 4000, CancellationToken ct = default)
        => (await CompleteWithUsageAsync("", systemPrompt, userPrompt, maxTokens, ct)).Text;

    public async Task<string> CompleteAsync(string cacheableSystem, string volatileSystem, string userPrompt, int maxTokens = 4000, CancellationToken ct = default)
        => (await CompleteWithUsageAsync(cacheableSystem, volatileSystem, userPrompt, maxTokens, ct)).Text;

    public Task<AICompletionResult> CompleteWithUsageAsync(string systemPrompt, string userPrompt, int maxTokens = 4000, CancellationToken ct = default)
        => CompleteWithUsageAsync("", systemPrompt, userPrompt, maxTokens, ct);

    public async Task<AICompletionResult> CompleteWithUsageAsync(string cacheableSystem, string volatileSystem, string userPrompt, int maxTokens = 4000, CancellationToken ct = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("GEMINI_API_KEY non configurata.");

        // Gemini ha un'API di caching dedicata (separata e con costi minimi); per ora
        // concateniamo cacheable + volatile nel system_instruction. Il free tier copre
        // comunque tutto, quindi non c'e' beneficio economico a usare la cache.
        var systemText = string.IsNullOrWhiteSpace(cacheableSystem)
            ? (volatileSystem ?? string.Empty)
            : (string.IsNullOrWhiteSpace(volatileSystem) ? cacheableSystem : $"{cacheableSystem}\n\n{volatileSystem}");

        var body = new
        {
            system_instruction = new
            {
                parts = new[] { new { text = systemText } }
            },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = userPrompt } }
                }
            },
            generationConfig = new
            {
                maxOutputTokens = maxTokens,
                temperature = 0.4
            }
        };

        var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";

        const int maxAttempts = 3;
        HttpResponseMessage? resp = null;
        string text = string.Empty;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Content = JsonContent.Create(body);

            try
            {
                resp = await _http.SendAsync(request, ct);
                text = await resp.Content.ReadAsStringAsync(ct);

                var status = (int)resp.StatusCode;
                var retriable = status == 429 || (status >= 500 && status <= 599);
                if (!retriable || attempt == maxAttempts)
                    break;

                _logger.LogWarning("Gemini transient {Status} (attempt {Attempt}/{Max}), retrying", status, attempt, maxAttempts);
                resp.Dispose();
                resp = null;
            }
            catch (HttpRequestException ex) when (attempt < maxAttempts)
            {
                _logger.LogWarning(ex, "Gemini network error (attempt {Attempt}/{Max}), retrying", attempt, maxAttempts);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested && attempt < maxAttempts)
            {
                _logger.LogWarning("Gemini timeout (attempt {Attempt}/{Max}), retrying", attempt, maxAttempts);
            }

            var delayMs = 500 * (int)Math.Pow(3, attempt - 1);
            await Task.Delay(delayMs, ct);
        }

        if (resp == null)
            throw new HttpRequestException("Gemini API non raggiungibile dopo i retry.");

        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogError("Gemini error {Status}: {Body}", resp.StatusCode, text);
            resp.Dispose();
            throw new HttpRequestException($"Gemini API error ({(int)resp.StatusCode}): {text}");
        }
        resp.Dispose();

        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;

        var sb = new System.Text.StringBuilder();
        if (root.TryGetProperty("candidates", out var candidates) && candidates.ValueKind == JsonValueKind.Array)
        {
            foreach (var cand in candidates.EnumerateArray())
            {
                if (cand.TryGetProperty("content", out var content)
                    && content.TryGetProperty("parts", out var parts)
                    && parts.ValueKind == JsonValueKind.Array)
                {
                    foreach (var part in parts.EnumerateArray())
                    {
                        if (part.TryGetProperty("text", out var pt) && pt.ValueKind == JsonValueKind.String)
                            sb.Append(pt.GetString());
                    }
                }
                break; // primo candidato e' sufficiente
            }
        }

        int inputTokens = 0, outputTokens = 0, cachedRead = 0;
        if (root.TryGetProperty("usageMetadata", out var usage) && usage.ValueKind == JsonValueKind.Object)
        {
            inputTokens = GetIntOr0(usage, "promptTokenCount");
            outputTokens = GetIntOr0(usage, "candidatesTokenCount");
            cachedRead = GetIntOr0(usage, "cachedContentTokenCount");
        }

        var cost = EstimateCostUsd(_model, inputTokens, outputTokens, cachedRead, 0);
        return new AICompletionResult(sb.ToString(), _model, inputTokens, outputTokens, cachedRead, 0, cost);
    }

    private static int GetIntOr0(JsonElement el, string name)
    {
        if (el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i))
            return i;
        return 0;
    }

    public decimal EstimateCostUsd(string model, int inputTokens, int outputTokens, int cachedReadTokens, int cacheCreationTokens)
    {
        if (!Pricing.TryGetValue(model, out var p))
        {
            // fallback: tratta come free tier (modelli sconosciuti = 0 USD)
            p = (0m, 0m);
        }
        decimal toMTok(int t) => (decimal)t / 1_000_000m;
        var inputCost = toMTok(inputTokens) * p.inputPerMTok;
        var outputCost = toMTok(outputTokens) * p.outputPerMTok;
        // Gemini cached content = 25% del prezzo input (approssimazione tier a pagamento)
        var cachedReadCost = toMTok(cachedReadTokens) * (p.inputPerMTok * 0.25m);
        return Math.Round(inputCost + outputCost + cachedReadCost, 6);
    }
}
