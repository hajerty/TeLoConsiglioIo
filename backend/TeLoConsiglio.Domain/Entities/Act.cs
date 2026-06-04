namespace TeLoConsiglio.Domain.Entities;

public enum ActType
{
    Mozione = 0,
    OrdineDelGiorno = 1,
    Delibera = 2,
    Emendamento = 3
}

public enum ActStatus
{
    Bozza = 0,
    Depositato = 1,
    Approvato = 2,
    Respinto = 3
}

public class Act
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OwnerId { get; set; } = string.Empty;
    public ApplicationUser? Owner { get; set; }
    public ActType Tipo { get; set; }
    public string Titolo { get; set; } = string.Empty;
    public string Oggetto { get; set; } = string.Empty;
    public string BodyMd { get; set; } = string.Empty;
    public string? ContextNotes { get; set; }
    public ActStatus Status { get; set; } = ActStatus.Bozza;
    public Guid? ParentActId { get; set; }
    public Act? ParentAct { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ActRevision> Revisions { get; set; } = new List<ActRevision>();
    public ICollection<LegalReference> LegalReferences { get; set; } = new List<LegalReference>();
}
