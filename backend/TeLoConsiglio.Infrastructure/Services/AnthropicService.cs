using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TeLoConsiglio.Infrastructure.Services;

public class AnthropicService : IAnthropicService
{
    private readonly HttpClient _http;
    private readonly ILogger<AnthropicService> _logger;
    private readonly string? _apiKey;
    private readonly string _model;

    // Prezzi in USD per milione di token (input / output).
    // cache_read = 10% del prezzo input (sconto cache hit).
    // cache_write (cache_creation) = 125% del prezzo input (overhead di creazione cache).
    // Riferimenti: https://www.anthropic.com/pricing
    private static readonly Dictionary<string, (decimal inputPerMTok, decimal outputPerMTok)> Pricing =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["claude-sonnet-4-6"] = (3m, 15m),
            ["claude-sonnet-4-5"] = (3m, 15m),
            ["claude-opus-4-5"] = (15m, 75m),
            ["claude-opus-4-6"] = (15m, 75m),
            ["claude-haiku-4-5"] = (1m, 5m),
        };

    public AnthropicService(HttpClient http, IConfiguration cfg, ILogger<AnthropicService> logger)
    {
        _http = http;
        _logger = logger;
        _apiKey = cfg["ANTHROPIC_API_KEY"] ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        _model = cfg["ANTHROPIC_MODEL"] ?? "claude-sonnet-4-6";
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);
    public string Model => _model;

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, int maxTokens = 4000, CancellationToken ct = default)
        => (await CompleteWithUsageAsync("", systemPrompt, userPrompt, maxTokens, ct)).Text;

    public async Task<string> CompleteAsync(string cacheableSystem, string volatileSystem, string userPrompt, int maxTokens = 4000, CancellationToken ct = default)
        => (await CompleteWithUsageAsync(cacheableSystem, volatileSystem, userPrompt, maxTokens, ct)).Text;

    public Task<AnthropicResult> CompleteWithUsageAsync(string systemPrompt, string userPrompt, int maxTokens = 4000, CancellationToken ct = default)
        => CompleteWithUsageAsync("", systemPrompt, userPrompt, maxTokens, ct);

    public async Task<AnthropicResult> CompleteWithUsageAsync(string cacheableSystem, string volatileSystem, string userPrompt, int maxTokens = 4000, CancellationToken ct = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("ANTHROPIC_API_KEY non configurata.");

        object systemPayload;
        if (!string.IsNullOrWhiteSpace(cacheableSystem))
        {
            var arr = new List<object>
            {
                new
                {
                    type = "text",
                    text = cacheableSystem,
                    cache_control = new { type = "ephemeral" }
                }
            };
            if (!string.IsNullOrWhiteSpace(volatileSystem))
                arr.Add(new { type = "text", text = volatileSystem });
            systemPayload = arr;
        }
        else
        {
            systemPayload = volatileSystem ?? string.Empty;
        }

        var body = new
        {
            model = _model,
            max_tokens = maxTokens,
            system = systemPayload,
            messages = new[]
            {
                new { role = "user", content = userPrompt }
            }
        };

        const int maxAttempts = 3;
        HttpResponseMessage? resp = null;
        string text = string.Empty;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
            request.Headers.Add("x-api-key", _apiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");
            request.Content = JsonContent.Create(body);

            try
            {
                resp = await _http.SendAsync(request, ct);
                text = await resp.Content.ReadAsStringAsync(ct);

                var status = (int)resp.StatusCode;
                var retriable = status == 429 || (status >= 500 && status <= 599);
                if (!retriable || attempt == maxAttempts)
                    break;

                _logger.LogWarning("Anthropic transient {Status} (attempt {Attempt}/{Max}), retrying", status, attempt, maxAttempts);
                resp.Dispose();
                resp = null;
            }
            catch (HttpRequestException ex) when (attempt < maxAttempts)
            {
                _logger.LogWarning(ex, "Anthropic network error (attempt {Attempt}/{Max}), retrying", attempt, maxAttempts);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested && attempt < maxAttempts)
            {
                _logger.LogWarning("Anthropic timeout (attempt {Attempt}/{Max}), retrying", attempt, maxAttempts);
            }

            var delayMs = 500 * (int)Math.Pow(3, attempt - 1);
            await Task.Delay(delayMs, ct);
        }

        if (resp == null)
            throw new HttpRequestException("Anthropic API non raggiungibile dopo i retry.");

        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogError("Anthropic error {Status}: {Body}", resp.StatusCode, text);
            resp.Dispose();
            throw new HttpRequestException($"Anthropic API error ({(int)resp.StatusCode}): {text}");
        }
        resp.Dispose();

        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;

        var sb = new System.Text.StringBuilder();
        if (root.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
        {
            foreach (var part in content.EnumerateArray())
            {
                if (part.TryGetProperty("type", out var t) && t.GetString() == "text"
                    && part.TryGetProperty("text", out var pt))
                {
                    sb.Append(pt.GetString());
                }
            }
        }

        int inputTokens = 0, outputTokens = 0, cachedRead = 0, cacheCreate = 0;
        if (root.TryGetProperty("usage", out var usage) && usage.ValueKind == JsonValueKind.Object)
        {
            inputTokens = GetIntOr0(usage, "input_tokens");
            outputTokens = GetIntOr0(usage, "output_tokens");
            cachedRead = GetIntOr0(usage, "cache_read_input_tokens");
            cacheCreate = GetIntOr0(usage, "cache_creation_input_tokens");
        }

        var cost = EstimateCostUsd(_model, inputTokens, outputTokens, cachedRead, cacheCreate);
        return new AnthropicResult(sb.ToString(), _model, inputTokens, outputTokens, cachedRead, cacheCreate, cost);
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
            // fallback sui prezzi sonnet
            p = (3m, 15m);
        }
        decimal toMTok(int t) => (decimal)t / 1_000_000m;
        var inputCost = toMTok(inputTokens) * p.inputPerMTok;
        var outputCost = toMTok(outputTokens) * p.outputPerMTok;
        var cachedReadCost = toMTok(cachedReadTokens) * (p.inputPerMTok * 0.10m);
        var cacheWriteCost = toMTok(cacheCreationTokens) * (p.inputPerMTok * 1.25m);
        return Math.Round(inputCost + outputCost + cachedReadCost + cacheWriteCost, 6);
    }
}
