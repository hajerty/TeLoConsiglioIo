using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TeLoConsiglio.Api.Auth;
using TeLoConsiglio.Api.Dtos;
using TeLoConsiglio.Domain.Entities;
using TeLoConsiglio.Infrastructure.Data;

namespace TeLoConsiglio.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly JwtTokenService _jwt;
    private readonly JwtSettings _jwtSettings;
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public AuthController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn, JwtTokenService jwt, JwtSettings jwtSettings, AppDbContext db, IConfiguration config)
    {
        _users = users;
        _signIn = signIn;
        _jwt = jwt;
        _jwtSettings = jwtSettings;
        _db = db;
        _config = config;
    }

    [HttpGet("providers")]
    public ActionResult<ProvidersDto> Providers()
    {
        var google = !string.IsNullOrWhiteSpace(_config["GOOGLE_CLIENT_ID"]);
        var ms = !string.IsNullOrWhiteSpace(_config["MICROSOFT_CLIENT_ID"]);
        return Ok(new ProvidersDto(true, google, ms));
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> Register([FromBody] RegisterDto dto)
    {
        var existing = await _users.FindByEmailAsync(dto.Email);
        if (existing != null) return Conflict(new { error = "Email gia' registrata" });

        // Resolve gruppo/comune from invitation if provided
        string? resolvedGruppo = dto.Gruppo;
        string resolvedComune = dto.Comune;
        Invitation? invitation = null;

        if (!string.IsNullOrWhiteSpace(dto.InvitationToken))
        {
            invitation = await _db.Invitations.FirstOrDefaultAsync(i =>
                i.Token == dto.InvitationToken &&
                i.ConsumedAt == null &&
                i.RevokedAt == null &&
                i.ExpiresAt > DateTime.UtcNow);

            if (invitation == null)
                return BadRequest(new { error = "Token di invito non valido, scaduto o gia' utilizzato." });

            // Invitation can pre-fill gruppo and comune if not explicitly provided
            if (string.IsNullOrWhiteSpace(resolvedGruppo) && !string.IsNullOrWhiteSpace(invitation.Gruppo))
                resolvedGruppo = invitation.Gruppo;
            if (!string.IsNullOrWhiteSpace(invitation.Comune))
                resolvedComune = invitation.Comune;
        }

        var user = new ApplicationUser
        {
            UserName = dto.Email,
            Email = dto.Email,
            FullName = dto.FullName,
            Comune = resolvedComune,
            Partito = dto.Partito,
            Gruppo = resolvedGruppo,
            EmailConfirmed = true
        };
        var res = await _users.CreateAsync(user, dto.Password);
        if (!res.Succeeded) return BadRequest(new { errors = res.Errors.Select(e => e.Description) });

        await _users.AddToRoleAsync(user, Roles.Consigliere);

        // Mark invitation consumed
        if (invitation != null)
        {
            invitation.ConsumedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        return Ok(await BuildAuthResponse(user));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginDto dto)
    {
        var user = await _users.FindByEmailAsync(dto.Email);
        if (user == null) return Unauthorized(new { error = "Credenziali non valide" });
        var ok = await _users.CheckPasswordAsync(user, dto.Password);
        if (!ok) return Unauthorized(new { error = "Credenziali non valide" });
        return Ok(await BuildAuthResponse(user));
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponseDto>> Refresh([FromBody] RefreshDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.RefreshToken))
            return Unauthorized(new { error = "Refresh token non valido" });
        var hash = JwtTokenService.HashRefreshToken(dto.RefreshToken);
        var rt = await _db.RefreshTokens.Include(r => r.User).FirstOrDefaultAsync(r => r.TokenHash == hash);
        if (rt == null || rt.RevokedAt != null || rt.ExpiresAt < DateTime.UtcNow)
            return Unauthorized(new { error = "Refresh token non valido" });
        if (rt.User == null) return Unauthorized();

        // Rotazione: revoca il vecchio token e crea il nuovo (collegandolo per replay-detection).
        rt.RevokedAt = DateTime.UtcNow;
        var resp = await BuildAuthResponse(rt.User, replacedTokenToLink: rt);
        await _db.SaveChangesAsync();
        return Ok(resp);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshDto? dto)
    {
        if (dto != null && !string.IsNullOrWhiteSpace(dto.RefreshToken))
        {
            var hash = JwtTokenService.HashRefreshToken(dto.RefreshToken);
            var rt = await _db.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == hash);
            if (rt != null && rt.RevokedAt == null)
            {
                rt.RevokedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }
        }
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me()
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return Unauthorized();
        var roles = await _users.GetRolesAsync(user);
        return Ok(new UserDto(user.Id, user.Email ?? "", user.FullName, user.Comune, user.Partito, user.Gruppo, roles));
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return Unauthorized();
        var res = await _users.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword);
        if (!res.Succeeded) return BadRequest(new { errors = res.Errors.Select(e => e.Description) });
        return NoContent();
    }

    private async Task<AuthResponseDto> BuildAuthResponse(ApplicationUser user, RefreshToken? replacedTokenToLink = null)
    {
        var (token, exp) = await _jwt.CreateAccessTokenAsync(user);
        var rtStr = _jwt.CreateRefreshToken();
        var rt = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = JwtTokenService.HashRefreshToken(rtStr),
            ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshExpirationDays)
        };
        _db.RefreshTokens.Add(rt);
        await _db.SaveChangesAsync();
        if (replacedTokenToLink != null)
        {
            replacedTokenToLink.ReplacedByTokenId = rt.Id;
        }
        var roles = await _users.GetRolesAsync(user);
        return new AuthResponseDto(token, exp, rtStr,
            new UserDto(user.Id, user.Email ?? "", user.FullName, user.Comune, user.Partito, user.Gruppo, roles));
    }
}
