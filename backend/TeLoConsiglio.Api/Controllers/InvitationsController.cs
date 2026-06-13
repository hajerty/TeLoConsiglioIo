using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TeLoConsiglio.Api.Dtos;
using TeLoConsiglio.Domain.Entities;
using TeLoConsiglio.Infrastructure.Data;

namespace TeLoConsiglio.Api.Controllers;

// ---------- DTOs ----------

public record CreateInvitationDto(
    [Required] string Nome,
    [Required] string Cognome,
    [Required, EmailAddress] string Email,
    string? Gruppo,
    string? Comune);

public record InvitationListItemDto(
    Guid Id,
    string Nome,
    string Cognome,
    string Email,
    string Comune,
    string Gruppo,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    string Status);   // Pending | Consumed | Expired | Revoked

public record InvitationPublicDto(
    string Nome,
    string Cognome,
    string Email,
    string Comune,
    string Gruppo,
    DateTime ExpiresAt,
    bool Consumed);

public record CreateInvitationResponseDto(string Token, string Url);

// ---------- Controller ----------

[ApiController]
[Route("api/invitations")]
public class InvitationsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IConfiguration _config;

    public InvitationsController(AppDbContext db, UserManager<ApplicationUser> users, IConfiguration config)
    {
        _db = db;
        _users = users;
        _config = config;
    }

    private string? GetUserId() => _users.GetUserId(User);

    /// <summary>
    /// Crea un invito. Solo Capogruppo, Vice o Admin.
    /// Body: { nome, cognome, email, gruppo?, comune? }
    /// Se gruppo/comune omessi, eredita dall'utente corrente.
    /// </summary>
    [Authorize(Policy = "RequireCapogruppoOrAdmin")]
    [HttpPost]
    public async Task<ActionResult<CreateInvitationResponseDto>> Create([FromBody] CreateInvitationDto dto)
    {
        var uid = GetUserId();
        if (uid == null) return Unauthorized();

        var currentUser = await _users.FindByIdAsync(uid);
        if (currentUser == null) return Unauthorized();

        var gruppo = !string.IsNullOrWhiteSpace(dto.Gruppo) ? dto.Gruppo : currentUser.Gruppo ?? "";
        var comune = !string.IsNullOrWhiteSpace(dto.Comune) ? dto.Comune : currentUser.Comune ?? "";

        var token = Guid.NewGuid().ToString("N");
        var invitation = new Invitation
        {
            Token = token,
            Email = dto.Email,
            Nome = dto.Nome,
            Cognome = dto.Cognome,
            Comune = comune,
            Gruppo = gruppo,
            InvitedByUserId = uid,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(14)
        };

        _db.Invitations.Add(invitation);
        await _db.SaveChangesAsync();

        // TODO: inviare email di invito all'indirizzo dto.Email
        var frontendUrl = _config["FRONTEND_URL"] ?? _config["Frontend__Url"] ?? "http://localhost:5173";
        var url = $"{frontendUrl}/register?invite={token}";

        return Ok(new CreateInvitationResponseDto(token, url));
    }

    /// <summary>
    /// Lista inviti emessi dall'utente corrente (paginata semplice).
    /// Solo Capogruppo, Vice o Admin.
    /// </summary>
    [Authorize(Policy = "RequireCapogruppoOrAdmin")]
    [HttpGet]
    public async Task<ActionResult<List<InvitationListItemDto>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var uid = GetUserId();
        if (uid == null) return Unauthorized();

        // Admin vede tutti; Capogruppo/Vice vedono solo i propri
        var roles = await _users.GetRolesAsync((await _users.FindByIdAsync(uid))!);
        IQueryable<Invitation> query = _db.Invitations;
        if (!roles.Contains(Roles.Admin))
            query = query.Where(i => i.InvitedByUserId == uid);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var now = DateTime.UtcNow;
        var result = items.Select(i => new InvitationListItemDto(
            i.Id,
            i.Nome,
            i.Cognome,
            i.Email,
            i.Comune,
            i.Gruppo,
            i.CreatedAt,
            i.ExpiresAt,
            i.RevokedAt.HasValue ? "Revoked" :
            i.ConsumedAt.HasValue ? "Consumed" :
            i.ExpiresAt < now ? "Expired" : "Pending"
        )).ToList();

        Response.Headers["X-Total-Count"] = total.ToString();
        return Ok(result);
    }

    /// <summary>
    /// Restituisce dati pubblici di un invito (per pre-fillare il form di registrazione).
    /// Pubblico, no auth. 404 se scaduto/consumato/revocato/inesistente.
    /// </summary>
    [HttpGet("{token}")]
    [AllowAnonymous]
    public async Task<ActionResult<InvitationPublicDto>> GetByToken(string token)
    {
        var invitation = await _db.Invitations.FirstOrDefaultAsync(i => i.Token == token);
        if (invitation == null) return NotFound();
        if (invitation.RevokedAt.HasValue) return NotFound();
        if (invitation.ExpiresAt < DateTime.UtcNow) return NotFound();
        // Consumed invitations: return data but mark as consumed (so frontend can show message)
        if (invitation.ConsumedAt.HasValue)
            return Ok(new InvitationPublicDto(
                invitation.Nome, invitation.Cognome, invitation.Email,
                invitation.Comune, invitation.Gruppo, invitation.ExpiresAt, true));

        return Ok(new InvitationPublicDto(
            invitation.Nome, invitation.Cognome, invitation.Email,
            invitation.Comune, invitation.Gruppo, invitation.ExpiresAt, false));
    }

    /// <summary>
    /// Revoca (elimina) un invito. Solo l'emittente o Admin.
    /// </summary>
    [Authorize]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Revoke(Guid id)
    {
        var uid = GetUserId();
        if (uid == null) return Unauthorized();

        var invitation = await _db.Invitations.FirstOrDefaultAsync(i => i.Id == id);
        if (invitation == null) return NotFound();

        var roles = await _users.GetRolesAsync((await _users.FindByIdAsync(uid))!);
        if (invitation.InvitedByUserId != uid && !roles.Contains(Roles.Admin))
            return NotFound(); // Anti-IDOR: 404 non 403

        if (invitation.ConsumedAt.HasValue)
            return BadRequest(new { error = "Impossibile revocare un invito gia' utilizzato." });

        invitation.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
