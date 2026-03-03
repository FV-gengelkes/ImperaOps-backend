namespace FreightVis.Domain.Entities;

public sealed class IncidentLookup
{
    public Guid Id { get; set; }

    /// <summary>Guid.Empty means this is a system-default row.</summary>
    public Guid ClientId { get; set; }

    /// <summary>"incident_type" or "status"</summary>
    public string FieldKey { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    /// <summary>The int stored in Incident.Type / Incident.Status.</summary>
    public int Value { get; set; }

    public int SortOrder { get; set; }

    /// <summary>When true, clients cannot edit or delete this row.</summary>
    public bool IsSystem { get; set; }

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
