using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeLoConsiglio.Api.Services;

namespace TeLoConsiglio.Api.Controllers;

[ApiController]
[Route("api/party-manifests")]
public class PartyManifestsController : ControllerBase
{
    private readonly PartyManifestService _service;

    public PartyManifestsController(PartyManifestService service)
    {
        _service = service;
    }

    /// <summary>Lista partiti (solo chiave e nome completo, no contenuto). Pubblico.</summary>
    [HttpGet]
    [AllowAnonymous]
    public IActionResult List()
    {
        var result = _service.All.Values
            .Select(m => new { key = m.Key, fullName = m.FullName })
            .OrderBy(m => m.key)
            .ToList();
        return Ok(result);
    }

    /// <summary>Dettaglio manifesto con testo linea politica. Richiede autenticazione.</summary>
    [HttpGet("{key}")]
    [Authorize]
    public IActionResult Get(string key)
    {
        var manifest = _service.GetManifest(key);
        if (manifest == null) return NotFound(new { error = $"Manifesto non trovato per chiave '{key}'." });
        return Ok(new { key = manifest.Key, fullName = manifest.FullName, lineaPoliticaMd = manifest.LineaPoliticaMd });
    }
}
