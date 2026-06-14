using System.Text;
using System.Text.Json;
using Markdig;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TeLoConsiglio.Api.Auth;
using TeLoConsiglio.Api.Dtos;
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
    private readonly IDocumentTextExtractor _extractor;
    private readonly IWebHostEnvironment _env;
    private readonly IFileEncryptor _fileEncryptor;
    private readonly INotificationService _notifications;

    public ActsController(
        AppDbContext db,
        UserManager<ApplicationUser> users,
        IAIService ai,
        ILogger<ActsController> logger,
        IDocumentTextExtractor extractor,
        IWebHostEnvironment env,
        IFileEncryptor fileEncryptor,
        INotificationService notifications)
    {
        _db = db; _users = users; _ai = ai; _logger = logger; _extractor = extractor; _env = env; _fileEncryptor = fileEncryptor; _notifications = notifications;
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
            BodyMd = dto.BodyMd ?? "",
            ReferenceUrlsJson = JsonSerializer.Serialize(dto.ReferenceUrls ?? new List<string>()),
            ReferenceNotesMd = dto.ReferenceNotesMd
        };
        _db.Acts.Add(a);
        await _db.SaveChangesAsync();

        // Notifica se l'atto viene creato già in stato Depositato
        if (a.Status == ActStatus.Depositato)
        {
            try { await _notifications.NotifyActDepositedAsync(a.Id); }
            catch (Exception ex) { _logger.LogError(ex, "Errore notifica atto depositato actId={ActId}", a.Id); }
        }

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
        var oldStatus = a.Status;
        a.Titolo = dto.Titolo;
        a.Oggetto = dto.Oggetto;
        a.ContextNotes = dto.ContextNotes;
        a.BodyMd = dto.BodyMd;
        a.Status = dto.Status;
        a.ReferenceUrlsJson = JsonSerializer.Serialize(dto.ReferenceUrls ?? new List<string>());
        a.ReferenceNotesMd = dto.ReferenceNotesMd;
        a.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Notifica se lo stato transita verso Depositato
        if (oldStatus != ActStatus.Depositato && dto.Status == ActStatus.Depositato)
        {
            try { await _notifications.NotifyActDepositedAsync(a.Id); }
            catch (Exception ex) { _logger.LogError(ex, "Errore notifica atto depositato actId={ActId}", a.Id); }
        }

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

    // ─── Attachments ────────────────────────────────────────────────────────────

    [HttpPost("{id:guid}/attachments")]
    [RequestSizeLimit(UploadValidator.MaxSizeBytes)]
    public async Task<ActionResult<ActAttachmentDto>> UploadAttachment(Guid id, IFormFile file)
    {
        var uid = GetUserId();
        var act = await _db.Acts.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == uid);
        if (act == null) return NotFound();
        if (file == null) return BadRequest(new { error = "File mancante" });

        var (ok, error, ext) = UploadValidator.Validate(file);
        if (!ok) return BadRequest(new { error });

        var dir = Path.Combine(_env.ContentRootPath, "uploads", "act-attachments", uid);
        Directory.CreateDirectory(dir);
        var safe = $"{Guid.NewGuid()}{ext}";
        var path = Path.Combine(dir, safe);
        var originalSafe = Path.GetFileName(file.FileName ?? "");
        string text;
        if (_fileEncryptor.IsEnabled)
        {
            await _fileEncryptor.EncryptToFileAsync(file.OpenReadStream(), path);
            using var decryptedStream = await _fileEncryptor.OpenDecryptedReadAsync(path);
            text = await _extractor.ExtractTextFromStreamAsync(decryptedStream, originalSafe);
        }
        else
        {
            using (var s = System.IO.File.Create(path)) await file.CopyToAsync(s);
            text = await _extractor.ExtractTextAsync(path, originalSafe);
        }
        const int maxExtractedChars = 200_000;
        if (text.Length > maxExtractedChars)
            text = text.Substring(0, maxExtractedChars) + "\n[... testo troncato ...]";

        var att = new ActAttachment
        {
            ActId = id,
            FilePath = path,
            OriginalName = originalSafe,
            ContentType = file.ContentType ?? "application/octet-stream",
            SizeBytes = file.Length,
            ExtractedText = text
        };
        _db.ActAttachments.Add(att);
        await _db.SaveChangesAsync();

        return Ok(new ActAttachmentDto(att.Id, att.OriginalName, att.ContentType, att.SizeBytes, att.CreatedAt));
    }

    [HttpGet("{id:guid}/attachments")]
    public async Task<ActionResult<List<ActAttachmentDto>>> ListAttachments(Guid id)
    {
        var uid = GetUserId();
        var act = await _db.Acts.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == uid);
        if (act == null) return NotFound();

        var attachments = await _db.ActAttachments
            .Where(a => a.ActId == id)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync();

        return Ok(attachments.Select(a => new ActAttachmentDto(a.Id, a.OriginalName, a.ContentType, a.SizeBytes, a.CreatedAt)).ToList());
    }

    [HttpGet("{id:guid}/attachments/{attId:guid}/download")]
    public async Task<IActionResult> DownloadAttachment(Guid id, Guid attId)
    {
        var uid = GetUserId();
        var act = await _db.Acts.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == uid);
        if (act == null) return NotFound();

        var att = await _db.ActAttachments.FirstOrDefaultAsync(a => a.Id == attId && a.ActId == id);
        if (att == null || !System.IO.File.Exists(att.FilePath)) return NotFound();

        Stream stream = _fileEncryptor.IsLikelyEncrypted(att.FilePath)
            ? await _fileEncryptor.OpenDecryptedReadAsync(att.FilePath)
            : System.IO.File.OpenRead(att.FilePath);
        return File(stream, att.ContentType, att.OriginalName);
    }

    [HttpDelete("{id:guid}/attachments/{attId:guid}")]
    public async Task<IActionResult> DeleteAttachment(Guid id, Guid attId)
    {
        var uid = GetUserId();
        var act = await _db.Acts.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == uid);
        if (act == null) return NotFound();

        var att = await _db.ActAttachments.FirstOrDefaultAsync(a => a.Id == attId && a.ActId == id);
        if (att == null) return NotFound();

        try { if (System.IO.File.Exists(att.FilePath)) System.IO.File.Delete(att.FilePath); } catch { }
        _db.ActAttachments.Remove(att);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ─── PDF Export ─────────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> ExportPdf(Guid id)
    {
        var uid = GetUserId();
        var user = await _users.FindByIdAsync(uid);
        var a = await _db.Acts
            .Include(x => x.LegalReferences)
            .FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == uid);
        if (a == null) return NotFound();

        var tipoNome = a.Tipo switch
        {
            ActType.Mozione => "MOZIONE",
            ActType.OrdineDelGiorno => "ORDINE DEL GIORNO",
            ActType.Delibera => "DELIBERA",
            ActType.Emendamento => "EMENDAMENTO",
            _ => a.Tipo.ToString().ToUpper()
        };

        var displayName = user?.FullName ?? "Consigliere";
        var partito = user?.Partito ?? "";
        var comune = user?.Comune ?? "";
        var dataOggi = a.UpdatedAt.ToString("dd/MM/yyyy");

        // Convert markdown to plain text for PDF body (strip markdown syntax)
        var plainBody = MarkdownToPlain(a.BodyMd);

        var titleSlug = SlugifyTitle(a.Titolo);
        var fileName = $"atto-{titleSlug}.pdf";

        var pdfBytes = QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontFamily("Times New Roman").FontSize(11));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text("TeLoConsiglio.io")
                            .Bold().FontSize(14).FontColor(Color.FromHex("#1d4ed8"));
                        row.ConstantItem(120).AlignRight().Text(dataOggi)
                            .FontSize(10).FontColor(Color.FromHex("#6b7280"));
                    });
                    col.Item().PaddingTop(4).Text($"{tipoNome}: {a.Titolo}")
                        .Bold().FontSize(13);
                    col.Item().PaddingTop(2).LineHorizontal(1).LineColor(Color.FromHex("#e5e7eb"));
                });

                page.Content().PaddingTop(12).Column(col =>
                {
                    if (!string.IsNullOrWhiteSpace(a.Oggetto))
                    {
                        col.Item().Text($"Oggetto: {a.Oggetto}").Bold().FontSize(11);
                        col.Item().PaddingTop(8);
                    }
                    col.Item().Text(plainBody).FontSize(11).LineHeight(1.4f);
                });

                page.Footer().Column(col =>
                {
                    col.Item().LineHorizontal(1).LineColor(Color.FromHex("#e5e7eb"));
                    col.Item().PaddingTop(4).Row(row =>
                    {
                        row.RelativeItem().DefaultTextStyle(s => s.FontSize(9).FontColor(Color.FromHex("#6b7280"))).Text(txt =>
                        {
                            txt.Span($"{displayName}").Bold();
                            if (!string.IsNullOrWhiteSpace(partito)) txt.Span($" — {partito}");
                            if (!string.IsNullOrWhiteSpace(comune)) txt.Span($" — Comune di {comune}");
                        });
                        row.ConstantItem(60).AlignRight().DefaultTextStyle(s => s.FontSize(9).FontColor(Color.FromHex("#6b7280"))).Text(txt =>
                        {
                            txt.CurrentPageNumber();
                            txt.Span(" / ");
                            txt.TotalPages();
                        });
                    });
                });
            });
        }).GeneratePdf();

        return File(pdfBytes, "application/pdf", fileName);
    }

    // ─── AI Endpoints ────────────────────────────────────────────────────────────

    [HttpPost("{id:guid}/generate-draft")]
    [HttpPost("{id:guid}/ai-draft")]
    [BudgetGuard]
    public async Task<ActionResult<GenerateDraftResponse>> GenerateDraft(Guid id, [FromBody] GenerateDraftRequest req)
    {
        if (!_ai.IsConfigured)
            return StatusCode(503, new { error = "Servizio AI non configurato (GEMINI_API_KEY mancante)." });

        var uid = GetUserId();
        var a = await _db.Acts
            .Include(x => x.Attachments)
            .FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == uid);
        if (a == null) return NotFound();

        var profile = await _db.PoliticalProfiles.FirstOrDefaultAsync(p => p.UserId == uid);
        var programs = await _db.ElectoralPrograms.Where(p => p.UserId == uid).OrderByDescending(p => p.UploadedAt).Take(1).ToListAsync();
        var argomentiText = profile != null
            ? string.Join(", ", JsonSerializer.Deserialize<List<string>>(profile.ArgomentiFortiJson) ?? new())
            : "";
        var temiText = profile != null
            ? string.Join(", ", JsonSerializer.Deserialize<List<string>>(profile.TemiInteresseJson) ?? new())
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

        // Build attachments context
        var attachmentsText = BuildAttachmentsContext(a.Attachments, maxTotalChars: 30_000);

        // Build reference URLs/notes context
        var refUrls = JsonSerializer.Deserialize<List<string>>(a.ReferenceUrlsJson) ?? new();
        var refsSection = BuildReferencesContext(refUrls, a.ReferenceNotesMd);

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

ARGOMENTI FORTI: {argomentiText}
TEMI DI INTERESSE: {temiText}

ESTRATTO DAL PROGRAMMA ELETTORALE:
{progText}";

        var volatileSystem = "Redigi l'atto richiesto dall'utente attenendoti al contesto politico fornito.";

        var user = $@"Redigi una bozza di {tipoNome} con i seguenti elementi.

TITOLO: {a.Titolo}
OGGETTO: {a.Oggetto}
NOTE/CONTESTO: {a.ContextNotes ?? "(nessuna)"}
{parentInfo}{attachmentsText}{refsSection}

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
        var a = await _db.Acts
            .Include(x => x.Attachments)
            .FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == uid);
        if (a == null) return NotFound();

        var text = string.IsNullOrWhiteSpace(req.Text) ? a.BodyMd : req.Text;
        if (string.IsNullOrWhiteSpace(text)) return BadRequest(new { error = "Testo vuoto." });

        var attachmentsText = BuildAttachmentsContext(a.Attachments, maxTotalChars: 30_000);
        var refUrls = JsonSerializer.Deserialize<List<string>>(a.ReferenceUrlsJson) ?? new();
        var refsSection = BuildReferencesContext(refUrls, a.ReferenceNotesMd);

        var system = "Sei un giurista esperto di diritto degli enti locali italiani. Identifica i riferimenti normativi pertinenti al testo fornito. " +
                     "Rispondi SOLO con un oggetto JSON valido secondo lo schema indicato. Italiano.";
        var userMsg = $@"Analizza il seguente testo di atto comunale e suggerisci i riferimenti normativi pertinenti (TUEL D.Lgs. 267/2000, Costituzione, leggi statali, leggi regionali, statuto comunale, regolamenti). Per ciascuno indica una citazione precisa (es. ""art. 42 D.Lgs. 267/2000"") e una breve motivazione del perché è pertinente.

TESTO:
---
{text}
---
{attachmentsText}{refsSection}

Restituisci ESCLUSIVAMENTE JSON nella forma:
{{
  ""references"": [
    {{ ""citation"": ""..."", ""description"": ""..."" }},
    ...
  ]
}}";

        try
        {
            var result = await _ai.CompleteWithUsageAsync(system, userMsg, maxTokens: 2000);
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

    // ─── Helpers ─────────────────────────────────────────────────────────────────

    private static ActDetailDto ToDetail(Act a)
    {
        var refUrls = JsonSerializer.Deserialize<List<string>>(a.ReferenceUrlsJson ?? "[]") ?? new List<string>();
        return new ActDetailDto(
            a.Id, a.Tipo, a.Titolo, a.Oggetto, a.ContextNotes, a.BodyMd, a.Status, a.ParentActId,
            a.CreatedAt, a.UpdatedAt,
            refUrls,
            a.ReferenceNotesMd,
            a.Revisions.OrderByDescending(r => r.CreatedAt).Select(r => new ActRevisionDto(r.Id, r.BodyMd, r.CreatedAt, r.AuthorId)).ToList(),
            a.LegalReferences.OrderByDescending(r => r.CreatedAt).Select(r => new LegalReferenceDto(r.Id, r.Citation, r.Description, r.Inserted, r.ConfirmedAt)).ToList()
        );
    }

    private static string BuildAttachmentsContext(ICollection<ActAttachment> attachments, int maxTotalChars)
    {
        if (attachments == null || !attachments.Any()) return "";
        var sb = new StringBuilder();
        sb.AppendLine("\n\nALLEGATI ALL'ATTO:");
        int used = 0;
        foreach (var att in attachments)
        {
            if (string.IsNullOrWhiteSpace(att.ExtractedText)) continue;
            var remaining = maxTotalChars - used;
            if (remaining <= 0) break;
            var chunk = att.ExtractedText.Length > remaining
                ? att.ExtractedText.Substring(0, remaining) + "\n[... troncato ...]"
                : att.ExtractedText;
            sb.AppendLine($"\n--- ALLEGATO: {att.OriginalName} ---");
            sb.AppendLine(chunk);
            used += chunk.Length;
        }
        return sb.ToString();
    }

    private static string BuildReferencesContext(List<string> urls, string? notesMd)
    {
        if ((urls == null || !urls.Any()) && string.IsNullOrWhiteSpace(notesMd)) return "";
        var sb = new StringBuilder();
        sb.AppendLine("\n\nRIFERIMENTI E LINK DI SPUNTO:");
        if (urls != null && urls.Any())
        {
            foreach (var url in urls)
                sb.AppendLine($"- {url}");
        }
        if (!string.IsNullOrWhiteSpace(notesMd))
        {
            sb.AppendLine("\nNote riferimenti:");
            sb.AppendLine(notesMd);
        }
        return sb.ToString();
    }

    private static string MarkdownToPlain(string md)
    {
        if (string.IsNullOrWhiteSpace(md)) return "";
        // Use Markdig to strip markdown to plain text
        var pipeline = new MarkdownPipelineBuilder().Build();
        var plain = Markdown.ToPlainText(md, pipeline);
        return plain;
    }

    private static string SlugifyTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "atto";
        var slug = title.ToLowerInvariant();
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[àáâãäå]", "a");
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[èéêë]", "e");
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[ìíîï]", "i");
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[òóôõö]", "o");
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[ùúûü]", "u");
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[\s-]+", "-").Trim('-');
        return slug.Length > 60 ? slug.Substring(0, 60).TrimEnd('-') : slug;
    }

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
