namespace TeLoConsiglio.Infrastructure.Services;

public interface IAuditLogger
{
    Task LogAsync(string action, string? resource = null, object? details = null, CancellationToken ct = default);
}
