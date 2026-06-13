using System.Text.Json;
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
[Route("api/documents")]
public class DocumentsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IDocumentTextExtractor _extractor;
    private readonly IAIService _ai;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(AppDbContext db, UserManager<ApplicationUser> users, IDocumentTextExtractor extractor, IAIService ai, IWebHostEnvironment env, ILogger<DocumentsController> logger)
    {
        _db = db; _users = users; _extractor = extractor; _ai = ai; _env = env; _logger = logger;
    }

    private string GetUserId() => _users.GetUserId(User)!;

    [HttpGet]
    public async Task<ActionResult<List<DocumentDto>>> List([FromQuery] string? q, [FromQuery] DocumentType? type)
    {
        var uid = GetUserId();
        IQueryable<Document> query = _db.Documents.Include(d => d.Summary).Where(d => d.OwnerId == uid);
        if (type.HasValue) query = query.Where(d => d.Type == type.Value);
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(d => d.OriginalName.ToLower().Contains(q.ToLower()) || d.ExtractedText.ToLower().Contains(q.ToLower()));
        var list = await query.OrderByDescending(d => d.CreatedAt).ToListAsync();
        return Ok(list.Select(d => new DocumentDto(d.Id, d.OriginalName, d.Type, d.CreatedAt, d.Summary != null)).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DocumentDetailDto>> Get(Guid id)
    {
        var uid = GetUserId();
        var d = await _db.Documents.Include(x => x.Summary).FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == uid);
        if (d == null) return NotFound();
        DocumentSummaryDto? s = null;
        if (d.Summary != null)
        {
            s = new DocumentSummaryDto(
                d.Summary.SummaryMd,
                JsonSerializer.Deserialize<List<string>>(d.Summary.KeyPointsJson) ?? new(),
                JsonSerializer.Deserialize<List<string>>(d.Summary.CriticitiesJson) ?? new(),
                d.Summary.GeneratedAt);
        }
        return Ok(new DocumentDetailDto(d.Id, d.OriginalName, d.Type, d.CreatedAt, d.ExtractedText, s));
    }

    [HttpPost]
    [RequestSizeLimit(UploadValidator.MaxSizeBytes)]
    public async Task<ActionResult<DocumentDto>> Upload(IFormFile file, [FromQuery] DocumentType type = DocumentType.Documento)
    {
        if (file == null) return BadRequest(new { error = "File mancante" });
        var (ok, error, ext) = UploadValidator.Validate(file);
        if (!ok) return BadRequest(new { error });

        var uid = GetUserId();
        var dir = Path.Combine(_env.ContentRootPath, "uploads", "documents", uid);
        Directory.CreateDirectory(dir);
        var safe = $"{Guid.NewGuid()}{ext}";
        var path = Path.Combine(dir, safe);
        using (var s = System.IO.File.Create(path)) await file.CopyToAsync(s);
        var originalSafe = Path.GetFileName(file.FileName ?? "");
        var text = await _extractor.ExtractTextAsync(path, originalSafe);
        // Tronca testo estratto per evitare bloat del DB
        const int maxExtractedChars = 1_000_000;
        if (text.Length > maxExtractedChars)
            text = text.Substring(0, maxExtractedChars) + "\n[... testo troncato ...]";
        var doc = new Document
        {
            OwnerId = uid,
            FilePath = path,
            OriginalName = originalSafe,
            ExtractedText = text,
            Type = type
        };
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();
        return Ok(new DocumentDto(doc.Id, doc.OriginalName, doc.Type, doc.CreatedAt, false));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var uid = GetUserId();
        var d = await _db.Documents.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == uid);
        if (d == null) return NotFound();
        try { if (System.IO.File.Exists(d.FilePath)) System.IO.File.Delete(d.FilePath); } catch { }
        _db.Documents.Remove(d);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:guid}/summarize")]
    [BudgetGuard]
    public async Task<ActionResult<DocumentSummaryDto>> Summarize(Guid id)
    {
        if (!_ai.IsConfigured)
            return StatusCode(503, new { error = "Servizio AI non configurato (GEMINI_API_KEY mancante)." });

        var uid = GetUserId();
        var doc = await _db.Documents.Include(d => d.Summary).FirstOrDefaultAsync(d => d.Id == id && d.OwnerId == uid);
        if (doc == null) return NotFound();
        if (string.IsNullOrWhiteSpace(doc.ExtractedText))
            return BadRequest(new { error = "Testo non estratto dal documento." });

        var profile = await _db.PoliticalProfiles.FirstOrDefaultAsync(p => p.UserId == uid);
        var puntiText = profile != null
            ? string.Join(", ", JsonSerializer.Deserialize<List<string>>(profile.PuntiEvidenzaJson) ?? new())
            : "";

        var lineaPolitica = profile?.LineaPoliticaMd ?? "(non specificata)";

        var cacheableSystem = $@"Sei un assistente esperto di diritto degli enti locali italiani (TUEL D.Lgs. 267/2000) e di tecnica legislativa. Devi assistere un consigliere comunale italiano. Rispondi SEMPRE in italiano, in modo conciso, professionale, e SOLO in JSON valido seguendo lo schema richiesto.

LINEA POLITICA DEL CONSIGLIERE:
{lineaPolitica}

PUNTI DA METTERE IN EVIDENZA: {puntiText}";

        var volatileSystem = "Analizza il documento fornito dall'utente alla luce del contesto politico sopra.";

        var bodyText = doc.ExtractedText.Length > 30000 ? doc.ExtractedText.Substring(0, 30000) + "\n[... testo troncato ...]" : doc.ExtractedText;

        var user = $@"Analizza il seguente documento.

DOCUMENTO:
---
{bodyText}
---

Restituisci ESCLUSIVAMENTE un oggetto JSON con questa struttura:
{{
  ""summaryMd"": ""<riassunto esecutivo del documento, in markdown>"",
  ""keyPoints"": [""<punto chiave 1>"", ""<punto chiave 2>"", ...],
  ""criticities"": [""<criticità o punto di attenzione rispetto alla linea politica>"", ...]
}}";

        try
        {
            var aiResult = await _ai.CompleteWithUsageAsync(cacheableSystem, volatileSystem, user, maxTokens: 4000);
            var raw = aiResult.Text;
            _db.UsageLogs.Add(new UsageLog
            {
                UserId = uid,
                Operation = "documents.summarize",
                Model = aiResult.Model,
                InputTokens = aiResult.InputTokens,
                OutputTokens = aiResult.OutputTokens,
                CachedInputTokens = aiResult.CachedReadTokens,
                EstimatedCostUsd = aiResult.EstimatedCostUsd
            });
            var json = ExtractJson(raw);
            using var docJson = JsonDocument.Parse(json);
            var root = docJson.RootElement;
            var summary = root.GetProperty("summaryMd").GetString() ?? "";
            var keyPoints = root.GetProperty("keyPoints").EnumerateArray().Select(e => e.GetString() ?? "").Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            var critic = root.GetProperty("criticities").EnumerateArray().Select(e => e.GetString() ?? "").Where(s => !string.IsNullOrWhiteSpace(s)).ToList();

            if (doc.Summary == null)
            {
                doc.Summary = new DocumentSummary { DocumentId = doc.Id };
                _db.DocumentSummaries.Add(doc.Summary);
            }
            doc.Summary.SummaryMd = summary;
            doc.Summary.KeyPointsJson = JsonSerializer.Serialize(keyPoints);
            doc.Summary.CriticitiesJson = JsonSerializer.Serialize(critic);
            doc.Summary.GeneratedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Ok(new DocumentSummaryDto(summary, keyPoints, critic, doc.Summary.GeneratedAt));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore Gemini summarize");
            return StatusCode(502, new { error = "Errore durante la generazione del riassunto: " + ex.Message });
        }
    }

    private static string ExtractJson(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start >= 0 && end > start) return text.Substring(start, end - start + 1);
        return text;
    }
}
