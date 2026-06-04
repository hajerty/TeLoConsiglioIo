namespace TeLoConsiglio.Domain.Entities;

public enum DocumentType
{
    Delibera = 0,
    Verbale = 1,
    Documento = 2,
    Altro = 99
}

public class Document
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OwnerId { get; set; } = string.Empty;
    public ApplicationUser? Owner { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string OriginalName { get; set; } = string.Empty;
    public string ExtractedText { get; set; } = string.Empty;
    public DocumentType Type { get; set; } = DocumentType.Documento;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DocumentSummary? Summary { get; set; }
}
