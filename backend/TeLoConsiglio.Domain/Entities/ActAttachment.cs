namespace TeLoConsiglio.Domain.Entities;

public class ActAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ActId { get; set; }
    public Act? Act { get; set; }
    public string FilePath { get; set; } = "";
    public string OriginalName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    /// <summary>Extracted text, truncated at 200k chars</summary>
    public string ExtractedText { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
