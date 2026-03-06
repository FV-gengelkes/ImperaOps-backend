namespace ImperaOps.Domain.Entities;

public sealed class CustomFieldValue : ISoftDeletable
{
    public long Id { get; set; }
    /// <summary>References Events.Id (previously IncidentId).</summary>
    public long EntityId { get; set; }
    public long CustomFieldId { get; set; }
    /// <summary>
    /// Stored as a string regardless of DataType; frontend parses based on the field's DataType.
    /// Multi-select values are stored as a JSON array of strings.
    /// </summary>
    public string Value { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
