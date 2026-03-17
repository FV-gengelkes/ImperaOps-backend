namespace ImperaOps.Api.Contracts;

// ── Fields ───────────────────────────────────────────────────────────────────

public sealed record AgFieldDto(
    long Id, long ClientId, string Name, decimal? Acreage,
    string? GrowerName, string? GrowerContact, string? Address,
    string? BoundaryGeoJson, string? Notes, int SprayJobCount,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record AgFieldListItemDto(
    long Id, string Name, decimal? Acreage,
    string? GrowerName, string? Address, int SprayJobCount,
    DateTimeOffset CreatedAt);

public sealed record CreateAgFieldRequest(
    string Name, decimal? Acreage, string? GrowerName,
    string? GrowerContact, string? Address, string? BoundaryGeoJson, string? Notes);

public sealed record UpdateAgFieldRequest(
    string Name, decimal? Acreage, string? GrowerName,
    string? GrowerContact, string? Address, string? BoundaryGeoJson, string? Notes);

// ── Spray Jobs ───────────────────────────────────────────────────────────────

public sealed record SprayJobDto(
    long Id, long ClientId, long FieldId, string? FieldName,
    string JobNumber, string Status, DateTime? ScheduledDate, DateTime? CompletedDate,
    string? DroneOperator, string? Product, string? ApplicationRate, string? ApplicationUnit,
    string? WeatherConditions, string? FlightLogGeoJson, string? Notes,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record SprayJobListItemDto(
    long Id, string JobNumber, long FieldId, string? FieldName,
    string Status, DateTime? ScheduledDate, DateTime? CompletedDate,
    string? DroneOperator, string? Product, DateTimeOffset CreatedAt);

public sealed record CreateSprayJobRequest(
    long FieldId, DateTime? ScheduledDate,
    string? DroneOperator, string? Product, string? ApplicationRate,
    string? ApplicationUnit, string? WeatherConditions, string? Notes);

public sealed record UpdateSprayJobRequest(
    long FieldId, string Status, DateTime? ScheduledDate, DateTime? CompletedDate,
    string? DroneOperator, string? Product, string? ApplicationRate,
    string? ApplicationUnit, string? WeatherConditions, string? FlightLogGeoJson, string? Notes);
