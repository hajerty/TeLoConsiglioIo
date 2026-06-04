namespace TeLoConsiglio.Domain.Entities;

public class LegalReference
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ActId { get; set; }
    public Act? Act { get; set; }
    public string Citation { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Inserted { get; set; } = false;
    public DateTime? ConfirmedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
