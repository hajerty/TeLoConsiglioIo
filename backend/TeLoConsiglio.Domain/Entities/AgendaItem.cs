namespace TeLoConsiglio.Domain.Entities;

public enum AgendaDecision
{
    DaDecidere = 0,
    Approvare = 1,
    Respingere = 2,
    Astenersi = 3
}

public enum AgendaItemStatus
{
    DaAnalizzare = 0,
    Analizzata = 1,
    ApprovataPerSeduta = 2
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

    /// <summary>FK opzionale a Documents (documento specifico per questo punto ODG).</summary>
    public Guid? DocumentId { get; set; }
    public Document? Document { get; set; }

    public AgendaItemStatus Status { get; set; } = AgendaItemStatus.DaAnalizzare;

    public ICollection<AgendaItemAssignment> Assignments { get; set; } = new List<AgendaItemAssignment>();
}
