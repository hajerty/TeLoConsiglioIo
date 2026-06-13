namespace TeLoConsiglio.Infrastructure.Services;

public record AnthropicResult(
    string Text,
    string Model,
    int InputTokens,
    int OutputTokens,
    int CachedReadTokens,
    int CacheCreationTokens,
    decimal EstimatedCostUsd
);

public interface IAnthropicService
{
    bool IsConfigured { get; }
    string Model { get; }

    Task<string> CompleteAsync(string systemPrompt, string userPrompt, int maxTokens = 4000, CancellationToken ct = default);
    Task<string> CompleteAsync(string cacheableSystem, string volatileSystem, string userPrompt, int maxTokens = 4000, CancellationToken ct = default);

    Task<AnthropicResult> CompleteWithUsageAsync(string systemPrompt, string userPrompt, int maxTokens = 4000, CancellationToken ct = default);
    Task<AnthropicResult> CompleteWithUsageAsync(string cacheableSystem, string volatileSystem, string userPrompt, int maxTokens = 4000, CancellationToken ct = default);

    decimal EstimateCostUsd(string model, int inputTokens, int outputTokens, int cachedReadTokens, int cacheCreationTokens);
}
