using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeLoConsiglio.Infrastructure.Services;

namespace TeLoConsiglio.Api.Controllers;

[ApiController]
[Authorize(Policy = "RequireAdminOnly")]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly IFileEncryptor _fileEncryptor;
    private readonly IWebHostEnvironment _env;

    public AdminController(IFileEncryptor fileEncryptor, IWebHostEnvironment env)
    {
        _fileEncryptor = fileEncryptor;
        _env = env;
    }

    /// <summary>
    /// Restituisce lo stato della cifratura at-rest dei documenti.
    /// Scansiona le directory uploads e conta file cifrati vs in chiaro.
    /// </summary>
    [HttpGet("encryption-status")]
    public IActionResult GetEncryptionStatus()
    {
        var uploadsRoot = Path.Combine(_env.ContentRootPath, "uploads");
        var subDirs = new[] { "programs", "documents", "act-attachments", "agenda-documents" };

        int encrypted = 0;
        int plain = 0;

        foreach (var sub in subDirs)
        {
            var dir = Path.Combine(uploadsRoot, sub);
            if (!Directory.Exists(dir)) continue;

            // Scansione ricorsiva: i file sono in sotto-cartelle per userId
            foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                if (_fileEncryptor.IsLikelyEncrypted(file))
                    encrypted++;
                else
                    plain++;
            }
        }

        return Ok(new
        {
            enabled = _fileEncryptor.IsEnabled,
            algorithm = "AES-256-GCM",
            filesEncrypted = encrypted,
            filesPlain = plain
        });
    }
}
