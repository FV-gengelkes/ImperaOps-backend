namespace FreightVis.Application.Incidents.Dtos;

public sealed record IncidentAnalyticsDto(
    int Total,
    int Open,
    int InProgress,
    int Blocked,
    int Closed,
    int ThisMonth,
    int LastMonth,
    IReadOnlyList<TypeCountDto> ByType,
    IReadOnlyList<MonthlyRowDto> ByMonth,
    IReadOnlyList<LocationCountDto> TopLocations,
    IReadOnlyList<LocationTypeCountDto> ByLocationAndType
);

public sealed record TypeCountDto(int Type, long Count);

public sealed record MonthlyRowDto(int Year, int Month, int Type, long Count);

public sealed record LocationCountDto(string Location, long Count);

public sealed record LocationTypeCountDto(string Location, int Type, long Count);
