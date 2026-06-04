namespace TeLoConsiglio.Domain.Entities;

public class AgendaItemAssignment
{
    public Guid AgendaItemId { get; set; }
    public AgendaItem? AgendaItem { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }
}
