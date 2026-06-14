using System.Text.Json;
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

public enum SittingPeriod { All, Past, Upcoming }

[ApiController]
[Authorize]
[Route("api/sittings")]
public class SittingsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IDocumentTextExtractor _extractor;
    private readonly IWebHostEnvironment _env;
    private readonly IFileEncryptor _fileEncryptor;
    private readonly IAIService _ai;
    private readonly ILogger<SittingsController> _logger;

    public SittingsController(
        AppDbContext db,
        UserManager<ApplicationUser> users,
        IDocumentTextExtractor extractor,
        IWebHostEnvironment env,
        IFileEncryptor fileEncryptor,
        IAIService ai,
        ILogger<SittingsController> logger)
    {
        _db = db;
        _users = users;
        _extractor = extractor;
        _env = env;
        _fileEncryptor = fileEncryptor;
        _ai = ai;
        _logger = logger;
    }

    private string? GetUserId() => _users.GetUserId(User);

    /// <summary>
    /// Elenca le sedute con filtri opzionali: from/to (range data), q (full-text su Titolo+Luogo),
    /// period (Past/Upcoming/All), paginazione con page/pageSize. Header X-Total-Count.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<SittingListDto>>> List(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? q,
        [FromQuery] SittingPeriod period = SittingPeriod.All,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var uid = GetUserId();
        if (uid == null) return Unauthorized();

        var now = DateTime.UtcNow;
        IQueryable<Sitting> query = _db.Sittings.Where(s => s.CreatedById == uid);

        if (from.HasValue)
            query = query.Where(s => s.Data >= from.Value.ToUniversalTime());
        if (to.HasValue)
            query = query.Where(s => s.Data <= to.Value.ToUniversalTime());
        if (!string.IsNullOrWhiteSpace(q))
        {
            var ql = q.ToLower();
            query = query.Where(s => s.Titolo.ToLower().Contains(ql) || s.Luogo.ToLower().Contains(ql));
        }
        if (period == SittingPeriod.Past)
            query = query.Where(s => s.Data < now);
        else if (period == SittingPeriod.Upcoming)
            query = query.Where(s => s.Data >= now);

        var total = await query.CountAsync();
        Response.Headers["X-Total-Count"] = total.ToString();

        var list = await query
            .OrderByDescending(s => s.Data)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
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
        const int maxExtractedChars = 1_000_000;
        if (text.Length > maxExtractedChars)
            text = text.Substring(0, maxExtractedChars) + "\n[... testo troncato ...]";

        var doc = new TeLoConsiglio.Domain.Entities.Document
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

    /// <summary>
    /// Genera e scarica il report PDF di una seduta.
    /// Contiene solo le voci ODG con Status == ApprovataPerSeduta.
    /// </summary>
    [HttpGet("{id:guid}/report.pdf")]
    public async Task<IActionResult> DownloadReport(Guid id)
    {
        var uid = GetUserId();
        if (uid == null) return Unauthorized();

        var s = await _db.Sittings
            .Include(x => x.AgendaItems.OrderBy(i => i.Ordine))
                .ThenInclude(i => i.Assignments)
                .ThenInclude(a => a.User)
            .Include(x => x.AgendaItems)
                .ThenInclude(i => i.Document)
            .FirstOrDefaultAsync(x => x.Id == id && x.CreatedById == uid);
        if (s == null) return NotFound();

        var approvedItems = s.AgendaItems
            .Where(i => i.Status == AgendaItemStatus.ApprovataPerSeduta)
            .OrderBy(i => i.Ordine)
            .ToList();

        var dataIt = s.Data.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);

        var pdfBytes = QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                page.Header().Column(col =>
                {
                    col.Item().Text("Report seduta — TeLoConsiglio.io")
                        .FontSize(16).Bold();
                    col.Item().Text($"Data: {dataIt}   Luogo: {s.Luogo}");
                    col.Item().Text($"Titolo: {s.Titolo}");
                    col.Item().PaddingTop(4).LineHorizontal(1);
                });

                page.Content().PaddingTop(10).Column(col =>
                {
                    if (approvedItems.Count == 0)
                    {
                        col.Item().Text("Nessuna voce approvata per la seduta.")
                            .Italic().FontColor(Colors.Grey.Medium);
                    }
                    else
                    {
                        int num = 1;
                        foreach (var item in approvedItems)
                        {
                            col.Item().PaddingBottom(8).Column(voce =>
                            {
                                voce.Item().Text($"{num}. {item.Descrizione}").Bold();

                                var docName = item.Document?.OriginalName;
                                voce.Item().Text($"Documento allegato: {docName ?? "nessuno"}");

                                var assigned = item.Assignments.Any()
                                    ? string.Join(", ", item.Assignments
                                        .Select(a => !string.IsNullOrWhiteSpace(a.User?.FullName)
                                            ? a.User!.FullName
                                            : a.User?.Email ?? a.UserId))
                                    : "nessuno";
                                voce.Item().Text($"Assegnati: {assigned}");

                                var decisioneIt = item.Decisione switch
                                {
                                    AgendaDecision.Approvare => "Approvare",
                                    AgendaDecision.Respingere => "Respingere",
                                    AgendaDecision.Astenersi => "Astenersi",
                                    _ => "Da decidere"
                                };
                                voce.Item().Text($"Decisione: {decisioneIt}");

                                if (!string.IsNullOrWhiteSpace(item.Motivazione))
                                    voce.Item().Text($"Motivazione: {item.Motivazione}");

                                voce.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
                            });
                            num++;
                        }
                    }
                });

                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text(txt =>
                    {
                        txt.Span($"Generato il {DateTime.Now:dd/MM/yyyy HH:mm} — TeLoConsiglio.io");
                    });
                    row.ConstantItem(50).AlignRight().Text(txt =>
                    {
                        txt.CurrentPageNumber();
                        txt.Span(" / ");
                        txt.TotalPages();
                    });
                });
            });
        }).GeneratePdf();

        var dataFilename = s.Data.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        var filename = $"report-seduta-{id}-{dataFilename}.pdf";
        return File(pdfBytes, "application/pdf", filename);
    }

    /// <summary>
    /// Suggerisce documenti già usati in altri punti ODG dell'utente
    /// con descrizione simile a quella dell'item corrente.
    /// </summary>
    [HttpGet("{id:guid}/agenda/{itemId:guid}/document-suggestions")]
    public async Task<ActionResult<List<DocumentSuggestionDto>>> DocumentSuggestions(Guid id, Guid itemId)
    {
        var uid = GetUserId();
        if (uid == null) return Unauthorized();

        // Ownership check sulla seduta
        var sittingExists = await _db.Sittings.AnyAsync(s => s.Id == id && s.CreatedById == uid);
        if (!sittingExists) return NotFound();

        var currentItem = await _db.AgendaItems
            .FirstOrDefaultAsync(i => i.Id == itemId && i.SittingId == id);
        if (currentItem == null) return NotFound();

        var targetDesc = currentItem.Descrizione ?? "";

        // Tutti gli AgendaItem dell'utente con DocumentId non null, diversi dall'item corrente
        var candidates = await _db.AgendaItems
            .Include(i => i.Sitting)
            .Include(i => i.Document)
            .Where(i => i.Id != itemId
                     && i.DocumentId != null
                     && i.Sitting != null
                     && i.Sitting.CreatedById == uid)
            .ToListAsync();

        var scored = candidates
            .Select(i =>
            {
                var desc = i.Descrizione ?? "";
                int score;
                if (string.Equals(desc, targetDesc, StringComparison.OrdinalIgnoreCase))
                    score = 100;
                else if (targetDesc.Length >= 6 && desc.Length >= 6)
                {
                    var shorter = targetDesc.Length <= desc.Length ? targetDesc : desc;
                    var longer = targetDesc.Length > desc.Length ? targetDesc : desc;
                    score = longer.Contains(shorter, StringComparison.OrdinalIgnoreCase) ? 50 : 0;
                }
                else
                    score = 0;
                return (item: i, score);
            })
            .Where(x => x.score > 0)
            .OrderByDescending(x => x.score)
            .ThenByDescending(x => x.item.Sitting!.Data)
            .Take(5)
            .Select(x => new DocumentSuggestionDto(
                x.item.Id,
                x.item.SittingId,
                x.item.Sitting!.Data,
                x.item.Sitting.Titolo,
                x.item.Descrizione,
                x.item.DocumentId!.Value,
                x.item.Document?.OriginalName ?? "",
                x.score))
            .ToList();

        return Ok(scored);
    }

    /// <summary>
    /// Copia il DocumentId da un AgendaItem sorgente all'item corrente (link, no copia file).
    /// </summary>
    [HttpPost("agenda/{itemId:guid}/clone-document")]
    public async Task<ActionResult<AgendaItemDto>> CloneDocument(Guid itemId, [FromBody] CloneDocumentDto dto)
    {
        var uid = GetUserId();
        if (uid == null) return Unauthorized();

        // Ownership sull'item corrente
        var item = await _db.AgendaItems
            .Include(i => i.Assignments)
                .ThenInclude(a => a.User)
            .Include(i => i.Sitting)
            .FirstOrDefaultAsync(x => x.Id == itemId);
        if (item == null || item.Sitting == null || item.Sitting.CreatedById != uid) return NotFound();

        // Ownership sull'item sorgente
        var sourceItem = await _db.AgendaItems
            .Include(i => i.Sitting)
            .FirstOrDefaultAsync(x => x.Id == dto.SourceAgendaItemId);
        if (sourceItem == null || sourceItem.Sitting == null || sourceItem.Sitting.CreatedById != uid)
            return NotFound();

        if (sourceItem.DocumentId == null)
            return BadRequest(new { error = "L'agenda item sorgente non ha un documento allegato." });

        item.DocumentId = sourceItem.DocumentId;
        await _db.SaveChangesAsync();

        return Ok(ToItemDto(item));
    }

    /// <summary>
    /// Importa una convocazione (PDF/DOCX/TXT) e la parsa via AI.
    /// Restituisce i metadati strutturati (data, luogo, titolo, punti ODG) senza creare la Sitting.
    /// Solo Capogruppo, Vice o Admin.
    /// </summary>
    [HttpPost("import-pdf")]
    [Authorize(Policy = "RequireCapogruppoOrAdmin")]
    [BudgetGuard]
    [RequestSizeLimit(UploadValidator.MaxSizeBytes)]
    public async Task<ActionResult<SittingParsedDto>> ImportPdf(IFormFile file, CancellationToken ct)
    {
        var uid = GetUserId();
        if (uid == null) return Unauthorized();

        if (file == null || file.Length == 0)
            return BadRequest(new { error = "File mancante o vuoto." });

        var (ok, error, ext) = UploadValidator.Validate(file);
        if (!ok) return BadRequest(new { error });

        if (!_ai.IsConfigured)
            return StatusCode(503, new { error = "GEMINI_API_KEY non configurata. Feature AI non disponibile." });

        // Salva temporaneamente in uploads/temp-imports/{userId}/{guid}.{ext}
        var tempDir = Path.Combine(_env.ContentRootPath, "uploads", "temp-imports", uid);
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, $"{Guid.NewGuid()}{ext}");

        try
        {
            string extractedText;
            var originalName = Path.GetFileName(file.FileName ?? $"convocazione{ext}");

            if (_fileEncryptor.IsEnabled)
            {
                await _fileEncryptor.EncryptToFileAsync(file.OpenReadStream(), tempFile);
                using var decrypted = await _fileEncryptor.OpenDecryptedReadAsync(tempFile);
                extractedText = await _extractor.ExtractTextFromStreamAsync(decrypted, originalName, ct);
            }
            else
            {
                using (var fs = System.IO.File.Create(tempFile))
                    await file.CopyToAsync(fs, ct);
                extractedText = await _extractor.ExtractTextAsync(tempFile, originalName, ct);
            }

            const int maxChars = 30_000;
            if (extractedText.Length > maxChars)
                extractedText = extractedText.Substring(0, maxChars);

            var cacheableSystem =
                "Sei un assistente per consiglieri comunali italiani. " +
                "Devi estrarre da una convocazione di consiglio comunale i metadati strutturati. " +
                "Rispondi SEMPRE in JSON valido.";

            var volatileSystem = "Parsa la convocazione fornita.";

            var userPrompt =
                "Estrai da questa convocazione di consiglio comunale: data e ora seduta, luogo, titolo\n" +
                "(es. 'Seduta ordinaria del Consiglio Comunale di Milano'), e la lista numerata dei punti\n" +
                "ODG con descrizione completa.\n\n" +
                "TESTO CONVOCAZIONE:\n---\n" + extractedText + "\n---\n\n" +
                "Rispondi ESCLUSIVAMENTE con JSON:\n" +
                "{\n" +
                "  \"data\": \"YYYY-MM-DDTHH:mm:ss\" (ISO 8601 UTC; se non trovata o ambigua, null),\n" +
                "  \"luogo\": \"...\",\n" +
                "  \"titolo\": \"...\",\n" +
                "  \"agendaItems\": [ { \"ordine\": 1, \"descrizione\": \"...\" }, ... ]\n" +
                "}";

            AICompletionResult result;
            try
            {
                result = await _ai.CompleteWithUsageAsync(cacheableSystem, volatileSystem, userPrompt, maxTokens: 4000, ct);
            }
            catch (AIQuotaExceededException ex)
            {
                _logger.LogWarning("Gemini quota exceeded durante import-pdf: {Msg}", ex.Message);
                return StatusCode(429, new { error = "ai_daily_quota_exceeded", message = "Limite giornaliero AI raggiunto, riprova domani." });
            }

            // Log usage
            _db.UsageLogs.Add(new UsageLog
            {
                UserId = uid,
                Operation = "sittings.import-pdf",
                Model = result.Model,
                InputTokens = result.InputTokens,
                OutputTokens = result.OutputTokens,
                CachedInputTokens = result.CachedReadTokens,
                EstimatedCostUsd = result.EstimatedCostUsd
            });
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "import-pdf AI: model={Model} in={In} out={Out} cost={Cost}",
                result.Model, result.InputTokens, result.OutputTokens, result.EstimatedCostUsd);

            // Parsa il JSON
            var json = ExtractJson(result.Text);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            DateTime? data = null;
            if (root.TryGetProperty("data", out var dataProp) && dataProp.ValueKind == JsonValueKind.String)
            {
                var dateStr = dataProp.GetString();
                if (DateTime.TryParse(dateStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed))
                    data = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            }

            var luogo = root.TryGetProperty("luogo", out var luogoProp) && luogoProp.ValueKind == JsonValueKind.String
                ? luogoProp.GetString() : null;
            var titolo = root.TryGetProperty("titolo", out var titoloProp) && titoloProp.ValueKind == JsonValueKind.String
                ? titoloProp.GetString() : null;

            var items = new List<ParsedAgendaItemDto>();
            if (root.TryGetProperty("agendaItems", out var itemsProp) && itemsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in itemsProp.EnumerateArray())
                {
                    var ordine = el.TryGetProperty("ordine", out var op) && op.ValueKind == JsonValueKind.Number
                        ? op.GetInt32() : 0;
                    var descrizione = el.TryGetProperty("descrizione", out var dp) && dp.ValueKind == JsonValueKind.String
                        ? (dp.GetString() ?? "") : "";
                    items.Add(new ParsedAgendaItemDto(ordine, descrizione));
                }
            }

            return Ok(new SittingParsedDto(data, luogo, titolo, items));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Errore durante import-pdf");
            return StatusCode(502, new { error = "Errore durante il parsing della convocazione: " + ex.Message });
        }
        finally
        {
            // Cancella sempre il file temporaneo
            if (System.IO.File.Exists(tempFile))
            {
                try { System.IO.File.Delete(tempFile); }
                catch (Exception ex) { _logger.LogWarning(ex, "Impossibile cancellare file temporaneo {Path}", tempFile); }
            }
        }
    }

    private static SittingDetailDto ToDetail(Sitting s) => new(
        s.Id, s.Data, s.Luogo, s.Titolo,
        s.AgendaItems.OrderBy(i => i.Ordine).Select(ToItemDto).ToList()
    );

    private static AgendaItemDto ToItemDto(AgendaItem i) => new(
        i.Id, i.Ordine, i.Descrizione, i.Decisione, i.Motivazione, i.ActId, i.DocumentId, i.Status,
        i.Assignments.Select(a => new AssignedUserDto(a.UserId, a.User?.Email ?? "", a.User?.FullName ?? "")).ToList()
    );

    private static string ExtractJson(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start >= 0 && end > start) return text.Substring(start, end - start + 1);
        return text;
    }
}
