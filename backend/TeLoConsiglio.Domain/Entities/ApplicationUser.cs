using Microsoft.AspNetCore.Identity;

namespace TeLoConsiglio.Domain.Entities;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public string? Comune { get; set; }
    public string? Partito { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public PoliticalProfile? PoliticalProfile { get; set; }
    public ICollection<ElectoralProgram> ElectoralPrograms { get; set; } = new List<ElectoralProgram>();
    public ICollection<Document> Documents { get; set; } = new List<Document>();
    public ICollection<Act> Acts { get; set; } = new List<Act>();
}
