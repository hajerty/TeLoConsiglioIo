namespace TeLoConsiglio.Infrastructure.Services;

public interface IAnthropicService
{
    bool IsConfigured { get; }
    Task<string> CompleteAsync(string systemPrompt, string userPrompt, int maxTokens = 4000, CancellationToken ct = default);
}
