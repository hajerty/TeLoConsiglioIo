namespace TeLoConsiglio.Infrastructure.Services;

public interface IAuditLogger
{
    Task LogAsync(string action, string? resource = null, object? details = null, CancellationToken ct = default);
    Task LogAsAsync(string userId, string action, string? resource = null, object? details = null, CancellationToken ct = default);
}
