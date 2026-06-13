using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TeLoConsiglio.Api.Auth;
using TeLoConsiglio.Api.Dtos;
using TeLoConsiglio.Domain.Entities;
using TeLoConsiglio.Infrastructure.Data;
using TeLoConsiglio.Infrastructure.Services;

namespace TeLoConsiglio.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/sittings")]
public class SittingsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IDocumentTextExtractor _extractor;
    private readonly IWebHostEnvironment _env;

    public SittingsController(AppDbContext db, UserManager<ApplicationUser> users, IDocumentTextExtractor extractor, IWebHostEnvironment env)
    {
        _db = db;
        _users = users;
        _extractor = extractor;
        _env = env;
    }

    private string? GetUserId() => _users.GetUserId(User);

    [HttpGet]
    public async Task<ActionResult<List<SittingListDto>>> List()
    {
        var uid = GetUserId();
        if (uid == null) return Unauthorized();
        var list = await _db.Sittings
            .Where(s => s.CreatedById == uid)
            .OrderByDescending(s => s.Data)
            .ToListAsync();
        return Ok(list.Select(s => new SittingListDto(s.Id, s.Data, s.Luogo, s.Titolo)).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SittingDetailDto>> Get(Guid id)
    {
        var uid = GetUserId();
        if (uid == null) return Unauthorized();
        var s = await _db.Sittings
            .Include(x => x.AgendaItems.OrderBy(i => i.Ordine))
                .ThenInclude(i => i.Assignments)
                .ThenInclude(a => a.User)
            .FirstOrDefaultAsync(x => x.Id == id && x.CreatedById == uid);
        if (s == null) return NotFound();
        return Ok(ToDetail(s));
    }

    [HttpPost]
    public async Task<ActionResult<SittingDetailDto>> Create([FromBody] SittingCreateDto dto)
    {
        var uid = GetUserId();
        if (uid == null) return Unauthorized();
        var s = new Sitting
        {
            Data = DateTime.SpecifyKind(dto.Data, DateTimeKind.Utc),
            Luogo = dto.Luogo,
            Titolo = dto.Titolo,
            CreatedById = uid
        };
        _db.Sittings.Add(s);
        await _db.SaveChangesAsync();
        return Ok(ToDetail(s));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var uid = GetUserId();
        if (uid == null) return Unauthorized();
        var s = await _db.Sittings.FirstOrDefaultAsync(x => x.Id == id && x.CreatedById == uid);
        if (s == null) return NotFound();
        _db.Sittings.Remove(s);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:guid}/agenda")]
    public async Task<ActionResult<AgendaItemDto>> AddAgendaItem(Guid id, [FromBody] AgendaItemCreateDto dto)
    {
        var uid = GetUserId();
        if (uid == null) return Unauthorized();
        var s = await _db.Sittings.FirstOrDefaultAsync(x => x.Id == id && x.CreatedById == uid);
        if (s == null) return NotFound();
        var item = new AgendaItem
        {
            SittingId = s.Id,
            Ordine = dto.Ordine,
            Descrizione = dto.Descrizione,
            Decisione = dto.Decisione,
            Motivazione = dto.Motivazione ?? "",
            ActId = dto.ActId
        };
        _db.AgendaItems.Add(item);
        if (dto.AssignedUserIds != null)
        {
            foreach (var aid in dto.AssignedUserIds.Distinct())
            {
                item.Assignments.Add(new AgendaItemAssignment { AgendaItemId = item.Id, UserId = aid });
            }
        }
        await _db.SaveChangesAsync();
        await _db.Entry(item).Collection(i => i.Assignments).Query().Include(a => a.User).LoadAsync();
        return Ok(ToItemDto(item));
    }

    [HttpPut("agenda/{itemId:guid}")]
    public async Task<ActionResult<AgendaItemDto>> UpdateAgendaItem(Guid itemId, [FromBody] AgendaItemUpdateDto dto)
    {
        var uid = GetUserId();
        if (uid == null) return Unauthorized();
        var item = await _db.AgendaItems
            .Include(i => i.Assignments)
            .Include(i => i.Sitting)
            .FirstOrDefaultAsync(x => x.Id == itemId);
        if (item == null || item.Sitting == null || item.Sitting.CreatedById != uid) return NotFound();
        item.Ordine = dto.Ordine;
        item.Descrizione = dto.Descrizione;
        item.Decisione = dto.Decisione;
        item.Motivazione = dto.Motivazione ?? "";
        item.ActId = dto.ActId;
        if (dto.AssignedUserIds != null)
        {
            _db.AgendaItemAssignments.RemoveRange(item.Assignments);
            foreach (var aid in dto.AssignedUserIds.Distinct())
            {
                item.Assignments.Add(new AgendaItemAssignment { AgendaItemId = item.Id, UserId = aid });
            }
        }
        await _db.SaveChangesAsync();
        await _db.Entry(item).Collection(i => i.Assignments).Query().Include(a => a.User).LoadAsync();
        return Ok(ToItemDto(item));
    }

    [HttpDelete("agenda/{itemId:guid}")]
    public async Task<IActionResult> DeleteAgendaItem(Guid itemId)
    {
        var uid = GetUserId();
        if (uid == null) return Unauthorized();
        var item = await _db.AgendaItems
            .Include(i => i.Sitting)
            .FirstOrDefaultAsync(x => x.Id == itemId);
        if (item == null || item.Sitting == null || item.Sitting.CreatedById != uid) return NotFound();
        _db.AgendaItems.Remove(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Carica un documento specifico per un punto ODG.
    /// Crea un Document (Type=Documento), estrae il testo e collega AgendaItem.DocumentId.
    /// </summary>
    [HttpPost("agenda/{itemId:guid}/document")]
    [RequestSizeLimit(UploadValidator.MaxSizeBytes)]
    public async Task<ActionResult<AgendaItemDto>> UploadAgendaDocument(Guid itemId, IFormFile file)
    {
        var uid = GetUserId();
        if (uid == null) return Unauthorized();

        var item = await _db.AgendaItems
            .Include(i => i.Assignments)
            .Include(i => i.Sitting)
            .FirstOrDefaultAsync(x => x.Id == itemId);
        if (item == null || item.Sitting == null || item.Sitting.CreatedById != uid) return NotFound();

        if (file == null) return BadRequest(new { error = "File mancante" });
        var (ok, error, ext) = UploadValidator.Validate(file);
        if (!ok) return BadRequest(new { error });

        var dir = Path.Combine(_env.ContentRootPath, "uploads", "documents", uid);
        Directory.CreateDirectory(dir);
        var safe = $"{Guid.NewGuid()}{ext}";
        var path = Path.Combine(dir, safe);
        using (var s = System.IO.File.Create(path)) await file.CopyToAsync(s);
        var originalSafe = Path.GetFileName(file.FileName ?? "");
        var text = await _extractor.ExtractTextAsync(path, originalSafe);
        const int maxExtractedChars = 1_000_000;
        if (text.Length > maxExtractedChars)
            text = text.Substring(0, maxExtractedChars) + "\n[... testo troncato ...]";

        var doc = new Document
        {
            OwnerId = uid,
            FilePath = path,
            OriginalName = originalSafe,
            ExtractedText = text,
            Type = DocumentType.Documento
        };
        _db.Documents.Add(doc);
        item.DocumentId = doc.Id;
        await _db.SaveChangesAsync();

        await _db.Entry(item).Collection(i => i.Assignments).Query().Include(a => a.User).LoadAsync();
        return Ok(ToItemDto(item));
    }

    /// <summary>
    /// Aggiorna lo stato di un punto ODG.
    /// Solo il creatore della seduta o Admin.
    /// </summary>
    [HttpPut("agenda/{itemId:guid}/status")]
    public async Task<ActionResult<AgendaItemDto>> UpdateAgendaItemStatus(Guid itemId, [FromBody] AgendaItemStatusUpdateDto dto)
    {
        var uid = GetUserId();
        if (uid == null) return Unauthorized();

        var item = await _db.AgendaItems
            .Include(i => i.Assignments)
                .ThenInclude(a => a.User)
            .Include(i => i.Sitting)
            .FirstOrDefaultAsync(x => x.Id == itemId);
        if (item == null || item.Sitting == null || item.Sitting.CreatedById != uid) return NotFound();

        item.Status = dto.Status;
        await _db.SaveChangesAsync();
        return Ok(ToItemDto(item));
    }

    private static SittingDetailDto ToDetail(Sitting s) => new(
        s.Id, s.Data, s.Luogo, s.Titolo,
        s.AgendaItems.OrderBy(i => i.Ordine).Select(ToItemDto).ToList()
    );

    private static AgendaItemDto ToItemDto(AgendaItem i) => new(
        i.Id, i.Ordine, i.Descrizione, i.Decisione, i.Motivazione, i.ActId, i.DocumentId, i.Status,
        i.Assignments.Select(a => new AssignedUserDto(a.UserId, a.User?.Email ?? "", a.User?.FullName ?? "")).ToList()
    );
}
