namespace TeLoConsiglio.Domain.Entities;

public class Invitation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Token { get; set; } = "";          // GUID-like, opaco
    public string Email { get; set; } = "";
    public string Nome { get; set; } = "";
    public string Cognome { get; set; } = "";
    public string Comune { get; set; } = "";
    public string Gruppo { get; set; } = "";
    public string InvitedByUserId { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(14);
    public DateTime? ConsumedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}
