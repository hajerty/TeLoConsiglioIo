namespace TeLoConsiglio.Domain.Entities;

public class ActRevision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ActId { get; set; }
    public Act? Act { get; set; }
    public string BodyMd { get; set; } = string.Empty;
    public string AuthorId { get; set; } = string.Empty;
    public ApplicationUser? Author { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
