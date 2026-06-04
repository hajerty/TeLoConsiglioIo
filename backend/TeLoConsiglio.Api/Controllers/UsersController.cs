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

    [HttpGet]
    public async Task<ActionResult<List<UserDto>>> List()
    {
        var list = await _users.Users.OrderBy(u => u.FullName).ToListAsync();
        var dto = new List<UserDto>();
        foreach (var u in list)
        {
            var roles = await _users.GetRolesAsync(u);
            dto.Add(new UserDto(u.Id, u.Email ?? "", u.FullName, u.Comune, u.Partito, roles));
        }
        return Ok(dto);
    }
}
