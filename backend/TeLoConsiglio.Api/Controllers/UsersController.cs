using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TeLoConsiglio.Api.Dtos;
using TeLoConsiglio.Domain.Entities;

namespace TeLoConsiglio.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _users;

    public UsersController(UserManager<ApplicationUser> users) { _users = users; }

    /// <summary>
    /// Lista compatta degli utenti per le assegnazioni ODG.
    /// Restituisce solo Id e displayName (nessuna email/comune/ruolo) per evitare enumeration.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<UserPickDto>>> List()
    {
        var list = await _users.Users
            .OrderBy(u => u.FullName)
            .Select(u => new UserPickDto(
                u.Id,
                !string.IsNullOrWhiteSpace(u.FullName) ? u.FullName : (u.Email ?? "Utente")
            ))
            .ToListAsync();
        return Ok(list);
    }

    /// <summary>
    /// Lista completa, solo Admin (email, comune, ruoli, ...).
    /// </summary>
    [Authorize(Roles = Roles.Admin)]
    [HttpGet("admin")]
    public async Task<ActionResult<List<UserDto>>> ListAdmin()
    {
        var list = await _users.Users.OrderBy(u => u.FullName).ToListAsync();
        var dto = new List<UserDto>();
        foreach (var u in list)
        {
            var roles = await _users.GetRolesAsync(u);
            dto.Add(new UserDto(u.Id, u.Email ?? "", u.FullName, u.Comune, u.Partito, u.Gruppo, roles));
        }
        return Ok(dto);
    }
}
