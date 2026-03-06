using System.Text;
using Dapper;
using ImperaOps.Application.Abstractions;
using ImperaOps.Application.Events.Dtos;
using MySqlConnector;

namespace ImperaOps.Infrastructure.Repositories;

public sealed class EventReadRepository : IEventReadRepository
{
    private readonly string _connectionString;

    public EventReadRepository(string connectionString) => _connectionString = connectionString;

    // ── List ──────────────────────────────────────────────────────────────────

    public async Task<PagedResult<EventListItemDto>> GetListAsync(
        long clientId, int page, int pageSize,
        long? eventTypeId, long? workflowStatusId,
        DateTime? dateFrom, DateTime? dateTo,
        string? search, CancellationToken ct)
    {
        page     = page     <= 0 ? 1  : page;
        pageSize = pageSize <= 0 ? 25 : Math.Min(pageSize, 200);
        var offset = (page - 1) * pageSize;

        var where = new StringBuilder("WHERE e.ClientId = @ClientId");
        if (eventTypeId.HasValue)       where.Append(" AND e.EventTypeId = @EventTypeId");
        if (workflowStatusId.HasValue)  where.Append(" AND e.WorkflowStatusId = @WorkflowStatusId");
        if (dateFrom.HasValue)          where.Append(" AND DATE(e.OccurredAt) >= DATE(@DateFrom)");
        if (dateTo.HasValue)            where.Append(" AND DATE(e.OccurredAt) <= DATE(@DateTo)");
        if (!string.IsNullOrWhiteSpace(search))
            where.Append(@" AND (
                e.Title LIKE CONCAT('%', @Search, '%')
                OR e.Location LIKE CONCAT('%', @Search, '%')
                OR e.Description LIKE CONCAT('%', @Search, '%')
                OR et.Name LIKE CONCAT('%', @Search, '%')
                OR ws.Name LIKE CONCAT('%', @Search, '%')
            )");

        var countSql = $@"
SELECT COUNT(1)
FROM Events e
LEFT JOIN EventTypes et ON et.Id = e.EventTypeId
LEFT JOIN WorkflowStatuses ws ON ws.Id = e.WorkflowStatusId
{where};";

        var pageSql = $@"
SELECT e.Id, e.ClientId, e.PublicId, e.EventTypeId, et.Name AS EventTypeName,
       e.WorkflowStatusId, ws.Name AS WorkflowStatusName, ws.Color AS WorkflowStatusColor, ws.IsClosed AS WorkflowStatusIsClosed,
       e.Title, e.OccurredAt, e.Location, e.OwnerUserId, e.ReferenceNumber,
       u.DisplayName AS OwnerDisplayName
FROM Events e
LEFT JOIN EventTypes et ON et.Id = e.EventTypeId
LEFT JOIN WorkflowStatuses ws ON ws.Id = e.WorkflowStatusId
LEFT JOIN Users u ON u.Id = e.OwnerUserId
{where}
ORDER BY e.OccurredAt DESC
LIMIT @PageSize OFFSET @Offset;";

        var param = new DynamicParameters();
        param.Add("ClientId", clientId);
        if (eventTypeId.HasValue)       param.Add("EventTypeId",      eventTypeId.Value);
        if (workflowStatusId.HasValue)  param.Add("WorkflowStatusId", workflowStatusId.Value);
        if (dateFrom.HasValue)          param.Add("DateFrom",         dateFrom.Value.Date);
        if (dateTo.HasValue)            param.Add("DateTo",           dateTo.Value.Date);
        if (!string.IsNullOrWhiteSpace(search)) param.Add("Search", search.Trim());
        param.Add("PageSize", pageSize);
        param.Add("Offset",   offset);

        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var total = await conn.ExecuteScalarAsync<int>(new CommandDefinition(countSql, param, cancellationToken: ct));
        var items = (await conn.QueryAsync<EventListItemDto>(new CommandDefinition(pageSql, param, cancellationToken: ct))).ToList();

        return new PagedResult<EventListItemDto>(items, total, page, pageSize);
    }

    // ── Detail by PublicId ────────────────────────────────────────────────────

    public async Task<EventDetailDto?> GetByPublicIdAsync(string publicId, CancellationToken ct)
    {
        const string sql = @"
SELECT e.Id, e.ClientId, e.PublicId, e.EventTypeId, et.Name AS EventTypeName,
       e.WorkflowStatusId, ws.Name AS WorkflowStatusName, ws.Color AS WorkflowStatusColor, ws.IsClosed AS WorkflowStatusIsClosed,
       e.Title, e.OccurredAt, e.Location, e.Description, e.ReportedByUserId,
       rep.DisplayName AS ReportedByDisplayName,
       e.ExternalReporterName, e.ExternalReporterContact,
       e.OwnerUserId, own.DisplayName AS OwnerDisplayName,
       e.ReferenceNumber,
       e.RootCauseId, rc.Name AS RootCauseName,
       e.CorrectiveAction,
       e.CreatedAt, e.UpdatedAt
FROM Events e
LEFT JOIN EventTypes et ON et.Id = e.EventTypeId
LEFT JOIN WorkflowStatuses ws ON ws.Id = e.WorkflowStatusId
LEFT JOIN Users rep ON rep.Id = e.ReportedByUserId
LEFT JOIN Users own ON own.Id = e.OwnerUserId
LEFT JOIN RootCauseTaxonomyItems rc ON rc.Id = e.RootCauseId
WHERE e.PublicId = @PublicId
LIMIT 1;";

        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        return await conn.QueryFirstOrDefaultAsync<EventDetailDto>(
            new CommandDefinition(sql, new { PublicId = publicId }, cancellationToken: ct));
    }

    // ── Export ────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<EventExportRowDto>> GetExportDataAsync(
        long clientId, long? eventTypeId, long? workflowStatusId,
        DateTime? dateFrom, DateTime? dateTo, string? search, CancellationToken ct)
    {
        var where = new StringBuilder("WHERE e.ClientId = @ClientId");
        if (eventTypeId.HasValue)       where.Append(" AND e.EventTypeId = @EventTypeId");
        if (workflowStatusId.HasValue)  where.Append(" AND e.WorkflowStatusId = @WorkflowStatusId");
        if (dateFrom.HasValue)          where.Append(" AND DATE(e.OccurredAt) >= DATE(@DateFrom)");
        if (dateTo.HasValue)            where.Append(" AND DATE(e.OccurredAt) <= DATE(@DateTo)");
        if (!string.IsNullOrWhiteSpace(search))
            where.Append(@" AND (
                e.Title LIKE CONCAT('%', @Search, '%')
                OR e.Location LIKE CONCAT('%', @Search, '%')
                OR e.Description LIKE CONCAT('%', @Search, '%')
                OR et.Name LIKE CONCAT('%', @Search, '%')
                OR ws.Name LIKE CONCAT('%', @Search, '%')
            )");

        var sql = $@"
SELECT e.PublicId, e.OccurredAt, et.Name AS EventTypeName, ws.Name AS WorkflowStatusName,
       e.Location, e.Description, own.DisplayName AS Owner
FROM Events e
LEFT JOIN EventTypes et ON et.Id = e.EventTypeId
LEFT JOIN WorkflowStatuses ws ON ws.Id = e.WorkflowStatusId
LEFT JOIN Users own ON own.Id = e.OwnerUserId
{where}
ORDER BY e.OccurredAt DESC;";

        var param = new DynamicParameters();
        param.Add("ClientId", clientId);
        if (eventTypeId.HasValue)       param.Add("EventTypeId",      eventTypeId.Value);
        if (workflowStatusId.HasValue)  param.Add("WorkflowStatusId", workflowStatusId.Value);
        if (dateFrom.HasValue)          param.Add("DateFrom",         dateFrom.Value.Date);
        if (dateTo.HasValue)            param.Add("DateTo",           dateTo.Value.Date);
        if (!string.IsNullOrWhiteSpace(search)) param.Add("Search", search.Trim());

        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        return (await conn.QueryAsync<EventExportRowDto>(new CommandDefinition(sql, param, cancellationToken: ct))).ToList();
    }

    // ── Analytics ─────────────────────────────────────────────────────────────

    private sealed record SummaryRow(long Total, decimal Open, decimal InProgress, decimal Blocked, decimal Closed, decimal ThisMonth, decimal LastMonth);

    public async Task<EventAnalyticsDto> GetAnalyticsAsync(IReadOnlyList<long> clientIds, DateTime? dateFrom, DateTime? dateTo, CancellationToken ct)
    {
        // Build the common date filter fragment (appended after the ClientId IN clause)
        var dateFilter = new StringBuilder();
        if (dateFrom.HasValue) dateFilter.Append(" AND e.OccurredAt >= @DateFrom");
        if (dateTo.HasValue)   dateFilter.Append(" AND e.OccurredAt <  @DateTo");

        // For the inner subquery in byLocationAndType we need a version without the alias
        var dateFilterNoAlias = new StringBuilder();
        if (dateFrom.HasValue) dateFilterNoAlias.Append(" AND OccurredAt >= @DateFrom");
        if (dateTo.HasValue)   dateFilterNoAlias.Append(" AND OccurredAt <  @DateTo");

        var summarySql = $@"
SELECT
    COUNT(*)                                                                           AS Total,
    SUM(ws.IsClosed = 0 AND ws.Name = 'Open')                                         AS Open,
    SUM(ws.IsClosed = 0 AND ws.Name = 'In Progress')                                  AS InProgress,
    SUM(ws.IsClosed = 0 AND ws.Name = 'Blocked')                                      AS Blocked,
    SUM(ws.IsClosed = 1)                                                              AS Closed,
    SUM(YEAR(e.OccurredAt) = YEAR(CURDATE()) AND MONTH(e.OccurredAt) = MONTH(CURDATE())) AS ThisMonth,
    SUM(YEAR(e.OccurredAt) = YEAR(DATE_SUB(CURDATE(), INTERVAL 1 MONTH))
        AND MONTH(e.OccurredAt) = MONTH(DATE_SUB(CURDATE(), INTERVAL 1 MONTH)))      AS LastMonth
FROM Events e
LEFT JOIN WorkflowStatuses ws ON ws.Id = e.WorkflowStatusId
WHERE e.ClientId IN @ClientIds{dateFilter};";

        var byTypeSql = $@"
SELECT e.EventTypeId, et.Name AS EventTypeName, COUNT(*) AS Count
FROM Events e
LEFT JOIN EventTypes et ON et.Id = e.EventTypeId
WHERE e.ClientId IN @ClientIds{dateFilter}
GROUP BY e.EventTypeId, et.Name
ORDER BY Count DESC;";

        var byMonthSql = $@"
SELECT YEAR(e.OccurredAt) AS Year, MONTH(e.OccurredAt) AS Month,
       e.EventTypeId, et.Name AS EventTypeName, COUNT(*) AS Count
FROM Events e
LEFT JOIN EventTypes et ON et.Id = e.EventTypeId
WHERE e.ClientId IN @ClientIds{dateFilter}
GROUP BY YEAR(e.OccurredAt), MONTH(e.OccurredAt), e.EventTypeId, et.Name
ORDER BY Year ASC, Month ASC, e.EventTypeId ASC;";

        var locationsSql = $@"
SELECT e.Location, COUNT(*) AS Count
FROM Events e
WHERE e.ClientId IN @ClientIds{dateFilter}
GROUP BY e.Location
ORDER BY Count DESC
LIMIT 10;";

        var byLocationAndTypeSql = $@"
SELECT e.Location, e.EventTypeId, et.Name AS EventTypeName, COUNT(*) AS Count
FROM Events e
LEFT JOIN EventTypes et ON et.Id = e.EventTypeId
INNER JOIN (
    SELECT Location, COUNT(*) AS LocationTotal
    FROM Events
    WHERE ClientId IN @ClientIds{dateFilterNoAlias}
    GROUP BY Location
    ORDER BY LocationTotal DESC
    LIMIT 8
) top ON e.Location = top.Location
WHERE e.ClientId IN @ClientIds{dateFilter}
GROUP BY e.Location, e.EventTypeId, et.Name
ORDER BY top.LocationTotal DESC, e.EventTypeId ASC;";

        var p = new DynamicParameters();
        p.Add("ClientIds", clientIds);
        if (dateFrom.HasValue) p.Add("DateFrom", dateFrom.Value.Date);
        if (dateTo.HasValue)   p.Add("DateTo",   dateTo.Value.Date.AddDays(1)); // exclusive upper bound

        async Task<T> One<T>(string sql)
        {
            await using var c = new MySqlConnection(_connectionString);
            await c.OpenAsync(ct);
            return await c.QuerySingleAsync<T>(new CommandDefinition(sql, p, cancellationToken: ct));
        }
        async Task<List<T>> Many<T>(string sql)
        {
            await using var c = new MySqlConnection(_connectionString);
            await c.OpenAsync(ct);
            return (await c.QueryAsync<T>(new CommandDefinition(sql, p, cancellationToken: ct))).ToList();
        }

        var byRootCauseSql = $@"
SELECT COALESCE(rc.Name, 'Unknown') AS Name, COUNT(*) AS Count
FROM Events e
LEFT JOIN RootCauseTaxonomyItems rc ON rc.Id = e.RootCauseId
WHERE e.ClientId IN @ClientIds{dateFilter}
  AND e.RootCauseId IS NOT NULL
GROUP BY e.RootCauseId, rc.Name
ORDER BY Count DESC;";

        var avgResolutionSql = $@"
SELECT AVG(TIMESTAMPDIFF(SECOND, e.CreatedAt, e.UpdatedAt) / 86400.0)
FROM Events e
LEFT JOIN WorkflowStatuses ws ON ws.Id = e.WorkflowStatusId
WHERE e.ClientId IN @ClientIds{dateFilter}
  AND ws.IsClosed = 1;";

        var slaComplianceSql = $@"
SELECT
    COUNT(*) AS TotalClosed,
    SUM(CASE
        WHEN sr.ClosureHours IS NOT NULL
             AND TIMESTAMPDIFF(HOUR, e.CreatedAt, e.UpdatedAt) <= sr.ClosureHours
        THEN 1 ELSE 0
    END) AS WithinSla
FROM Events e
LEFT JOIN WorkflowStatuses ws ON ws.Id = e.WorkflowStatusId
LEFT JOIN SlaRules sr ON sr.ClientId = e.ClientId
    AND (sr.EventTypeId = e.EventTypeId OR sr.EventTypeId IS NULL)
    AND sr.DeletedAt IS NULL
WHERE e.ClientId IN @ClientIds{dateFilter}
  AND ws.IsClosed = 1;";

        var summaryTask           = One<SummaryRow>                 (summarySql);
        var byTypeTask            = Many<EventTypeCountDto>          (byTypeSql);
        var byMonthTask           = Many<EventMonthlyRowDto>         (byMonthSql);
        var topLocationsTask      = Many<EventLocationCountDto>      (locationsSql);
        var byLocationAndTypeTask = Many<EventLocationTypeCountDto>  (byLocationAndTypeSql);

        async Task<List<EventRootCauseCountDto>> ManyRootCause()
        {
            await using var c = new MySqlConnection(_connectionString);
            await c.OpenAsync(ct);
            return (await c.QueryAsync<EventRootCauseCountDto>(new CommandDefinition(byRootCauseSql, p, cancellationToken: ct))).ToList();
        }

        async Task<double?> OneNullableDouble(string sql)
        {
            await using var c = new MySqlConnection(_connectionString);
            await c.OpenAsync(ct);
            var val = await c.ExecuteScalarAsync<object>(new CommandDefinition(sql, p, cancellationToken: ct));
            if (val == null || val == DBNull.Value) return null;
            return Convert.ToDouble(val);
        }

        async Task<(long TotalClosed, long WithinSla)> OneSlaRow()
        {
            await using var c = new MySqlConnection(_connectionString);
            await c.OpenAsync(ct);
            var row = await c.QueryFirstOrDefaultAsync<(long TotalClosed, long WithinSla)>(
                new CommandDefinition(slaComplianceSql, p, cancellationToken: ct));
            return row;
        }

        var byRootCauseTask  = ManyRootCause();
        var avgResTask       = OneNullableDouble(avgResolutionSql);
        var slaTask          = OneSlaRow();

        await Task.WhenAll(summaryTask, byTypeTask, byMonthTask, topLocationsTask, byLocationAndTypeTask, byRootCauseTask, avgResTask, slaTask);

        var summary           = await summaryTask;
        var byType            = await byTypeTask;
        var byMonth           = await byMonthTask;
        var topLocations      = await topLocationsTask;
        var byLocationAndType = await byLocationAndTypeTask;
        var byRootCause       = await byRootCauseTask;
        var avgResolution     = await avgResTask;
        var slaRow            = await slaTask;

        double? slaRate = null;
        if (slaRow.TotalClosed > 0)
            slaRate = Math.Round((double)slaRow.WithinSla / slaRow.TotalClosed * 100, 1);

        return new EventAnalyticsDto(
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
            byLocationAndType,
            byRootCause,
            avgResolution,
            slaRate);
    }
}
