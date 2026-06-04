using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TeLoConsiglio.Domain.Entities;

namespace TeLoConsiglio.Infrastructure.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<ElectoralProgram> ElectoralPrograms => Set<ElectoralProgram>();
    public DbSet<PoliticalProfile> PoliticalProfiles => Set<PoliticalProfile>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentSummary> DocumentSummaries => Set<DocumentSummary>();
    public DbSet<Act> Acts => Set<Act>();
    public DbSet<ActRevision> ActRevisions => Set<ActRevision>();
    public DbSet<LegalReference> LegalReferences => Set<LegalReference>();
    public DbSet<Sitting> Sittings => Set<Sitting>();
    public DbSet<AgendaItem> AgendaItems => Set<AgendaItem>();
    public DbSet<AgendaItemAssignment> AgendaItemAssignments => Set<AgendaItemAssignment>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<PoliticalProfile>()
            .HasOne(p => p.User)
            .WithOne(u => u.PoliticalProfile)
            .HasForeignKey<PoliticalProfile>(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<PoliticalProfile>().HasIndex(p => p.UserId).IsUnique();

        b.Entity<ElectoralProgram>()
            .HasOne(p => p.User)
            .WithMany(u => u.ElectoralPrograms)
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<Document>()
            .HasOne(d => d.Owner)
            .WithMany(u => u.Documents)
            .HasForeignKey(d => d.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<DocumentSummary>()
            .HasOne(s => s.Document)
            .WithOne(d => d.Summary)
            .HasForeignKey<DocumentSummary>(s => s.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<Act>()
            .HasOne(a => a.Owner)
            .WithMany(u => u.Acts)
            .HasForeignKey(a => a.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Entity<Act>()
            .HasOne(a => a.ParentAct)
            .WithMany()
            .HasForeignKey(a => a.ParentActId)
            .OnDelete(DeleteBehavior.SetNull);

        b.Entity<ActRevision>()
            .HasOne(r => r.Act)
            .WithMany(a => a.Revisions)
            .HasForeignKey(r => r.ActId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<ActRevision>()
            .HasOne(r => r.Author)
            .WithMany()
            .HasForeignKey(r => r.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Entity<LegalReference>()
            .HasOne(l => l.Act)
            .WithMany(a => a.LegalReferences)
            .HasForeignKey(l => l.ActId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<Sitting>()
            .HasOne(s => s.CreatedBy)
            .WithMany()
            .HasForeignKey(s => s.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        b.Entity<AgendaItem>()
            .HasOne(i => i.Sitting)
            .WithMany(s => s.AgendaItems)
            .HasForeignKey(i => i.SittingId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<AgendaItem>()
            .HasOne(i => i.Act)
            .WithMany()
            .HasForeignKey(i => i.ActId)
            .OnDelete(DeleteBehavior.SetNull);

        b.Entity<AgendaItemAssignment>()
            .HasKey(a => new { a.AgendaItemId, a.UserId });

        b.Entity<AgendaItemAssignment>()
            .HasOne(a => a.AgendaItem)
            .WithMany(i => i.Assignments)
            .HasForeignKey(a => a.AgendaItemId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<AgendaItemAssignment>()
            .HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<RefreshToken>()
            .HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<RefreshToken>().HasIndex(r => r.Token).IsUnique();
    }
}
