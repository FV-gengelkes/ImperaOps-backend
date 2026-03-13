using ImperaOps.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ImperaOps.Infrastructure.Data;

public sealed class ImperaOpsDbContext : DbContext
{
    public ImperaOpsDbContext(DbContextOptions<ImperaOpsDbContext> options) : base(options) { }

    public DbSet<Event> Events => Set<Event>();
    public DbSet<EventType> EventTypes => Set<EventType>();
    public DbSet<WorkflowStatus> WorkflowStatuses => Set<WorkflowStatus>();
    public DbSet<WorkflowTransition> WorkflowTransitions => Set<WorkflowTransition>();
    public DbSet<EventTask> Tasks => Set<EventTask>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<ClientCounter> ClientCounters => Set<ClientCounter>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<UserClientAccess> UserClientAccess => Set<UserClientAccess>();
    public DbSet<CustomField> CustomFields => Set<CustomField>();
    public DbSet<CustomFieldValue> CustomFieldValues => Set<CustomFieldValue>();
    public DbSet<UserToken> UserTokens => Set<UserToken>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<RootCauseTaxonomyItem> RootCauseTaxonomyItems => Set<RootCauseTaxonomyItem>();
    public DbSet<SlaRule> SlaRules => Set<SlaRule>();
    public DbSet<ClientWebhook> ClientWebhooks => Set<ClientWebhook>();
    public DbSet<EventLinkGroup> EventLinkGroups => Set<EventLinkGroup>();
    public DbSet<EventLink> EventLinks => Set<EventLink>();
    public DbSet<InsightAlert> InsightAlerts => Set<InsightAlert>();
    public DbSet<Investigation> Investigations => Set<Investigation>();
    public DbSet<InvestigationWitness> InvestigationWitnesses => Set<InvestigationWitness>();
    public DbSet<InvestigationEvidence> InvestigationEvidence => Set<InvestigationEvidence>();
    public DbSet<ClientDocument> ClientDocuments => Set<ClientDocument>();
    public DbSet<DocumentReference> DocumentReferences => Set<DocumentReference>();
    public DbSet<ApiCredential> ApiCredentials => Set<ApiCredential>();
    public DbSet<ApiRequestLog> ApiRequestLogs => Set<ApiRequestLog>();
    public DbSet<ApiCredentialAuditLog> ApiCredentialAuditLogs => Set<ApiCredentialAuditLog>();
    public DbSet<WorkflowRule> WorkflowRules => Set<WorkflowRule>();
    public DbSet<WorkflowRuleExecution> WorkflowRuleExecutions => Set<WorkflowRuleExecution>();
    public DbSet<ReportSchedule> ReportSchedules => Set<ReportSchedule>();
    public DbSet<RoundRobinState> RoundRobinStates => Set<RoundRobinState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Event>(b =>
        {
            b.ToTable("Events");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedOnAdd();
            b.Property(x => x.PublicId).HasMaxLength(20).IsRequired();
            b.Property(x => x.Title).HasMaxLength(500).IsRequired();
            b.Property(x => x.Location).HasMaxLength(256);
            b.Property(x => x.Description).HasColumnType("longtext");
            b.Property(x => x.ExternalReporterName).HasMaxLength(200);
            b.Property(x => x.ExternalReporterContact).HasMaxLength(200);
            b.Property(x => x.CorrectiveAction).HasColumnType("longtext");
            b.Property(x => x.CreatedVia).HasMaxLength(32);
            b.Property(x => x.Source).HasMaxLength(100);
            b.Property(x => x.ExternalId).HasMaxLength(255);
            b.Property(x => x.DeletedAt).HasColumnType("datetime(6)");
            b.HasIndex(x => new { x.ClientId, x.OccurredAt });
            b.HasIndex(x => new { x.ClientId, x.ReferenceNumber }).IsUnique();
            b.HasIndex(x => new { x.ClientId, x.PublicId }).IsUnique();
            b.HasIndex(x => x.DeletedAt);
            b.HasQueryFilter(x => x.DeletedAt == null);
        });

        modelBuilder.Entity<EventType>(b =>
        {
            b.ToTable("EventTypes");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedOnAdd();
            b.Property(x => x.Name).HasMaxLength(150).IsRequired();
            b.Property(x => x.DeletedAt).HasColumnType("datetime(6)");
            b.HasIndex(x => x.ClientId);
            b.HasQueryFilter(x => x.DeletedAt == null);
        });

        modelBuilder.Entity<WorkflowStatus>(b =>
        {
            b.ToTable("WorkflowStatuses");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedOnAdd();
            b.Property(x => x.Name).HasMaxLength(150).IsRequired();
            b.Property(x => x.Color).HasMaxLength(7);
            b.Property(x => x.DeletedAt).HasColumnType("datetime(6)");
            b.HasIndex(x => x.ClientId);
            b.HasQueryFilter(x => x.DeletedAt == null);
        });

        modelBuilder.Entity<WorkflowTransition>(b =>
        {
            b.ToTable("WorkflowTransitions");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedOnAdd();
            b.Property(x => x.Label).HasMaxLength(100);
            b.Property(x => x.DeletedAt).HasColumnType("datetime(6)");
            b.HasIndex(x => x.ClientId);
            b.HasQueryFilter(x => x.DeletedAt == null);
        });

        modelBuilder.Entity<EventTask>(b =>
        {
            b.ToTable("Tasks");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedOnAdd();
            b.Property(x => x.PublicId).HasMaxLength(20).IsRequired();
            b.Property(x => x.Title).HasMaxLength(500).IsRequired();
            b.Property(x => x.Description).HasColumnType("longtext");
            b.Property(x => x.DeletedAt).HasColumnType("datetime(6)");
            b.HasIndex(x => new { x.EventId, x.SortOrder });
            b.HasIndex(x => x.DeletedAt);
            b.HasQueryFilter(x => x.DeletedAt == null);
        });

        modelBuilder.Entity<AuditEvent>(b =>
        {
            b.ToTable("AuditEvents");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedOnAdd();
            b.Property(x => x.EntityType).HasMaxLength(50).IsRequired();
            b.Property(x => x.EventType).HasMaxLength(50).IsRequired();
            b.Property(x => x.UserDisplayName).HasMaxLength(128).IsRequired();
            b.Property(x => x.Body).HasColumnType("longtext").IsRequired();
            b.Property(x => x.DeletedAt).HasColumnType("datetime(6)");
            b.HasIndex(x => new { x.EntityType, x.EntityId, x.CreatedAt });
            b.HasQueryFilter(x => x.DeletedAt == null);
        });

        modelBuilder.Entity<Attachment>(b =>
        {
            b.ToTable("Attachments");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedOnAdd();
            b.Property(x => x.EntityType).HasMaxLength(50).IsRequired();
            b.Property(x => x.FileName).HasMaxLength(255).IsRequired();
            b.Property(x => x.StorageKey).HasMaxLength(500).IsRequired();
            b.Property(x => x.ContentType).HasMaxLength(255).IsRequired();
            b.Property(x => x.UploadedByDisplayName).HasMaxLength(128).IsRequired();
            b.Property(x => x.DeletedAt).HasColumnType("datetime(6)");
            b.HasIndex(x => new { x.EntityType, x.EntityId, x.CreatedAt });
            b.HasQueryFilter(x => x.DeletedAt == null);
        });

        modelBuilder.Entity<ClientCounter>(b =>
        {
            b.ToTable("ClientCounters");
            b.HasKey(x => new { x.ClientId, x.CounterName });
            b.Property(x => x.CounterName).HasMaxLength(50).IsRequired();
            b.Property(x => x.DeletedAt).HasColumnType("datetime(6)");
            b.HasQueryFilter(x => x.DeletedAt == null);
        });

        modelBuilder.Entity<AppUser>(b =>
        {
            b.ToTable("Users");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedOnAdd();
            b.Property(x => x.Email).HasMaxLength(256).IsRequired();
            b.Property(x => x.DisplayName).HasMaxLength(128).IsRequired();
            b.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
            b.Property(x => x.TotpSecret).HasMaxLength(64);
            b.Property(x => x.DeletedAt).HasColumnType("datetime(6)");
            b.HasIndex(x => x.Email).IsUnique();
            b.HasQueryFilter(x => x.DeletedAt == null);
        });

        modelBuilder.Entity<Client>(b =>
        {
            b.ToTable("Clients");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedOnAdd();
            b.Property(x => x.Name).HasMaxLength(256).IsRequired();
            b.Property(x => x.Slug).HasMaxLength(100).IsRequired();
            b.Property(x => x.AppliedTemplateIds).HasColumnType("longtext");
            b.Property(x => x.LogoStorageKey).HasMaxLength(500);
            b.Property(x => x.PrimaryColor).HasMaxLength(7);
            b.Property(x => x.SystemName).HasMaxLength(100);
            b.Property(x => x.LinkColor).HasMaxLength(7);
            b.Property(x => x.InboundEmailSlug).HasMaxLength(100);
            b.Property(x => x.ClientSid).HasMaxLength(64).IsRequired();
            b.Property(x => x.DeletedAt).HasColumnType("datetime(6)");
            b.HasIndex(x => x.Slug).IsUnique();
            b.HasIndex(x => x.ClientSid).IsUnique();
            b.HasIndex(x => x.InboundEmailSlug).IsUnique();
            b.HasIndex(x => x.ParentClientId);
            b.HasQueryFilter(x => x.DeletedAt == null);
        });

        modelBuilder.Entity<UserClientAccess>(b =>
        {
            b.ToTable("UserClientAccess");
            b.HasKey(x => new { x.UserId, x.ClientId });
            b.Property(x => x.Role).HasMaxLength(64).IsRequired();
            b.Property(x => x.DeletedAt).HasColumnType("datetime(6)");
            b.HasQueryFilter(x => x.DeletedAt == null);
        });

        modelBuilder.Entity<CustomField>(b =>
        {
            b.ToTable("CustomFields");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedOnAdd();
            b.Property(x => x.Name).HasMaxLength(150).IsRequired();
            b.Property(x => x.DataType).HasMaxLength(50).IsRequired();
            b.Property(x => x.Options).HasColumnType("longtext");
            b.Property(x => x.DeletedAt).HasColumnType("datetime(6)");
            b.HasIndex(x => x.ClientId);
            b.HasQueryFilter(x => x.DeletedAt == null);
        });

        modelBuilder.Entity<CustomFieldValue>(b =>
        {
            b.ToTable("CustomFieldValues");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedOnAdd();
            b.Property(x => x.Value).HasColumnType("longtext").IsRequired();
            b.Property(x => x.DeletedAt).HasColumnType("datetime(6)");
            b.HasIndex(x => new { x.EntityId, x.CustomFieldId }).IsUnique();
            b.HasQueryFilter(x => x.DeletedAt == null);
        });

        modelBuilder.Entity<UserToken>(b =>
        {
            b.ToTable("UserTokens");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedOnAdd();
            b.Property(x => x.Token).HasMaxLength(64).IsRequired();
            b.Property(x => x.Type).HasMaxLength(32).IsRequired();
            b.Property(x => x.Description).HasMaxLength(500);
            b.Property(x => x.DeletedAt).HasColumnType("datetime(6)");
            b.HasIndex(x => x.Token).IsUnique();
            b.HasIndex(x => new { x.UserId, x.Type });
            b.HasQueryFilter(x => x.DeletedAt == null);
        });

        modelBuilder.Entity<Notification>(b =>
        {
            b.ToTable("Notifications");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedOnAdd();
            b.Property(x => x.NotificationType).HasMaxLength(50).IsRequired();
            b.Property(x => x.Title).HasMaxLength(255).IsRequired();
            b.Property(x => x.Body).HasColumnType("longtext").IsRequired();
            b.Property(x => x.EntityPublicId).HasMaxLength(20);
            b.Property(x => x.SubEntityPublicId).HasMaxLength(20);
            b.Property(x => x.DeletedAt).HasColumnType("datetime(6)");
            b.HasIndex(x => new { x.UserId, x.IsRead, x.CreatedAt });
            b.HasIndex(x => new { x.UserId, x.CreatedAt });
            b.HasIndex(x => x.DeletedAt);
            b.HasQueryFilter(x => x.DeletedAt == null);
        });

        modelBuilder.Entity<NotificationPreference>(b =>
        {
            b.ToTable("NotificationPreferences");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedOnAdd();
            b.Property(x => x.NotificationType).HasMaxLength(50).IsRequired();
            b.Property(x => x.DeletedAt).HasColumnType("datetime(6)");
            b.HasIndex(x => new { x.UserId, x.NotificationType }).IsUnique();
            b.HasQueryFilter(x => x.DeletedAt == null);
        });

        modelBuilder.Entity<RootCauseTaxonomyItem>(b =>
        {
            b.ToTable("RootCauseTaxonomyItems");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedOnAdd();
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.DeletedAt).HasColumnType("datetime(6)");
            b.HasIndex(x => x.ClientId);
            b.HasQueryFilter(x => x.DeletedAt == null);
        });

        modelBuilder.Entity<SlaRule>(b =>
        {
            b.ToTable("SlaRules");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedOnAdd();
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.DeletedAt).HasColumnType("datetime(6)");
            b.HasIndex(x => x.ClientId);
            b.HasQueryFilter(x => x.DeletedAt == null);
        });

        modelBuilder.Entity<ClientWebhook>(b =>
        {
            b.ToTable("ClientWebhooks");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedOnAdd();
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.Url).HasMaxLength(500).IsRequired();
            b.Property(x => x.Secret).HasMaxLength(256);
            b.Property(x => x.EventTypes).HasColumnType("longtext").IsRequired();
            b.Property(x => x.DeletedAt).HasColumnType("datetime(6)");
            b.HasIndex(x => x.ClientId);
            b.HasQueryFilter(x => x.DeletedAt == null);
        });

        modelBuilder.Entity<EventLinkGroup>(b =>
        {
            b.ToTable("EventLinkGroups");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedOnAdd();
            b.Property(x => x.Title).HasMaxLength(500).IsRequired();
            b.Property(x => x.Description).HasColumnType("longtext");
            b.Property(x => x.DeletedAt).HasColumnType("datetime(6)");
            b.HasIndex(x => x.ClientId);
            b.HasQueryFilter(x => x.DeletedAt == null);
        });

        modelBuilder.Entity<EventLink>(b =>
        {
            b.ToTable("EventLinks");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedOnAdd();
            b.Property(x => x.DeletedAt).HasColumnType("datetime(6)");
            b.HasIndex(x => new { x.ClientId, x.LinkGroupId });
            b.HasIndex(x => new { x.LinkGroupId, x.EventId }).IsUnique();
            b.HasQueryFilter(x => x.DeletedAt == null);
        });

        modelBuilder.Entity<InsightAlert>(b =>
        {
            b.ToTable("InsightAlerts");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedOnAdd();
            b.Property(x => x.AlertType).HasMaxLength(50).IsRequired();
            b.Property(x => x.Severity).HasMaxLength(20).IsRequired();
            b.Property(x => x.Title).HasMaxLength(500).IsRequired();
            b.Property(x => x.Body).HasColumnType("longtext").IsRequired();
            b.Property(x => x.MetadataJson).HasColumnType("longtext");
            b.Property(x => x.RelatedEventIds).HasColumnType("longtext");
            b.Property(x => x.AiSummary).HasColumnType("longtext");
            b.Property(x => x.DeletedAt).HasColumnType("datetime(6)");
            b.HasIndex(x => new { x.ClientId, x.AlertType, x.GeneratedAt });
            b.HasIndex(x => new { x.ClientId, x.IsAcknowledged });
            b.HasQueryFilter(x => x.DeletedAt == null);
        });

        modelBuilder.Entity<Investigation>(b =>
        {
            b.ToTable("Investigations");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedOnAdd();
            b.Property(x => x.Status).HasMaxLength(50).IsRequired();
            b.Property(x => x.Summary).HasColumnType("longtext");
            b.Property(x => x.RootCauseAnalysis).HasColumnType("longtext");
            b.Property(x => x.CorrectiveActions).HasColumnType("longtext");
            b.Property(x => x.DeletedAt).HasColumnType("datetime(6)");
            b.HasIndex(x => x.EventId).IsUnique();
            b.HasIndex(x => new { x.ClientId, x.Status });
            b.HasQueryFilter(x => x.DeletedAt == null);
        });

        modelBuilder.Entity<InvestigationWitness>(b =>
        {
            b.ToTable("InvestigationWitnesses");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedOnAdd();
            b.Property(x => x.WitnessName).HasMaxLength(200).IsRequired();
            b.Property(x => x.WitnessContact).HasMaxLength(200);
            b.Property(x => x.Statement).HasColumnType("longtext").IsRequired();
            b.Property(x => x.DeletedAt).HasColumnType("datetime(6)");
            b.HasIndex(x => new { x.InvestigationId, x.SortOrder });
            b.HasQueryFilter(x => x.DeletedAt == null);
        });

        modelBuilder.Entity<ClientDocument>(b =>
        {
            b.ToTable("ClientDocuments");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedOnAdd();
            b.Property(x => x.Title).HasMaxLength(300).IsRequired();
            b.Property(x => x.Description).HasColumnType("longtext");
            b.Property(x => x.Category).HasMaxLength(50).IsRequired();
            b.Property(x => x.StorageKey).HasMaxLength(500).IsRequired();
            b.Property(x => x.FileName).HasMaxLength(255).IsRequired();
            b.Property(x => x.ContentType).HasMaxLength(255).IsRequired();
            b.Property(x => x.UploadedByDisplayName).HasMaxLength(128).IsRequired();
            b.Property(x => x.DeletedAt).HasColumnType("datetime(6)");
            b.HasIndex(x => new { x.ClientId, x.Category });
            b.HasQueryFilter(x => x.DeletedAt == null);
        });

        modelBuilder.Entity<DocumentReference>(b =>
        {
            b.ToTable("DocumentReferences");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedOnAdd();
            b.Property(x => x.EntityType).HasMaxLength(50).IsRequired();
            b.Property(x => x.DeletedAt).HasColumnType("datetime(6)");
            b.HasIndex(x => new { x.EntityType, x.EntityId });
            b.HasIndex(x => x.DocumentId);
            b.HasIndex(x => new { x.DocumentId, x.EntityType, x.EntityId }).IsUnique();
            b.HasQueryFilter(x => x.DeletedAt == null);
        });

        modelBuilder.Entity<InvestigationEvidence>(b =>
        {
            b.ToTable("InvestigationEvidence");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedOnAdd();
            b.Property(x => x.Title).HasMaxLength(500).IsRequired();
            b.Property(x => x.Description).HasColumnType("longtext");
            b.Property(x => x.EvidenceType).HasMaxLength(50).IsRequired();
            b.Property(x => x.DeletedAt).HasColumnType("datetime(6)");
            b.HasIndex(x => new { x.InvestigationId, x.SortOrder });
            b.HasQueryFilter(x => x.DeletedAt == null);
        });

        modelBuilder.Entity<ApiCredential>(b =>
        {
            b.ToTable("ApiCredentials");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedOnAdd();
            b.Property(x => x.Name).HasMaxLength(255).IsRequired();
            b.Property(x => x.KeyId).HasMaxLength(64).IsRequired();
            b.Property(x => x.SecretHash).HasMaxLength(255).IsRequired();
            b.Property(x => x.SecretLast4).HasMaxLength(8).IsRequired();
            b.Property(x => x.ScopesJson).HasColumnType("json").IsRequired();
            b.Property(x => x.Status).HasMaxLength(20).IsRequired();
            b.Property(x => x.LastUsedIp).HasMaxLength(64);
            b.Property(x => x.DeletedAt).HasColumnType("datetime(6)");
            b.HasIndex(x => x.KeyId).IsUnique();
            b.HasIndex(x => new { x.ClientId, x.Status });
            b.HasQueryFilter(x => x.DeletedAt == null);
        });

        modelBuilder.Entity<ApiRequestLog>(b =>
        {
            b.ToTable("ApiRequestLogs");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedOnAdd();
            b.Property(x => x.Method).HasMaxLength(16).IsRequired();
            b.Property(x => x.Path).HasMaxLength(255).IsRequired();
            b.Property(x => x.IpAddress).HasMaxLength(64);
            b.Property(x => x.UserAgent).HasMaxLength(255);
            b.Property(x => x.RequestId).HasMaxLength(64).IsRequired();
            b.HasIndex(x => new { x.ClientId, x.CreatedAt });
        });

        modelBuilder.Entity<ApiCredentialAuditLog>(b =>
        {
            b.ToTable("ApiCredentialAuditLogs");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedOnAdd();
            b.Property(x => x.Action).HasMaxLength(32).IsRequired();
            b.Property(x => x.DetailsJson).HasColumnType("json");
            b.HasIndex(x => x.ApiCredentialId);
        });

        modelBuilder.Entity<WorkflowRule>(b =>
        {
            b.ToTable("WorkflowRules");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedOnAdd();
            b.Property(x => x.Name).HasMaxLength(255).IsRequired();
            b.Property(x => x.Description).HasColumnType("longtext");
            b.Property(x => x.TriggerType).HasMaxLength(50).IsRequired();
            b.Property(x => x.ConditionsJson).HasColumnType("longtext").IsRequired();
            b.Property(x => x.ActionsJson).HasColumnType("longtext").IsRequired();
            b.Property(x => x.DeletedAt).HasColumnType("datetime(6)");
            b.HasIndex(x => new { x.ClientId, x.TriggerType });
            b.HasIndex(x => x.DeletedAt);
            b.HasQueryFilter(x => x.DeletedAt == null);
        });

        modelBuilder.Entity<WorkflowRuleExecution>(b =>
        {
            b.ToTable("WorkflowRuleExecutions");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedOnAdd();
            b.Property(x => x.TriggerType).HasMaxLength(50).IsRequired();
            b.Property(x => x.ErrorMessage).HasColumnType("longtext");
            b.HasIndex(x => x.WorkflowRuleId);
            b.HasIndex(x => x.EventId);
            b.HasIndex(x => new { x.ClientId, x.ExecutedAt });
        });

        modelBuilder.Entity<ReportSchedule>(b =>
        {
            b.ToTable("ReportSchedules");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedOnAdd();
            b.Property(x => x.Frequency).HasMaxLength(20).IsRequired();
            b.HasIndex(x => x.ClientId).IsUnique();
        });

        modelBuilder.Entity<RoundRobinState>(b =>
        {
            b.ToTable("RoundRobinStates");
            b.HasKey(x => x.WorkflowRuleId);
            b.Property(x => x.WorkflowRuleId).ValueGeneratedNever();
        });

        // Convention: all ISeedable entities get IsSeedData column with default false
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ISeedable).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType)
                    .Property(nameof(ISeedable.IsSeedData)).HasDefaultValue(false);
            }
        }
    }
}
