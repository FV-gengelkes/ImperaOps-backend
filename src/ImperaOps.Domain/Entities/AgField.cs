namespace ImperaOps.Domain.Entities;

public sealed class AgField : ISoftDeletable
{
    public long Id { get; set; }
    public long ClientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal? Acreage { get; set; }
    public string? GrowerName { get; set; }
    public string? GrowerContact { get; set; }
    public string? Address { get; set; }
    public string? BoundaryGeoJson { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
