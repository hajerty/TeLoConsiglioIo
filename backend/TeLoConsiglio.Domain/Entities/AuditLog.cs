namespace TeLoConsiglio.Domain.Entities;

public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? UserId { get; set; }           // null per azioni di sistema
    public string? UserEmail { get; set; }         // snapshot denormalizzato
    public string Action { get; set; } = "";       // es. "auth.login.success", "sitting.create"
    public string? Resource { get; set; }          // es. "Sitting:<guid>", "User:<id>"
    public string? DetailsJson { get; set; }       // payload extra in JSON (opzionale)
    public string? IpAddress { get; set; }         // remote ip troncato
    public string? UserAgent { get; set; }         // troncato a 500 char
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
