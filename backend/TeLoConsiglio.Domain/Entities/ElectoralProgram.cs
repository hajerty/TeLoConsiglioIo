namespace TeLoConsiglio.Domain.Entities;

public class ElectoralProgram
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string OriginalName { get; set; } = string.Empty;
    public string ExtractedText { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
