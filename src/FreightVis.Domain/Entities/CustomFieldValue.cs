namespace FreightVis.Domain.Entities;

public sealed class CustomFieldValue
{
    public Guid Id { get; set; }

    public Guid IncidentId { get; set; }

    public Guid CustomFieldId { get; set; }

    /// <summary>
    /// Stored as a string regardless of DataType; frontend parses based on the field's DataType.
    /// Multi-select values are stored as a JSON array of strings.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
