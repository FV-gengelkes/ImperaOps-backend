namespace FreightVis.Domain.Entities;

public sealed class IncidentEvent
{
    public Guid Id { get; set; }

    public Guid IncidentId { get; set; }

    public Guid ClientId { get; set; }

    /// <summary>One of: incident_created, type_changed, status_changed, owner_changed, comment</summary>
    public string EventType { get; set; } = string.Empty;

    public Guid? UserId { get; set; }

    public string UserDisplayName { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
