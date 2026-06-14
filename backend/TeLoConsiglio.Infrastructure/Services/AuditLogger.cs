using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using TeLoConsiglio.Domain.Entities;
using TeLoConsiglio.Infrastructure.Data;

namespace TeLoConsiglio.Infrastructure.Services;

public class AuditLogger : IAuditLogger
{
    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _http;
    private readonly UserManager<ApplicationUser> _users;
    private readonly ILogger<AuditLogger> _logger;

    public AuditLogger(
        AppDbContext db,
        IHttpContextAccessor http,
        UserManager<ApplicationUser> users,
        ILogger<AuditLogger> logger)
    {
        _db = db;
        _http = http;
        _users = users;
        _logger = logger;
    }

    public async Task LogAsync(string action, string? resource = null, object? details = null, CancellationToken ct = default)
    {
        try
        {
            var ctx = _http.HttpContext;
            string? userId = null;
            string? userEmail = null;
            string? ipAddress = null;
            string? userAgent = null;

            if (ctx != null)
            {
                userId = _users.GetUserId(ctx.User);
                if (userId != null)
                {
                    var user = await _users.FindByIdAsync(userId);
                    userEmail = user?.Email;
                }

                var ip = ctx.Connection.RemoteIpAddress;
                if (ip != null)
                    ipAddress = ip.ToString();

                userAgent = ctx.Request.Headers["User-Agent"].ToString();
                if (!string.IsNullOrEmpty(userAgent) && userAgent.Length > 500)
                    userAgent = userAgent.Substring(0, 500);
            }

            string? detailsJson = null;
            if (details != null)
            {
                detailsJson = JsonSerializer.Serialize(details);
            }

            _db.AuditLogs.Add(new AuditLog
            {
                Action = action,
                Resource = resource,
                UserId = userId,
                UserEmail = userEmail,
                DetailsJson = detailsJson,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // L'audit NON deve mai rompere l'azione utente: log e basta
            _logger.LogError(ex, "Errore salvataggio audit log per action={Action} resource={Resource}", action, resource);
        }
    }
}
