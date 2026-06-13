namespace TeLoConsiglio.Domain.Entities;

public enum LineaPoliticaSource
{
    Partito = 0,
    Manuale = 1
}

public class PoliticalProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }
    public string LineaPoliticaMd { get; set; } = string.Empty;
    /// <summary>JSON array of strings — punti evidenza (kept for retrocompatibility)</summary>
    public string PuntiEvidenzaJson { get; set; } = "[]";
    /// <summary>JSON array of strings — argomenti forti del consigliere</summary>
    public string ArgomentiFortiJson { get; set; } = "[]";
    /// <summary>JSON array of strings — temi di interesse del consigliere</summary>
    public string TemiInteresseJson { get; set; } = "[]";
    public LineaPoliticaSource LineaPoliticaSource { get; set; } = LineaPoliticaSource.Partito;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
