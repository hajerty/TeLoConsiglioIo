namespace TeLoConsiglio.Domain.Entities;

public class Sitting
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime Data { get; set; }
    public string Luogo { get; set; } = string.Empty;
    public string Titolo { get; set; } = string.Empty;
    public string CreatedById { get; set; } = string.Empty;
    public ApplicationUser? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<AgendaItem> AgendaItems { get; set; } = new List<AgendaItem>();
}
