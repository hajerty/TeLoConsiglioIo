using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace TeLoConsiglio.Domain.Entities;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Comune { get; set; }

    [MaxLength(100)]
    public string? Partito { get; set; }

    /// <summary>Nome del gruppo consiliare, es. "Gruppo PD". Nullable.</summary>
    [MaxLength(200)]
    public string? Gruppo { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Se false, nessuna notifica email viene inviata all'utente.</summary>
    public bool EmailNotificationsEnabled { get; set; } = true;

    public PoliticalProfile? PoliticalProfile { get; set; }
    public ICollection<ElectoralProgram> ElectoralPrograms { get; set; } = new List<ElectoralProgram>();
    public ICollection<Document> Documents { get; set; } = new List<Document>();
    public ICollection<Act> Acts { get; set; } = new List<Act>();
}
