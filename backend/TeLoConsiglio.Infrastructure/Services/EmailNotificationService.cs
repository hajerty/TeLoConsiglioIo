using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TeLoConsiglio.Domain.Entities;
using TeLoConsiglio.Infrastructure.Data;

namespace TeLoConsiglio.Infrastructure.Services;

/// <summary>
/// Invia notifiche email su eventi applicativi.
/// Le operazioni sono best-effort: gli errori SMTP vengono loggati ma non rilanciano eccezioni,
/// in modo da non bloccare mai l'azione utente principale.
/// </summary>
public class EmailNotificationService : INotificationService
{
    private readonly AppDbContext _db;
    private readonly IEmailSender _email;
    private readonly UserManager<ApplicationUser> _users;
    private readonly ILogger<EmailNotificationService> _logger;
    private readonly string _frontendUrl;

    public EmailNotificationService(
        AppDbContext db,
        IEmailSender email,
        UserManager<ApplicationUser> users,
        IConfiguration config,
        ILogger<EmailNotificationService> logger)
    {
        _db = db;
        _email = email;
        _users = users;
        _logger = logger;
        _frontendUrl = config["FRONTEND_URL"]
            ?? config["Frontend:Url"]
            ?? "http://localhost:8080";
    }

    // ── Assegnato a punto ODG ────────────────────────────────────────────────────

    public async Task NotifyAssignedToAgendaAsync(string userId, Guid sittingId, Guid agendaItemId, CancellationToken ct = default)
    {
        try
        {
            var user = await _users.FindByIdAsync(userId);
            if (user == null || !user.EmailNotificationsEnabled || string.IsNullOrWhiteSpace(user.Email))
                return;

            var item = await _db.AgendaItems
                .Include(i => i.Sitting)
                .FirstOrDefaultAsync(i => i.Id == agendaItemId && i.SittingId == sittingId, ct);
            if (item?.Sitting == null) return;

            var descTruncated = Truncate(item.Descrizione, 60);
            var dataIt = item.Sitting.Data.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
            var link = $"{_frontendUrl}/sedute/{sittingId}";

            var subject = $"Assegnazione al punto ODG: {descTruncated}";
            var body = $@"<p>Ciao {EscapeHtml(user.FullName)},</p>
<p>sei stato assegnato come responsabile del punto <strong>«{EscapeHtml(item.Descrizione)}»</strong>
della seduta <strong>«{EscapeHtml(item.Sitting.Titolo)}»</strong>
del {dataIt}, presso {EscapeHtml(item.Sitting.Luogo)}.</p>
<p><a href=""{link}"">Apri la seduta</a></p>
<hr><small>TeLoConsiglio.io — <a href=""{_frontendUrl}/profilo/notifiche"">gestisci le notifiche</a></small>";

            await SendSafeAsync(user.Email, subject, body, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NotifyAssignedToAgendaAsync: errore inatteso per userId={UserId} agendaItemId={ItemId}", userId, agendaItemId);
        }
    }

    // ── Decisione cambiata su punto ODG ─────────────────────────────────────────

    public async Task NotifyAgendaDecisionChangedAsync(Guid sittingId, Guid agendaItemId, string oldDecision, string newDecision, CancellationToken ct = default)
    {
        try
        {
            var item = await _db.AgendaItems
                .Include(i => i.Sitting)
                .Include(i => i.Assignments)
                    .ThenInclude(a => a.User)
                .FirstOrDefaultAsync(i => i.Id == agendaItemId && i.SittingId == sittingId, ct);
            if (item?.Sitting == null) return;

            var recipients = item.Assignments
                .Where(a => a.User != null && a.User.EmailNotificationsEnabled && !string.IsNullOrWhiteSpace(a.User.Email))
                .Select(a => a.User!)
                .ToList();
            if (recipients.Count == 0) return;

            var link = $"{_frontendUrl}/sedute/{sittingId}";
            var subject = $"Decisione aggiornata su punto ODG: {Truncate(item.Descrizione, 60)}";

            var tasks = recipients.Select(u =>
            {
                var body = $@"<p>Ciao {EscapeHtml(u.FullName)},</p>
<p>la decisione del punto <strong>«{EscapeHtml(item.Descrizione)}»</strong>
è cambiata da <em>{EscapeHtml(oldDecision)}</em> a <strong>{EscapeHtml(newDecision)}</strong>.</p>
<p><a href=""{link}"">Apri la seduta</a></p>
<hr><small>TeLoConsiglio.io — <a href=""{_frontendUrl}/profilo/notifiche"">gestisci le notifiche</a></small>";
                return SendSafeAsync(u.Email!, subject, body, ct);
            });

            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NotifyAgendaDecisionChangedAsync: errore inatteso per agendaItemId={ItemId}", agendaItemId);
        }
    }

    // ── Nuovo documento su punto ODG ────────────────────────────────────────────

    public async Task NotifyNewDocumentOnAgendaAsync(Guid sittingId, Guid agendaItemId, string documentName, CancellationToken ct = default)
    {
        try
        {
            var item = await _db.AgendaItems
                .Include(i => i.Sitting)
                .Include(i => i.Assignments)
                    .ThenInclude(a => a.User)
                .FirstOrDefaultAsync(i => i.Id == agendaItemId && i.SittingId == sittingId, ct);
            if (item?.Sitting == null) return;

            var recipients = item.Assignments
                .Where(a => a.User != null && a.User.EmailNotificationsEnabled && !string.IsNullOrWhiteSpace(a.User.Email))
                .Select(a => a.User!)
                .ToList();
            if (recipients.Count == 0) return;

            var link = $"{_frontendUrl}/sedute/{sittingId}";
            var subject = $"Nuovo documento sul punto ODG: {Truncate(item.Descrizione, 60)}";

            var tasks = recipients.Select(u =>
            {
                var body = $@"<p>Ciao {EscapeHtml(u.FullName)},</p>
<p>è stato caricato il documento <strong>«{EscapeHtml(documentName)}»</strong>
sul punto <strong>«{EscapeHtml(item.Descrizione)}»</strong>
della seduta <strong>«{EscapeHtml(item.Sitting!.Titolo)}»</strong>.</p>
<p><a href=""{link}"">Apri la seduta</a></p>
<hr><small>TeLoConsiglio.io — <a href=""{_frontendUrl}/profilo/notifiche"">gestisci le notifiche</a></small>";
                return SendSafeAsync(u.Email!, subject, body, ct);
            });

            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NotifyNewDocumentOnAgendaAsync: errore inatteso per agendaItemId={ItemId}", agendaItemId);
        }
    }

    // ── Atto depositato ──────────────────────────────────────────────────────────

    public async Task NotifyActDepositedAsync(Guid actId, CancellationToken ct = default)
    {
        try
        {
            var act = await _db.Acts
                .Include(a => a.Owner)
                .FirstOrDefaultAsync(a => a.Id == actId, ct);
            if (act?.Owner == null) return;

            // Destinatari: utenti con ruolo Capogruppo AND stesso Gruppo dell'autore
            var gruppo = act.Owner.Gruppo;
            if (string.IsNullOrWhiteSpace(gruppo))
            {
                _logger.LogDebug("NotifyActDepositedAsync: autore actId={ActId} non ha Gruppo impostato, skip.", actId);
                return;
            }

            var capigruppo = await _users.GetUsersInRoleAsync(Roles.Capogruppo);
            var recipients = capigruppo
                .Where(u => u.EmailNotificationsEnabled
                    && !string.IsNullOrWhiteSpace(u.Email)
                    && string.Equals(u.Gruppo, gruppo, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (recipients.Count == 0) return;

            var tipoLabel = act.Tipo switch
            {
                Domain.Entities.ActType.Mozione => "mozione",
                Domain.Entities.ActType.OrdineDelGiorno => "ordine del giorno",
                Domain.Entities.ActType.Delibera => "delibera",
                Domain.Entities.ActType.Emendamento => "emendamento",
                _ => act.Tipo.ToString().ToLowerInvariant()
            };

            var link = $"{_frontendUrl}/atti/{actId}";
            var subject = $"Atto depositato: {Truncate(act.Titolo, 60)}";

            var tasks = recipients.Select(u =>
            {
                var body = $@"<p>Ciao {EscapeHtml(u.FullName)},</p>
<p>il consigliere <strong>{EscapeHtml(act.Owner!.FullName)}</strong> ha depositato un {tipoLabel}
intitolato <strong>«{EscapeHtml(act.Titolo)}»</strong>, oggetto: «{EscapeHtml(act.Oggetto)}».</p>
<p><a href=""{link}"">Apri l'atto</a></p>
<hr><small>TeLoConsiglio.io — <a href=""{_frontendUrl}/profilo/notifiche"">gestisci le notifiche</a></small>";
                return SendSafeAsync(u.Email!, subject, body, ct);
            });

            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NotifyActDepositedAsync: errore inatteso per actId={ActId}", actId);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private async Task SendSafeAsync(string to, string subject, string htmlBody, CancellationToken ct)
    {
        try
        {
            await _email.SendAsync(to, subject, htmlBody, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Invio email fallito verso {To} — subject: {Subject}", to, subject);
        }
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s.Substring(0, max) + "…";

    private static string EscapeHtml(string? s) =>
        System.Web.HttpUtility.HtmlEncode(s ?? "");
}
