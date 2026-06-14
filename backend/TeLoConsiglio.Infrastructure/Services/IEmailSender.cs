namespace TeLoConsiglio.Infrastructure.Services;

public interface IEmailSender
{
    /// <summary>True se il servizio email e' configurato e pronto all'invio.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Invia un'email HTML.
    /// </summary>
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
}
