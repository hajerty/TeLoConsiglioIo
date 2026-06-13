using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TeLoConsiglio.Api.Dtos;
using TeLoConsiglio.Domain.Entities;
using TeLoConsiglio.Infrastructure.Data;

namespace TeLoConsiglio.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public DashboardController(AppDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    private string? GetUserId() => _users.GetUserId(User);

    /// <summary>
    /// Restituisce la dashboard personalizzata dell'utente:
    /// documenti recenti, prossima seduta del Comune, voci ODG da analizzare, contatori.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<DashboardDto>> Get()
    {
        var uid = GetUserId();
        if (uid == null) return Unauthorized();

        var currentUser = await _users.FindByIdAsync(uid);
        if (currentUser == null) return Unauthorized();

        var now = DateTime.UtcNow;

        // ---- 1. Recent documents (top 5) ----
        var recentDocs = await _db.Documents
            .Where(d => d.OwnerId == uid)
            .OrderByDescending(d => d.CreatedAt)
            .Take(5)
            .Select(d => new DashboardDocumentDto(
                d.Id,
                d.OriginalName,
                d.Type,
                d.CreatedAt,
                d.Summary != null))
            .ToListAsync();

        // ---- 2. Next sitting (stesso Comune dell'utente, data >= now) ----
        DashboardNextSittingDto? nextSitting = null;
        if (!string.IsNullOrWhiteSpace(currentUser.Comune))
        {
            var userComune = currentUser.Comune;
            var nextSittingEntity = await _db.Sittings
                .Include(s => s.CreatedBy)
                .Include(s => s.AgendaItems)
                .Where(s => s.Data >= now
                         && s.CreatedBy != null
                         && s.CreatedBy.Comune == userComune)
                .OrderBy(s => s.Data)
                .FirstOrDefaultAsync();

            if (nextSittingEntity != null)
            {
                nextSitting = new DashboardNextSittingDto(
                    nextSittingEntity.Id,
                    nextSittingEntity.Data,
                    nextSittingEntity.Luogo,
                    nextSittingEntity.Titolo,
                    nextSittingEntity.AgendaItems.Count,
                    nextSittingEntity.AgendaItems.Count(i => i.Status == AgendaItemStatus.DaAnalizzare));
            }
        }

        // ---- 3. Documents to analyze (DaAnalizzare in sedute future proprie) ----
        var docsToAnalyze = await _db.AgendaItems
            .Include(i => i.Sitting)
            .Where(i => i.Status == AgendaItemStatus.DaAnalizzare
                     && i.Sitting != null
                     && i.Sitting.CreatedById == uid
                     && i.Sitting.Data >= now)
            .OrderBy(i => i.Sitting!.Data)
            .ThenBy(i => i.Ordine)
            .Select(i => new DashboardAgendaItemDto(
                i.SittingId,
                i.Sitting!.Data,
                i.Sitting.Titolo,
                i.Id,
                i.Descrizione,
                i.DocumentId))
            .ToListAsync();

        // ---- 4. Counters ----
        var actsBozza = await _db.Acts
            .CountAsync(a => a.OwnerId == uid && a.Status == ActStatus.Bozza);

        var upcomingSittings = await _db.Sittings
            .Include(s => s.CreatedBy)
            .Where(s => s.Data >= now
                     && s.CreatedBy != null
                     && !string.IsNullOrWhiteSpace(currentUser.Comune)
                     && s.CreatedBy.Comune == currentUser.Comune)
            .CountAsync();

        // Pending invitations: solo per Capogruppo/Vice/Admin
        int pendingInvitations = 0;
        var roles = await _users.GetRolesAsync(currentUser);
        if (roles.Contains(Roles.Admin) || roles.Contains(Roles.Capogruppo) || roles.Contains(Roles.Vice))
        {
            pendingInvitations = await _db.Invitations
                .CountAsync(i => i.InvitedByUserId == uid
                              && i.ConsumedAt == null
                              && i.RevokedAt == null
                              && i.ExpiresAt > now);
        }

        var counters = new DashboardCountersDto(actsBozza, upcomingSittings, pendingInvitations);

        return Ok(new DashboardDto(recentDocs, nextSitting, docsToAnalyze, counters));
    }
}
