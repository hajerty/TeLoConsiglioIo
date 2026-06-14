using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TeLoConsiglio.Api.Auth;
using TeLoConsiglio.Api.Dtos;
using TeLoConsiglio.Api.Services;
using TeLoConsiglio.Domain.Entities;
using TeLoConsiglio.Infrastructure.Data;
using TeLoConsiglio.Infrastructure.Services;

namespace TeLoConsiglio.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/profile")]
public class ProfileController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IDocumentTextExtractor _extractor;
    private readonly IWebHostEnvironment _env;
    private readonly PartyManifestService _partyManifests;
    private readonly IFileEncryptor _fileEncryptor;

    public ProfileController(
        AppDbContext db,
        UserManager<ApplicationUser> users,
        IDocumentTextExtractor extractor,
        IWebHostEnvironment env,
        PartyManifestService partyManifests,
        IFileEncryptor fileEncryptor)
    {
        _db = db; _users = users; _extractor = extractor; _env = env; _partyManifests = partyManifests; _fileEncryptor = fileEncryptor;
    }

    private string GetUserId() => _users.GetUserId(User) ?? throw new InvalidOperationException();

    [HttpGet("political")]
    public async Task<ActionResult<PoliticalProfileDto>> GetPolitical()
    {
        var uid = GetUserId();
        var prof = await _db.PoliticalProfiles.FirstOrDefaultAsync(p => p.UserId == uid);

        // Auto-populate from party manifest if profile is empty
        if ((prof == null || string.IsNullOrWhiteSpace(prof.LineaPoliticaMd)))
        {
            var user = await _users.FindByIdAsync(uid);
            if (user != null && !string.IsNullOrWhiteSpace(user.Partito))
            {
                var manifest = _partyManifests.GetManifest(user.Partito);
                if (manifest != null)
                {
                    if (prof == null)
                    {
                        prof = new PoliticalProfile { UserId = uid };
                        _db.PoliticalProfiles.Add(prof);
                    }
                    prof.LineaPoliticaMd = manifest.LineaPoliticaMd;
                    prof.LineaPoliticaSource = LineaPoliticaSource.Partito;
                    prof.UpdatedAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync();
                }
            }
        }

        if (prof == null)
            return Ok(new PoliticalProfileDto("", new List<string>(), new List<string>(), LineaPoliticaSource.Partito));

        var argomenti = JsonSerializer.Deserialize<List<string>>(prof.ArgomentiFortiJson) ?? new List<string>();
        var temi = JsonSerializer.Deserialize<List<string>>(prof.TemiInteresseJson) ?? new List<string>();
        return Ok(new PoliticalProfileDto(prof.LineaPoliticaMd, argomenti, temi, prof.LineaPoliticaSource));
    }

    [HttpPut("political")]
    public async Task<ActionResult<PoliticalProfileDto>> UpdatePolitical([FromBody] PoliticalProfileUpdateDto dto)
    {
        var uid = GetUserId();
        var prof = await _db.PoliticalProfiles.FirstOrDefaultAsync(p => p.UserId == uid);
        if (prof == null)
        {
            prof = new PoliticalProfile { UserId = uid };
            _db.PoliticalProfiles.Add(prof);
        }

        // Detect if lineaPoliticaMd changed from the party manifest → mark as Manuale
        if (dto.LineaPoliticaMd != null)
        {
            var user = await _users.FindByIdAsync(uid);
            var manifest = (user != null && !string.IsNullOrWhiteSpace(user.Partito))
                ? _partyManifests.GetManifest(user.Partito)
                : null;
            var isManifestText = manifest != null &&
                string.Equals(dto.LineaPoliticaMd.Trim(), manifest.LineaPoliticaMd.Trim(), StringComparison.Ordinal);
            prof.LineaPoliticaSource = isManifestText ? LineaPoliticaSource.Partito : LineaPoliticaSource.Manuale;
            prof.LineaPoliticaMd = dto.LineaPoliticaMd;
        }

        if (dto.ArgomentiForti != null)
            prof.ArgomentiFortiJson = JsonSerializer.Serialize(dto.ArgomentiForti);
        if (dto.TemiInteresse != null)
            prof.TemiInteresseJson = JsonSerializer.Serialize(dto.TemiInteresse);

        prof.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var argomenti = JsonSerializer.Deserialize<List<string>>(prof.ArgomentiFortiJson) ?? new List<string>();
        var temi = JsonSerializer.Deserialize<List<string>>(prof.TemiInteresseJson) ?? new List<string>();
        return Ok(new PoliticalProfileDto(prof.LineaPoliticaMd, argomenti, temi, prof.LineaPoliticaSource));
    }

    [HttpPost("political/reset-linea")]
    public async Task<ActionResult<PoliticalProfileDto>> ResetLinea()
    {
        var uid = GetUserId();
        var user = await _users.FindByIdAsync(uid);
        if (user == null || string.IsNullOrWhiteSpace(user.Partito))
            return BadRequest(new { error = "Partito non impostato nel profilo utente." });

        var manifest = _partyManifests.GetManifest(user.Partito);
        if (manifest == null)
            return BadRequest(new { error = $"Nessun manifesto disponibile per il partito '{user.Partito}'." });

        var prof = await _db.PoliticalProfiles.FirstOrDefaultAsync(p => p.UserId == uid);
        if (prof == null)
        {
            prof = new PoliticalProfile { UserId = uid };
            _db.PoliticalProfiles.Add(prof);
        }
        prof.LineaPoliticaMd = manifest.LineaPoliticaMd;
        prof.LineaPoliticaSource = LineaPoliticaSource.Partito;
        prof.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var argomenti = JsonSerializer.Deserialize<List<string>>(prof.ArgomentiFortiJson) ?? new List<string>();
        var temi = JsonSerializer.Deserialize<List<string>>(prof.TemiInteresseJson) ?? new List<string>();
        return Ok(new PoliticalProfileDto(prof.LineaPoliticaMd, argomenti, temi, prof.LineaPoliticaSource));
    }

    // ── Preferenze notifiche ────────────────────────────────────────────────────

    [HttpGet("notifications")]
    public async Task<ActionResult<NotificationPreferencesDto>> GetNotifications()
    {
        var uid = GetUserId();
        var user = await _users.FindByIdAsync(uid);
        if (user == null) return Unauthorized();
        return Ok(new NotificationPreferencesDto(user.EmailNotificationsEnabled));
    }

    [HttpPut("notifications")]
    public async Task<ActionResult<NotificationPreferencesDto>> UpdateNotifications([FromBody] NotificationPreferencesUpdateDto dto)
    {
        var uid = GetUserId();
        var user = await _users.FindByIdAsync(uid);
        if (user == null) return Unauthorized();
        user.EmailNotificationsEnabled = dto.EmailNotificationsEnabled;
        await _users.UpdateAsync(user);
        return Ok(new NotificationPreferencesDto(user.EmailNotificationsEnabled));
    }

    // ── Programmi elettorali ────────────────────────────────────────────────────

    [HttpGet("programs")]
    public async Task<ActionResult<List<ElectoralProgramDto>>> ListPrograms()
    {
        var uid = GetUserId();
        var list = await _db.ElectoralPrograms.Where(p => p.UserId == uid).OrderByDescending(p => p.UploadedAt).ToListAsync();
        return Ok(list.Select(p => new ElectoralProgramDto(p.Id, p.OriginalName, p.UploadedAt)).ToList());
    }

    [HttpPost("programs")]
    [RequestSizeLimit(UploadValidator.MaxSizeBytes)]
    public async Task<ActionResult<ElectoralProgramDto>> UploadProgram(IFormFile file)
    {
        if (file == null) return BadRequest(new { error = "File mancante" });
        var (ok, error, ext) = UploadValidator.Validate(file);
        if (!ok) return BadRequest(new { error });

        var uid = GetUserId();
        var dir = Path.Combine(_env.ContentRootPath, "uploads", "programs", uid);
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
        var prog = new ElectoralProgram
        {
            UserId = uid,
            FilePath = path,
            OriginalName = originalSafe,
            ExtractedText = text
        };
        _db.ElectoralPrograms.Add(prog);
        await _db.SaveChangesAsync();
        return Ok(new ElectoralProgramDto(prog.Id, prog.OriginalName, prog.UploadedAt));
    }

    [HttpDelete("programs/{id:guid}")]
    public async Task<IActionResult> DeleteProgram(Guid id)
    {
        var uid = GetUserId();
        var prog = await _db.ElectoralPrograms.FirstOrDefaultAsync(p => p.Id == id && p.UserId == uid);
        if (prog == null) return NotFound();
        try { if (System.IO.File.Exists(prog.FilePath)) System.IO.File.Delete(prog.FilePath); } catch { }
        _db.ElectoralPrograms.Remove(prog);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("programs/{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id)
    {
        var uid = GetUserId();
        var prog = await _db.ElectoralPrograms.FirstOrDefaultAsync(p => p.Id == id && p.UserId == uid);
        if (prog == null || !System.IO.File.Exists(prog.FilePath)) return NotFound();
        Stream stream = _fileEncryptor.IsLikelyEncrypted(prog.FilePath)
            ? await _fileEncryptor.OpenDecryptedReadAsync(prog.FilePath)
            : System.IO.File.OpenRead(prog.FilePath);
        return File(stream, "application/octet-stream", prog.OriginalName);
    }
}
