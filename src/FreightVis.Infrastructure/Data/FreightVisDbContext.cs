using FreightVis.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FreightVis.Infrastructure.Data;

public sealed class FreightVisDbContext : DbContext
{
    public FreightVisDbContext(DbContextOptions<FreightVisDbContext> options) : base(options) { }

    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<UserClientAccess> UserClientAccess => Set<UserClientAccess>();
    public DbSet<IncidentLookup> IncidentLookups => Set<IncidentLookup>();
    public DbSet<CustomField> CustomFields => Set<CustomField>();
    public DbSet<CustomFieldValue> CustomFieldValues => Set<CustomFieldValue>();
    public DbSet<IncidentEvent> IncidentEvents => Set<IncidentEvent>();
    public DbSet<IncidentAttachment> IncidentAttachments => Set<IncidentAttachment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Incident>(b =>
        {
            b.ToTable("Incidents");
            b.HasKey(x => x.Id);
            b.Property(x => x.Location).HasMaxLength(256);
            b.Property(x => x.Description).HasMaxLength(4000);
            b.HasIndex(x => new { x.ClientId, x.OccurredAt });
            b.HasIndex(x => new { x.ClientId, x.ReferenceNumber }).IsUnique();
        });

        modelBuilder.Entity<AppUser>(b =>
        {
            b.ToTable("Users");
            b.HasKey(x => x.Id);
            b.Property(x => x.Email).HasMaxLength(256).IsRequired();
            b.Property(x => x.DisplayName).HasMaxLength(128).IsRequired();
            b.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
            b.HasIndex(x => x.Email).IsUnique();
        });

        modelBuilder.Entity<Client>(b =>
        {
            b.ToTable("Clients");
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).HasMaxLength(256).IsRequired();
            b.HasIndex(x => x.ParentClientId);
        });

        modelBuilder.Entity<UserClientAccess>(b =>
        {
            b.ToTable("UserClientAccess");
            b.HasKey(x => new { x.UserId, x.ClientId });
            b.Property(x => x.Role).HasMaxLength(64).IsRequired();
        });

        modelBuilder.Entity<IncidentLookup>(b =>
        {
            b.ToTable("IncidentLookups");
            b.HasKey(x => x.Id);
            b.Property(x => x.FieldKey).HasMaxLength(50).IsRequired();
            b.Property(x => x.Label).HasMaxLength(150).IsRequired();
            b.HasIndex(x => new { x.ClientId, x.FieldKey });
            b.HasIndex(x => new { x.ClientId, x.FieldKey, x.Value }).IsUnique();
        });

        modelBuilder.Entity<CustomField>(b =>
        {
            b.ToTable("CustomFields");
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).HasMaxLength(150).IsRequired();
            b.Property(x => x.DataType).HasMaxLength(50).IsRequired();
            b.Property(x => x.Options).HasColumnType("longtext");
            b.HasIndex(x => x.ClientId);
        });

        modelBuilder.Entity<CustomFieldValue>(b =>
        {
            b.ToTable("CustomFieldValues");
            b.HasKey(x => x.Id);
            b.Property(x => x.Value).HasColumnType("longtext").IsRequired();
            b.HasIndex(x => new { x.IncidentId, x.CustomFieldId }).IsUnique();
        });

        modelBuilder.Entity<IncidentEvent>(b =>
        {
            b.ToTable("IncidentEvents");
            b.HasKey(x => x.Id);
            b.Property(x => x.EventType).HasMaxLength(50).IsRequired();
            b.Property(x => x.UserDisplayName).HasMaxLength(128).IsRequired();
            b.Property(x => x.Body).HasColumnType("longtext").IsRequired();
            b.HasIndex(x => new { x.IncidentId, x.CreatedAt });
        });

        modelBuilder.Entity<IncidentAttachment>(b =>
        {
            b.ToTable("IncidentAttachments");
            b.HasKey(x => x.Id);
            b.Property(x => x.FileName).HasMaxLength(255).IsRequired();
            b.Property(x => x.StorageKey).HasMaxLength(500).IsRequired();
            b.Property(x => x.ContentType).HasMaxLength(255).IsRequired();
            b.Property(x => x.UploadedByDisplayName).HasMaxLength(128).IsRequired();
            b.HasIndex(x => new { x.IncidentId, x.CreatedAt });
        });
    }
}
