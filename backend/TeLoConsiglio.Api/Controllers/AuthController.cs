using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TeLoConsiglio.Api.Auth;
using TeLoConsiglio.Api.Dtos;
using TeLoConsiglio.Domain.Entities;
using TeLoConsiglio.Infrastructure.Data;
using TeLoConsiglio.Infrastructure.Services;

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
    private readonly IAuditLogger _audit;

    public AuthController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn, JwtTokenService jwt, JwtSettings jwtSettings, AppDbContext db, IConfiguration config, IAuditLogger audit)
    {
        _users = users;
        _signIn = signIn;
        _jwt = jwt;
        _jwtSettings = jwtSettings;
        _db = db;
        _config = config;
        _audit = audit;
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

        var authResp = await BuildAuthResponse(user);
        try { await _audit.LogAsync("auth.register", $"User:{user.Id}", new { comune = user.Comune, partito = user.Partito, gruppo = user.Gruppo, viaInvito = invitation != null }); } catch { }
        return Ok(authResp);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginDto dto)
    {
        var user = await _users.FindByEmailAsync(dto.Email);
        if (user == null)
        {
            try { await _audit.LogAsync("auth.login.failure", $"Email:{dto.Email}", new { reason = "user_not_found" }); } catch { }
            return Unauthorized(new { error = "Credenziali non valide" });
        }
        var ok = await _users.CheckPasswordAsync(user, dto.Password);
        if (!ok)
        {
            try { await _audit.LogAsync("auth.login.failure", $"Email:{dto.Email}", new { reason = "wrong_password" }); } catch { }
            return Unauthorized(new { error = "Credenziali non valide" });
        }
        var loginResp = await BuildAuthResponse(user);
        try { await _audit.LogAsync("auth.login.success", $"User:{user.Id}"); } catch { }
        return Ok(loginResp);
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
        var uid = _users.GetUserId(User);
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
        try { await _audit.LogAsync("auth.logout", uid != null ? $"User:{uid}" : null); } catch { }
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

    [Authorize]
    [HttpPut("me/complete-profile")]
    public async Task<ActionResult<UserDto>> CompleteProfile([FromBody] CompleteProfileDto dto)
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return Unauthorized();

        user.Comune = dto.Comune;
        user.Partito = dto.Partito;
        user.Gruppo = dto.Gruppo;

        var res = await _users.UpdateAsync(user);
        if (!res.Succeeded) return BadRequest(new { errors = res.Errors.Select(e => e.Description) });

        try { await _audit.LogAsync("auth.complete-profile", $"User:{user.Id}", new { comune = user.Comune, partito = user.Partito }); } catch { }

        var roles = await _users.GetRolesAsync(user);
        return Ok(new UserDto(user.Id, user.Email ?? "", user.FullName, user.Comune, user.Partito, user.Gruppo, roles));
    }

    // ---- OAuth external login ----

    private static readonly Dictionary<string, string> ProviderSchemeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "google", "Google" },
        { "microsoft", "Microsoft" }
    };

    [HttpGet("external/{provider}")]
    [AllowAnonymous]
    public async Task<IActionResult> ExternalChallenge(string provider, [FromQuery] string? returnUrl = null)
    {
        if (!ProviderSchemeMap.TryGetValue(provider, out var schemeName))
            return BadRequest(new { error = $"Provider '{provider}' non supportato. Valori validi: google, microsoft." });

        var schemeProvider = HttpContext.RequestServices.GetRequiredService<IAuthenticationSchemeProvider>();
        var scheme = await schemeProvider.GetSchemeAsync(schemeName);
        if (scheme == null)
            return StatusCode(503, new { error = $"Il provider '{provider}' non e' configurato. Impostare le variabili d'ambiente {provider.ToUpper()}_CLIENT_ID e {provider.ToUpper()}_CLIENT_SECRET." });

        var safeReturnUrl = Uri.EscapeDataString(returnUrl ?? "");
        var callbackUrl = Url.Action("ExternalFinalize", "Auth", new { provider = provider.ToLower(), returnUrl = safeReturnUrl }, Request.Scheme)!;

        var props = new AuthenticationProperties { RedirectUri = callbackUrl };
        return Challenge(props, schemeName);
    }

    [HttpGet("external/{provider}/finalize")]
    [AllowAnonymous]
    public async Task<IActionResult> ExternalFinalize(string provider, [FromQuery] string? returnUrl = null)
    {
        var frontendUrl = _config["Frontend__Url"] ?? _config["FRONTEND_URL"] ?? "http://localhost:5173";
        var errorRedirect = $"{frontendUrl}/login?oauthError=external_auth_failed";

        var result = await HttpContext.AuthenticateAsync("ExternalAuthCookie");
        if (!result.Succeeded || result.Principal == null)
        {
            try { await _audit.LogAsync("auth.oauth.failure", $"Provider:{provider}"); } catch { }
            return Redirect(errorRedirect);
        }

        var providerKey = result.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = result.Principal.FindFirstValue(ClaimTypes.Email)
            ?? result.Principal.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email);
        var name = result.Principal.FindFirstValue(ClaimTypes.Name)
            ?? $"{result.Principal.FindFirstValue(ClaimTypes.GivenName)} {result.Principal.FindFirstValue(ClaimTypes.Surname)}".Trim();

        if (string.IsNullOrWhiteSpace(providerKey) || string.IsNullOrWhiteSpace(email))
        {
            try { await _audit.LogAsync("auth.oauth.failure", $"Provider:{provider}"); } catch { }
            return Redirect(errorRedirect);
        }

        if (!ProviderSchemeMap.TryGetValue(provider, out var schemeName))
        {
            try { await _audit.LogAsync("auth.oauth.failure", $"Provider:{provider}"); } catch { }
            return Redirect(errorRedirect);
        }

        // Clean up the ephemeral OAuth cookie
        await HttpContext.SignOutAsync("ExternalAuthCookie");

        // 1. Cerca login esterno esistente
        var user = await _users.FindByLoginAsync(schemeName, providerKey);

        if (user == null)
        {
            // 2. Cerca per email
            user = await _users.FindByEmailAsync(email);
            if (user != null)
            {
                // Collega il login esterno all'account esistente
                var linkResult = await _users.AddLoginAsync(user, new UserLoginInfo(schemeName, providerKey, schemeName));
                if (!linkResult.Succeeded)
                    return Redirect(errorRedirect);
            }
            else
            {
                // 3. Crea nuovo utente
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    FullName = string.IsNullOrWhiteSpace(name) ? email : name
                };
                var createResult = await _users.CreateAsync(user);
                if (!createResult.Succeeded)
                    return Redirect(errorRedirect);

                await _users.AddToRoleAsync(user, Roles.Consigliere);

                var loginResult = await _users.AddLoginAsync(user, new UserLoginInfo(schemeName, providerKey, schemeName));
                if (!loginResult.Succeeded)
                    return Redirect(errorRedirect);
            }
        }

        var authResp = await BuildAuthResponse(user);
        var needsProfile = string.IsNullOrWhiteSpace(user.Comune) || string.IsNullOrWhiteSpace(user.Partito);

        try { await _audit.LogAsync("auth.oauth.success", $"User:{user.Id}", new { provider = schemeName }); } catch { }

        var at = Uri.EscapeDataString(authResp.AccessToken);
        var rt = Uri.EscapeDataString(authResp.RefreshToken);
        var np = needsProfile ? "true" : "false";

        return Redirect($"{frontendUrl}/oauth-callback?at={at}&rt={rt}&needsProfile={np}");
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
