using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TeLoConsiglio.Api.Dtos;
using TeLoConsiglio.Api.Auth;
using TeLoConsiglio.Domain.Entities;
using TeLoConsiglio.Infrastructure.Data;
using TeLoConsiglio.Infrastructure.Services;

namespace TeLoConsiglio.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/acts")]
public class ActsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IAIService _ai;
    private readonly ILogger<ActsController> _logger;

    public ActsController(AppDbContext db, UserManager<ApplicationUser> users, IAIService ai, ILogger<ActsController> logger)
    {
        _db = db; _users = users; _ai = ai; _logger = logger;
    }

    private string GetUserId() => _users.GetUserId(User)!;

    [HttpGet]
    public async Task<ActionResult<List<ActListDto>>> List([FromQuery] ActType? tipo, [FromQuery] ActStatus? status, [FromQuery] string? q, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var uid = GetUserId();
        IQueryable<Act> query = _db.Acts.Where(a => a.OwnerId == uid);
        if (tipo.HasValue) query = query.Where(a => a.Tipo == tipo.Value);
        if (status.HasValue) query = query.Where(a => a.Status == status.Value);
        if (from.HasValue) query = query.Where(a => a.CreatedAt >= from.Value);
        if (to.HasValue) query = query.Where(a => a.CreatedAt <= to.Value);
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(a => a.Titolo.ToLower().Contains(q.ToLower()) || a.Oggetto.ToLower().Contains(q.ToLower()) || a.BodyMd.ToLower().Contains(q.ToLower()));
        var list = await query.OrderByDescending(a => a.UpdatedAt).ToListAsync();
        return Ok(list.Select(a => new ActListDto(a.Id, a.Tipo, a.Titolo, a.Oggetto, a.Status, a.CreatedAt, a.UpdatedAt)).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ActDetailDto>> Get(Guid id)
    {
        var uid = GetUserId();
        var a = await _db.Acts
            .Include(x => x.Revisions.OrderByDescending(r => r.CreatedAt))
            .Include(x => x.LegalReferences)
            .FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == uid);
        if (a == null) return NotFound();
        return Ok(ToDetail(a));
    }

    [HttpPost]
    public async Task<ActionResult<ActDetailDto>> Create([FromBody] ActCreateDto dto)
    {
        var uid = GetUserId();
        var a = new Act
        {
            OwnerId = uid,
            Tipo = dto.Tipo,
            Titolo = dto.Titolo,
            Oggetto = dto.Oggetto,
            ContextNotes = dto.ContextNotes,
            ParentActId = dto.ParentActId,
            BodyMd = dto.BodyMd ?? ""
        };
        _db.Acts.Add(a);
        await _db.SaveChangesAsync();
        return Ok(ToDetail(a));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ActDetailDto>> Update(Guid id, [FromBody] ActUpdateDto dto)
    {
        var uid = GetUserId();
        var a = await _db.Acts.Include(x => x.Revisions).Include(x => x.LegalReferences).FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == uid);
        if (a == null) return NotFound();
        if (a.BodyMd != dto.BodyMd)
        {
            _db.ActRevisions.Add(new ActRevision { ActId = a.Id, BodyMd = a.BodyMd, AuthorId = uid });
        }
        a.Titolo = dto.Titolo;
        a.Oggetto = dto.Oggetto;
        a.ContextNotes = dto.ContextNotes;
        a.BodyMd = dto.BodyMd;
        a.Status = dto.Status;
        a.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(ToDetail(a));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var uid = GetUserId();
        var a = await _db.Acts.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == uid);
        if (a == null) return NotFound();
        _db.Acts.Remove(a);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:guid}/generate-draft")]
    [HttpPost("{id:guid}/ai-draft")]
    [BudgetGuard]
    public async Task<ActionResult<GenerateDraftResponse>> GenerateDraft(Guid id, [FromBody] GenerateDraftRequest req)
    {
        if (!_ai.IsConfigured)
            return StatusCode(503, new { error = "Servizio AI non configurato (GEMINI_API_KEY mancante)." });

        var uid = GetUserId();
        var a = await _db.Acts.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == uid);
        if (a == null) return NotFound();

        var profile = await _db.PoliticalProfiles.FirstOrDefaultAsync(p => p.UserId == uid);
        var programs = await _db.ElectoralPrograms.Where(p => p.UserId == uid).OrderByDescending(p => p.UploadedAt).Take(1).ToListAsync();
        var puntiText = profile != null
            ? string.Join(", ", JsonSerializer.Deserialize<List<string>>(profile.PuntiEvidenzaJson) ?? new())
            : "";
        var progText = programs.FirstOrDefault()?.ExtractedText ?? "";
        if (progText.Length > 8000) progText = progText.Substring(0, 8000) + "\n[... troncato ...]";

        string parentInfo = "";
        if (a.ParentActId.HasValue)
        {
            var parent = await _db.Acts.FirstOrDefaultAsync(p => p.Id == a.ParentActId.Value);
            if (parent != null)
                parentInfo = $"\n\nATTO DI RIFERIMENTO ({parent.Tipo}): {parent.Titolo}\n{parent.BodyMd}\n";
        }

        var tipoNome = a.Tipo switch
        {
            ActType.Mozione => "MOZIONE",
            ActType.OrdineDelGiorno => "ORDINE DEL GIORNO",
            ActType.Delibera => "DELIBERA",
            ActType.Emendamento => "EMENDAMENTO",
            _ => a.Tipo.ToString()
        };

        var cacheableSystem = $@"Sei un esperto redattore di atti amministrativi italiani per consigli comunali, competente in TUEL D.Lgs. 267/2000, Costituzione, leggi regionali e regolamenti consiliari. Scrivi atti formali in italiano corretto, con la struttura tipica delle delibere comunali (Premessa, Visti, Considerato che, Dato atto, Tutto cio' premesso, Impegna/Delibera). Non includere meta-commenti, restituisci SOLO il testo dell'atto in markdown.

LINEA POLITICA DEL CONSIGLIERE:
{profile?.LineaPoliticaMd ?? "(non specificata)"}

PUNTI DA METTERE IN EVIDENZA: {puntiText}

ESTRATTO DAL PROGRAMMA ELETTORALE:
{progText}";

        var volatileSystem = "Redigi l'atto richiesto dall'utente attenendoti al contesto politico fornito.";

        var user = $@"Redigi una bozza di {tipoNome} con i seguenti elementi.

TITOLO: {a.Titolo}
OGGETTO: {a.Oggetto}
NOTE/CONTESTO: {a.ContextNotes ?? "(nessuna)"}
{parentInfo}

ISTRUZIONI AGGIUNTIVE: {req.AdditionalInstructions ?? "(nessuna)"}

Produci ora il testo completo dell'atto in markdown.";

        try
        {
            var result = await _ai.CompleteWithUsageAsync(cacheableSystem, volatileSystem, user, maxTokens: 4000);
            await LogUsageAsync(uid, "acts.ai-draft", result);
            return Ok(new GenerateDraftResponse(result.Text));
        }
        catch (AIQuotaExceededException ex)
        {
            _logger.LogWarning("Gemini quota exceeded: {Msg}", ex.Message);
            return StatusCode(429, new { error = "ai_daily_quota_exceeded", message = "Limite giornaliero AI raggiunto, riprova domani." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore Gemini draft");
            return StatusCode(502, new { error = "Errore AI: " + ex.Message });
        }
    }

    [HttpPost("{id:guid}/suggest-legal-refs")]
    [HttpPost("{id:guid}/legal-refs/suggest")]
    [BudgetGuard]
    public async Task<ActionResult<SuggestLegalRefsResponse>> SuggestLegalRefs(Guid id, [FromBody] SuggestLegalRefsRequest req)
    {
        if (!_ai.IsConfigured)
            return StatusCode(503, new { error = "Servizio AI non configurato." });

        var uid = GetUserId();
        var a = await _db.Acts.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == uid);
        if (a == null) return NotFound();

        var text = string.IsNullOrWhiteSpace(req.Text) ? a.BodyMd : req.Text;
        if (string.IsNullOrWhiteSpace(text)) return BadRequest(new { error = "Testo vuoto." });

        var system = "Sei un giurista esperto di diritto degli enti locali italiani. Identifica i riferimenti normativi pertinenti al testo fornito. " +
                     "Rispondi SOLO con un oggetto JSON valido secondo lo schema indicato. Italiano.";
        var user = $@"Analizza il seguente testo di atto comunale e suggerisci i riferimenti normativi pertinenti (TUEL D.Lgs. 267/2000, Costituzione, leggi statali, leggi regionali, statuto comunale, regolamenti). Per ciascuno indica una citazione precisa (es. ""art. 42 D.Lgs. 267/2000"") e una breve motivazione del perché è pertinente.

TESTO:
---
{text}
---

Restituisci ESCLUSIVAMENTE JSON nella forma:
{{
  ""references"": [
    {{ ""citation"": ""..."", ""description"": ""..."" }},
    ...
  ]
}}";

        try
        {
            var result = await _ai.CompleteWithUsageAsync(system, user, maxTokens: 2000);
            await LogUsageAsync(uid, "acts.legal-refs.suggest", result);
            var raw = result.Text;
            var json = ExtractJson(raw);
            using var doc = JsonDocument.Parse(json);
            var refs = doc.RootElement.GetProperty("references").EnumerateArray()
                .Select(e => new SuggestedLegalRef(
                    e.GetProperty("citation").GetString() ?? "",
                    e.GetProperty("description").GetString() ?? ""))
                .Where(r => !string.IsNullOrWhiteSpace(r.Citation))
                .ToList();

            // Persist as un-confirmed references
            foreach (var r in refs)
            {
                _db.LegalReferences.Add(new LegalReference
                {
                    ActId = a.Id,
                    Citation = r.Citation,
                    Description = r.Description,
                    Inserted = false
                });
            }
            await _db.SaveChangesAsync();

            return Ok(new SuggestLegalRefsResponse(refs));
        }
        catch (AIQuotaExceededException ex)
        {
            _logger.LogWarning("Gemini quota exceeded: {Msg}", ex.Message);
            return StatusCode(429, new { error = "ai_daily_quota_exceeded", message = "Limite giornaliero AI raggiunto, riprova domani." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore Gemini legal-refs");
            return StatusCode(502, new { error = "Errore AI: " + ex.Message });
        }
    }

    [HttpPost("{id:guid}/insert-legal-refs")]
    [HttpPost("{id:guid}/legal-refs/insert")]
    [HttpPost("{id:guid}/legal-refs/confirm")]
    public async Task<ActionResult<ActDetailDto>> InsertLegalRefs(Guid id, [FromBody] InsertLegalRefsRequest req)
    {
        var uid = GetUserId();
        var a = await _db.Acts.Include(x => x.LegalReferences).Include(x => x.Revisions).FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == uid);
        if (a == null) return NotFound();

        var refs = a.LegalReferences.Where(r => req.ReferenceIds.Contains(r.Id)).ToList();
        if (refs.Count == 0) return BadRequest(new { error = "Nessun riferimento selezionato" });

        _db.ActRevisions.Add(new ActRevision { ActId = a.Id, BodyMd = a.BodyMd, AuthorId = uid });

        var block = "\n\n## Riferimenti normativi\n" + string.Join("\n", refs.Select(r => $"- **{r.Citation}** — {r.Description}"));

        if (string.Equals(req.Mode, "placeholder", StringComparison.OrdinalIgnoreCase) && a.BodyMd.Contains("[[REF]]"))
        {
            a.BodyMd = a.BodyMd.Replace("[[REF]]", block.TrimStart('\n'));
        }
        else
        {
            a.BodyMd = (a.BodyMd ?? "") + block;
        }

        foreach (var r in refs)
        {
            r.Inserted = true;
            r.ConfirmedAt = DateTime.UtcNow;
        }
        a.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(ToDetail(a));
    }

    [HttpPost("{id:guid}/legal-refs")]
    public async Task<ActionResult<LegalReferenceDto>> CreateLegalRef(Guid id, [FromBody] CreateLegalRefRequest req)
    {
        var uid = GetUserId();
        var a = await _db.Acts.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == uid);
        if (a == null) return NotFound();
        if (string.IsNullOrWhiteSpace(req.Citation))
            return BadRequest(new { error = "Citation obbligatoria" });

        var r = new LegalReference
        {
            ActId = a.Id,
            Citation = req.Citation.Trim(),
            Description = req.Description?.Trim() ?? "",
            Inserted = false
        };
        _db.LegalReferences.Add(r);
        await _db.SaveChangesAsync();
        return Ok(new LegalReferenceDto(r.Id, r.Citation, r.Description, r.Inserted, r.ConfirmedAt));
    }

    [HttpDelete("legal-refs/{refId:guid}")]
    public async Task<IActionResult> DeleteRef(Guid refId)
    {
        var uid = GetUserId();
        var r = await _db.LegalReferences.Include(x => x.Act).FirstOrDefaultAsync(x => x.Id == refId);
        if (r == null || r.Act == null || r.Act.OwnerId != uid) return NotFound();
        _db.LegalReferences.Remove(r);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static ActDetailDto ToDetail(Act a) => new(
        a.Id, a.Tipo, a.Titolo, a.Oggetto, a.ContextNotes, a.BodyMd, a.Status, a.ParentActId, a.CreatedAt, a.UpdatedAt,
        a.Revisions.OrderByDescending(r => r.CreatedAt).Select(r => new ActRevisionDto(r.Id, r.BodyMd, r.CreatedAt, r.AuthorId)).ToList(),
        a.LegalReferences.OrderByDescending(r => r.CreatedAt).Select(r => new LegalReferenceDto(r.Id, r.Citation, r.Description, r.Inserted, r.ConfirmedAt)).ToList()
    );

    private async Task LogUsageAsync(string userId, string operation, AICompletionResult r)
    {
        _db.UsageLogs.Add(new UsageLog
        {
            UserId = userId,
            Operation = operation,
            Model = r.Model,
            InputTokens = r.InputTokens,
            OutputTokens = r.OutputTokens,
            CachedInputTokens = r.CachedReadTokens,
            EstimatedCostUsd = r.EstimatedCostUsd
        });
        await _db.SaveChangesAsync();
    }

    private static string ExtractJson(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start >= 0 && end > start) return text.Substring(start, end - start + 1);
        return text;
    }
}
