namespace TeLoConsiglio.Domain.Entities;

public enum AgendaDecision
{
    DaDecidere = 0,
    Approvare = 1,
    Respingere = 2,
    Astenersi = 3
}

public class AgendaItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SittingId { get; set; }
    public Sitting? Sitting { get; set; }
    public int Ordine { get; set; }
    public string Descrizione { get; set; } = string.Empty;
    public AgendaDecision Decisione { get; set; } = AgendaDecision.DaDecidere;
    public string Motivazione { get; set; } = string.Empty;
    public Guid? ActId { get; set; }
    public Act? Act { get; set; }

    public ICollection<AgendaItemAssignment> Assignments { get; set; } = new List<AgendaItemAssignment>();
}
