namespace ImperaOps.Domain.Entities;

public sealed class SprayJob : ISoftDeletable
{
    public long Id { get; set; }
    public long ClientId { get; set; }
    public long FieldId { get; set; }
    public string JobNumber { get; set; } = string.Empty;
    public long ReferenceNumber { get; set; }
    /// <summary>scheduled | in_progress | completed | cancelled</summary>
    public string Status { get; set; } = "scheduled";
    public DateTime? ScheduledDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public string? DroneOperator { get; set; }
    public string? Product { get; set; }
    public string? ApplicationRate { get; set; }
    public string? ApplicationUnit { get; set; }
    public string? WeatherConditions { get; set; }
    public string? FlightLogGeoJson { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
