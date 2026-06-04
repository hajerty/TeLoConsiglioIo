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

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        request.Headers.Add("x-api-key", _apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

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
        request.Content = JsonContent.Create(body);

        var resp = await _http.SendAsync(request, ct);
        var text = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogError("Anthropic error {Status}: {Body}", resp.StatusCode, text);
            throw new HttpRequestException($"Anthropic API error ({(int)resp.StatusCode}): {text}");
        }

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
