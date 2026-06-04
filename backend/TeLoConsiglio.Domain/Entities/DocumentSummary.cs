namespace TeLoConsiglio.Domain.Entities;

public class DocumentSummary
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public Document? Document { get; set; }
    public string SummaryMd { get; set; } = string.Empty;
    /// <summary>JSON array of strings</summary>
    public string KeyPointsJson { get; set; } = "[]";
    /// <summary>JSON array of strings - critiche rispetto alla linea politica</summary>
    public string CriticitiesJson { get; set; } = "[]";
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
