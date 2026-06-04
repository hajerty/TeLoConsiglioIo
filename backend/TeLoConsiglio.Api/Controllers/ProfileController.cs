using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TeLoConsiglio.Api.Dtos;
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

    public ProfileController(AppDbContext db, UserManager<ApplicationUser> users, IDocumentTextExtractor extractor, IWebHostEnvironment env)
    {
        _db = db; _users = users; _extractor = extractor; _env = env;
    }

    private string GetUserId() => _users.GetUserId(User) ?? throw new InvalidOperationException();

    [HttpGet("political")]
    public async Task<ActionResult<PoliticalProfileDto>> GetPolitical()
    {
        var uid = GetUserId();
        var prof = await _db.PoliticalProfiles.FirstOrDefaultAsync(p => p.UserId == uid);
        if (prof == null) return Ok(new PoliticalProfileDto("", new List<string>()));
        var punti = JsonSerializer.Deserialize<List<string>>(prof.PuntiEvidenzaJson) ?? new List<string>();
        return Ok(new PoliticalProfileDto(prof.LineaPoliticaMd, punti));
    }

    [HttpPut("political")]
    public async Task<ActionResult<PoliticalProfileDto>> UpdatePolitical([FromBody] PoliticalProfileDto dto)
    {
        var uid = GetUserId();
        var prof = await _db.PoliticalProfiles.FirstOrDefaultAsync(p => p.UserId == uid);
        if (prof == null)
        {
            prof = new PoliticalProfile { UserId = uid };
            _db.PoliticalProfiles.Add(prof);
        }
        prof.LineaPoliticaMd = dto.LineaPoliticaMd ?? "";
        prof.PuntiEvidenzaJson = JsonSerializer.Serialize(dto.PuntiEvidenza ?? new List<string>());
        prof.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(dto);
    }

    [HttpGet("programs")]
    public async Task<ActionResult<List<ElectoralProgramDto>>> ListPrograms()
    {
        var uid = GetUserId();
        var list = await _db.ElectoralPrograms.Where(p => p.UserId == uid).OrderByDescending(p => p.UploadedAt).ToListAsync();
        return Ok(list.Select(p => new ElectoralProgramDto(p.Id, p.OriginalName, p.UploadedAt)).ToList());
    }

    [HttpPost("programs")]
    [RequestSizeLimit(50_000_000)]
    public async Task<ActionResult<ElectoralProgramDto>> UploadProgram(IFormFile file)
    {
        if (file == null || file.Length == 0) return BadRequest(new { error = "File mancante" });
        var uid = GetUserId();
        var dir = Path.Combine(_env.ContentRootPath, "uploads", "programs", uid);
        Directory.CreateDirectory(dir);
        var safe = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var path = Path.Combine(dir, safe);
        using (var s = System.IO.File.Create(path)) await file.CopyToAsync(s);
        var text = await _extractor.ExtractTextAsync(path, file.FileName);
        var prog = new ElectoralProgram
        {
            UserId = uid,
            FilePath = path,
            OriginalName = file.FileName,
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
        var stream = System.IO.File.OpenRead(prog.FilePath);
        return File(stream, "application/octet-stream", prog.OriginalName);
    }
}
