using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TeLoConsiglio.Infrastructure.Services;

public class AnthropicService : IAnthropicService
{
    private readonly HttpClient _http;
    private readonly ILogger<AnthropicService> _logger;
    private readonly string? _apiKey;
    private readonly string _model;

    public AnthropicService(HttpClient http, IConfiguration cfg, ILogger<AnthropicService> logger)
    {
        _http = http;
        _logger = logger;
        _apiKey = cfg["ANTHROPIC_API_KEY"] ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        _model = cfg["ANTHROPIC_MODEL"] ?? "claude-opus-4-5";
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, int maxTokens = 4000, CancellationToken ct = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("ANTHROPIC_API_KEY non configurata.");

        var body = new
        {
            model = _model,
            max_tokens = maxTokens,
            system = systemPrompt,
            messages = new[]
            {
                new { role = "user", content = userPrompt }
            }
        };

        const int maxAttempts = 3; // 1 try + 2 retries
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

            // exponential backoff: 500ms, 1500ms
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
        if (root.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var part in content.EnumerateArray())
            {
                if (part.TryGetProperty("type", out var t) && t.GetString() == "text"
                    && part.TryGetProperty("text", out var pt))
                {
                    sb.Append(pt.GetString());
                }
            }
            return sb.ToString();
        }
        return string.Empty;
    }
}
