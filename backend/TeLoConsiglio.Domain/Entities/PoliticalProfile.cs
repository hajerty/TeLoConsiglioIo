namespace TeLoConsiglio.Domain.Entities;

public class PoliticalProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }
    public string LineaPoliticaMd { get; set; } = string.Empty;
    /// <summary>JSON array of strings</summary>
    public string PuntiEvidenzaJson { get; set; } = "[]";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
