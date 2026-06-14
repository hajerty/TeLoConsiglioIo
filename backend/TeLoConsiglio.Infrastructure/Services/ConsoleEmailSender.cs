using Microsoft.Extensions.Logging;

namespace TeLoConsiglio.Infrastructure.Services;

/// <summary>
/// Fallback email sender: logga l'email su stdout invece di inviarla.
/// Usato quando SMTP_HOST non e' configurato.
/// </summary>
public class ConsoleEmailSender : IEmailSender
{
    private readonly ILogger<ConsoleEmailSender> _logger;

    public bool IsConfigured => true;

    public ConsoleEmailSender(ILogger<ConsoleEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        const int maxBodyLen = 500;
        var truncated = htmlBody.Length > maxBodyLen
            ? htmlBody.Substring(0, maxBodyLen) + " [... body troncato ...]"
            : htmlBody;

        _logger.LogInformation(
            "[EMAIL-CONSOLE] To: {To} | Subject: {Subject} | Body (troncato): {Body}",
            to, subject, truncated);

        return Task.CompletedTask;
    }
}
