using System.Text;
using Dapper;
using FreightVis.Application.Abstractions;
using FreightVis.Application.Incidents.Dtos;
using MySqlConnector;

namespace FreightVis.Infrastructure.Repositories;

public sealed class IncidentReadRepository : IIncidentReadRepository
{
    private readonly string _connectionString;

    public IncidentReadRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    // ── List (with optional filters) ────────────────────────────────────────

    public async Task<PagedResult<IncidentListItemDto>> GetListAsync(
        Guid clientId, int page, int pageSize,
        int? type, int? status, DateTime? dateFrom, DateTime? dateTo,
        string? search, CancellationToken ct)
    {
        page     = page     <= 0 ? 1  : page;
        pageSize = pageSize <= 0 ? 25 : Math.Min(pageSize, 200);
        var offset = (page - 1) * pageSize;

        var where = new StringBuilder("WHERE i.ClientId = @ClientId");
        if (type.HasValue)     where.Append(" AND i.Type = @Type");
        if (status.HasValue)   where.Append(" AND i.Status = @Status");
        if (dateFrom.HasValue) where.Append(" AND DATE(i.OccurredAt) >= DATE(@DateFrom)");
        if (dateTo.HasValue)   where.Append(" AND DATE(i.OccurredAt) <= DATE(@DateTo)");
        if (!string.IsNullOrWhiteSpace(search))
            where.Append(@" AND (
                i.Location LIKE CONCAT('%', @Search, '%')
                OR i.Description LIKE CONCAT('%', @Search, '%')
                OR EXISTS (
                    SELECT 1 FROM IncidentLookups il
                    WHERE (il.ClientId = @ClientId OR il.ClientId = @EmptyGuid)
                      AND il.FieldKey = 'incident_type'
                      AND il.Value = i.Type
                      AND il.IsActive = 1
                      AND il.Label LIKE CONCAT('%', @Search, '%')
                )
                OR EXISTS (
                    SELECT 1 FROM IncidentLookups il
                    WHERE (il.ClientId = @ClientId OR il.ClientId = @EmptyGuid)
                      AND il.FieldKey = 'status'
                      AND il.Value = i.Status
                      AND il.IsActive = 1
                      AND il.Label LIKE CONCAT('%', @Search, '%')
                )
            )");

        var countSql = $"SELECT COUNT(1) FROM Incidents i {where};";
        var pageSql  = $@"
SELECT i.Id, i.ClientId, i.Type, i.Status, i.OccurredAt, i.Location, i.OwnerUserId, i.ReferenceNumber,
       u.DisplayName AS OwnerDisplayName
FROM Incidents i
LEFT JOIN Users u ON u.Id = i.OwnerUserId
{where}
ORDER BY i.OccurredAt DESC
LIMIT @PageSize OFFSET @Offset;";

        var param = new DynamicParameters();
        param.Add("ClientId", clientId);
        if (type.HasValue)     param.Add("Type",     type.Value);
        if (status.HasValue)   param.Add("Status",   status.Value);
        if (dateFrom.HasValue) param.Add("DateFrom", dateFrom.Value.Date);
        if (dateTo.HasValue)   param.Add("DateTo",   dateTo.Value.Date);
        if (!string.IsNullOrWhiteSpace(search))
        {
            param.Add("Search",    search.Trim());
            param.Add("EmptyGuid", Guid.Empty);
        }
        param.Add("PageSize", pageSize);
        param.Add("Offset",   offset);

        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var total = await conn.ExecuteScalarAsync<int>(new CommandDefinition(countSql, param, cancellationToken: ct));
        var items = (await conn.QueryAsync<IncidentListItemDto>(new CommandDefinition(pageSql, param, cancellationToken: ct))).ToList();

        return new PagedResult<IncidentListItemDto>(items, total, page, pageSize);
    }

    // ── Export (all matching rows, no pagination) ────────────────────────────

    private sealed record ExportRow(int ReferenceNumber, DateTime OccurredAt, int Type, int Status,
        string Location, string Description, string? OwnerDisplayName);

    public async Task<IReadOnlyList<IncidentExportRowDto>> GetExportDataAsync(
        Guid clientId, int? type, int? status,
        DateTime? dateFrom, DateTime? dateTo,
        string? search, CancellationToken ct)
    {
        var where = new StringBuilder("WHERE i.ClientId = @ClientId");
        if (type.HasValue)     where.Append(" AND i.Type = @Type");
        if (status.HasValue)   where.Append(" AND i.Status = @Status");
        if (dateFrom.HasValue) where.Append(" AND DATE(i.OccurredAt) >= DATE(@DateFrom)");
        if (dateTo.HasValue)   where.Append(" AND DATE(i.OccurredAt) <= DATE(@DateTo)");
        if (!string.IsNullOrWhiteSpace(search))
            where.Append(@" AND (
                i.Location LIKE CONCAT('%', @Search, '%')
                OR i.Description LIKE CONCAT('%', @Search, '%')
                OR EXISTS (
                    SELECT 1 FROM IncidentLookups il
                    WHERE (il.ClientId = @ClientId OR il.ClientId = @EmptyGuid)
                      AND il.FieldKey = 'incident_type'
                      AND il.Value = i.Type
                      AND il.IsActive = 1
                      AND il.Label LIKE CONCAT('%', @Search, '%')
                )
                OR EXISTS (
                    SELECT 1 FROM IncidentLookups il
                    WHERE (il.ClientId = @ClientId OR il.ClientId = @EmptyGuid)
                      AND il.FieldKey = 'status'
                      AND il.Value = i.Status
                      AND il.IsActive = 1
                      AND il.Label LIKE CONCAT('%', @Search, '%')
                )
            )");

        var sql = $@"
SELECT i.ReferenceNumber, i.OccurredAt, i.Type, i.Status, i.Location, i.Description,
       u.DisplayName AS OwnerDisplayName
FROM Incidents i
LEFT JOIN Users u ON u.Id = i.OwnerUserId
{where}
ORDER BY i.OccurredAt DESC;";

        const string lookupSql = @"
SELECT FieldKey, Value, Label
FROM IncidentLookups
WHERE (ClientId = @ClientId OR ClientId = @EmptyGuid)
  AND FieldKey IN ('incident_type', 'status')
  AND IsActive = 1
ORDER BY IsSystem ASC;";   // system rows first; client rows overwrite

        var param = new DynamicParameters();
        param.Add("ClientId",  clientId);
        param.Add("EmptyGuid", Guid.Empty);
        if (type.HasValue)     param.Add("Type",     type.Value);
        if (status.HasValue)   param.Add("Status",   status.Value);
        if (dateFrom.HasValue) param.Add("DateFrom", dateFrom.Value.Date);
        if (dateTo.HasValue)   param.Add("DateTo",   dateTo.Value.Date);
        if (!string.IsNullOrWhiteSpace(search))
            param.Add("Search", search.Trim());

        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var rows    = (await conn.QueryAsync<ExportRow>(new CommandDefinition(sql, param, cancellationToken: ct))).ToList();
        var lookups = (await conn.QueryAsync<(string FieldKey, int Value, string Label)>(
                          new CommandDefinition(lookupSql, new { ClientId = clientId, EmptyGuid = Guid.Empty }, cancellationToken: ct)))
                      .ToList();

        var typeLabels   = new Dictionary<int, string>();
        var statusLabels = new Dictionary<int, string>();
        foreach (var l in lookups)
        {
            var dict = l.FieldKey == "incident_type" ? typeLabels : statusLabels;
            dict[l.Value] = l.Label;   // later entries (client) overwrite earlier (system)
        }

        return rows.Select(r => new IncidentExportRowDto(
            r.ReferenceNumber,
            r.OccurredAt,
            typeLabels.GetValueOrDefault(r.Type,   r.Type.ToString()),
            statusLabels.GetValueOrDefault(r.Status, r.Status.ToString()),
            r.Location,
            r.Description,
            r.OwnerDisplayName
        )).ToList();
    }

    // ── Analytics ────────────────────────────────────────────────────────────

    private sealed record SummaryRow(long Total, decimal Open, decimal InProgress, decimal Blocked, decimal Closed, decimal ThisMonth, decimal LastMonth);

    public async Task<IncidentAnalyticsDto> GetAnalyticsAsync(IReadOnlyList<Guid> clientIds, CancellationToken ct)
    {
        const string summarySql = @"
SELECT
    COUNT(*)                                                                           AS Total,
    SUM(Status = 1)                                                                    AS Open,
    SUM(Status = 2)                                                                    AS InProgress,
    SUM(Status = 3)                                                                    AS Blocked,
    SUM(Status = 4)                                                                    AS Closed,
    SUM(YEAR(OccurredAt) = YEAR(CURDATE()) AND MONTH(OccurredAt) = MONTH(CURDATE()))  AS ThisMonth,
    SUM(YEAR(OccurredAt) = YEAR(DATE_SUB(CURDATE(), INTERVAL 1 MONTH))
        AND MONTH(OccurredAt) = MONTH(DATE_SUB(CURDATE(), INTERVAL 1 MONTH)))         AS LastMonth
FROM Incidents
WHERE ClientId IN @ClientIds;";

        const string byTypeSql = @"
SELECT Type, COUNT(*) AS Count
FROM Incidents
WHERE ClientId IN @ClientIds
GROUP BY Type
ORDER BY Count DESC;";

        const string byMonthSql = @"
SELECT YEAR(OccurredAt) AS Year, MONTH(OccurredAt) AS Month, Type, COUNT(*) AS Count
FROM Incidents
WHERE ClientId IN @ClientIds
  AND OccurredAt >= DATE_SUB(CURDATE(), INTERVAL 12 MONTH)
GROUP BY YEAR(OccurredAt), MONTH(OccurredAt), Type
ORDER BY Year ASC, Month ASC, Type ASC;";

        const string locationsSql = @"
SELECT Location, COUNT(*) AS Count
FROM Incidents
WHERE ClientId IN @ClientIds
GROUP BY Location
ORDER BY Count DESC
LIMIT 10;";

        const string byLocationAndTypeSql = @"
SELECT i.Location, i.Type, COUNT(*) AS Count
FROM Incidents i
INNER JOIN (
    SELECT Location, COUNT(*) AS LocationTotal
    FROM Incidents
    WHERE ClientId IN @ClientIds
    GROUP BY Location
    ORDER BY LocationTotal DESC
    LIMIT 8
) top ON i.Location = top.Location
WHERE i.ClientId IN @ClientIds
GROUP BY i.Location, i.Type
ORDER BY top.LocationTotal DESC, i.Type ASC;";

        var p = new { ClientIds = clientIds };

        async Task<T> One<T>(string sql) {
            await using var c = new MySqlConnection(_connectionString);
            await c.OpenAsync(ct);
            return await c.QuerySingleAsync<T>(new CommandDefinition(sql, p, cancellationToken: ct));
        }
        async Task<List<T>> Many<T>(string sql) {
            await using var c = new MySqlConnection(_connectionString);
            await c.OpenAsync(ct);
            return (await c.QueryAsync<T>(new CommandDefinition(sql, p, cancellationToken: ct))).ToList();
        }

        var summaryTask           = One<SummaryRow>         (summarySql);
        var byTypeTask            = Many<TypeCountDto>       (byTypeSql);
        var byMonthTask           = Many<MonthlyRowDto>      (byMonthSql);
        var topLocationsTask      = Many<LocationCountDto>   (locationsSql);
        var byLocationAndTypeTask = Many<LocationTypeCountDto>(byLocationAndTypeSql);

        await Task.WhenAll(summaryTask, byTypeTask, byMonthTask, topLocationsTask, byLocationAndTypeTask);

        var summary           = await summaryTask;
        var byType            = await byTypeTask;
        var byMonth           = await byMonthTask;
        var topLocations      = await topLocationsTask;
        var byLocationAndType = await byLocationAndTypeTask;

        return new IncidentAnalyticsDto(
            (int)summary.Total,
            (int)summary.Open,
            (int)summary.InProgress,
            (int)summary.Blocked,
            (int)summary.Closed,
            (int)summary.ThisMonth,
            (int)summary.LastMonth,
            byType,
            byMonth,
            topLocations,
            byLocationAndType);
    }
}
