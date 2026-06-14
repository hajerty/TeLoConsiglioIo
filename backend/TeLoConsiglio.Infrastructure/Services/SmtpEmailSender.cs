using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace TeLoConsiglio.Infrastructure.Services;

/// <summary>
/// Implementazione reale che invia email tramite SMTP con MailKit + STARTTLS/SSL.
/// Configurazione tramite env vars: SMTP_HOST, SMTP_PORT, SMTP_USER, SMTP_PASS,
/// SMTP_FROM, SMTP_USE_SSL.
/// </summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly ILogger<SmtpEmailSender> _logger;
    private readonly string _host;
    private readonly int _port;
    private readonly string _user;
    private readonly string _pass;
    private readonly string _from;
    private readonly bool _useSsl;

    public bool IsConfigured => true; // costruito solo se SMTP_HOST e' valorizzato

    public SmtpEmailSender(IConfiguration cfg, ILogger<SmtpEmailSender> logger)
    {
        _logger = logger;
        _host = cfg["SMTP_HOST"]!;
        _port = int.TryParse(cfg["SMTP_PORT"], out var p) ? p : 587;
        _user = cfg["SMTP_USER"] ?? string.Empty;
        _pass = cfg["SMTP_PASS"] ?? string.Empty;
        _from = cfg["SMTP_FROM"] ?? "no-reply@teloconsiglio.io";

        // SMTP_USE_SSL: default true se porta 465, STARTTLS altrimenti
        if (cfg["SMTP_USE_SSL"] is string sslVal)
            _useSsl = !string.Equals(sslVal, "false", StringComparison.OrdinalIgnoreCase);
        else
            _useSsl = _port == 465;
    }

    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(_from));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = htmlBody };

        using var smtp = new SmtpClient();

        var socketOptions = _useSsl
            ? SecureSocketOptions.SslOnConnect
            : SecureSocketOptions.StartTlsWhenAvailable;

        await smtp.ConnectAsync(_host, _port, socketOptions, ct);

        if (!string.IsNullOrWhiteSpace(_user))
            await smtp.AuthenticateAsync(_user, _pass, ct);

        await smtp.SendAsync(message, ct);
        await smtp.DisconnectAsync(true, ct);

        _logger.LogInformation("Email inviata a {To} — Subject: {Subject}", to, subject);
    }
}
