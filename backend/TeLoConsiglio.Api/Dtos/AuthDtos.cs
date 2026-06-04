using System.ComponentModel.DataAnnotations;

namespace TeLoConsiglio.Api.Dtos;

public record RegisterDto([Required, EmailAddress] string Email, [Required, MinLength(8)] string Password, [Required] string FullName, string? Comune, string? Partito);
public record LoginDto([Required, EmailAddress] string Email, [Required] string Password);
public record RefreshDto([Required] string RefreshToken);
public record AuthResponseDto(string AccessToken, DateTime ExpiresAt, string RefreshToken, UserDto User);
public record UserDto(string Id, string Email, string FullName, string? Comune, string? Partito, IList<string> Roles);
public record UserPickDto(string Id, string DisplayName);
public record ProvidersDto(bool Email, bool Google, bool Microsoft);
public record ChangePasswordDto([Required] string CurrentPassword, [Required, MinLength(8)] string NewPassword);
