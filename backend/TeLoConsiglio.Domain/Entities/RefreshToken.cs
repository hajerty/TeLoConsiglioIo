namespace TeLoConsiglio.Domain.Entities;

public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }
    /// <summary>
    /// Hash SHA-256 (hex lowercase) del refresh token. Il valore in chiaro non viene mai persistito.
    /// </summary>
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }
    /// <summary>
    /// Id del token che ha sostituito questo (per detection di replay e rotazione).
    /// </summary>
    public Guid? ReplacedByTokenId { get; set; }
}
