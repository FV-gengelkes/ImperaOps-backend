using Dapper;
using ImperaOps.Application.Abstractions;
using MySqlConnector;

namespace ImperaOps.Infrastructure.Services;

public sealed class CounterService : ICounterService
{
    private readonly string _connectionString;

    public CounterService(string connectionString) => _connectionString = connectionString;

    public async Task<long> AllocateAsync(long clientId, string counterName, CancellationToken ct)
    {
        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        // LAST_INSERT_ID(1) in the INSERT sets the session LAST_INSERT_ID even for new rows
        // (no auto-increment column), so SELECT LAST_INSERT_ID() returns the correct value.
        await conn.ExecuteAsync(
            "INSERT INTO ClientCounters (ClientId, CounterName, Value) VALUES (@ClientId, @Name, LAST_INSERT_ID(1)) " +
            "ON DUPLICATE KEY UPDATE Value = LAST_INSERT_ID(Value + 1)",
            new { ClientId = clientId, Name = counterName });

        return await conn.ExecuteScalarAsync<long>("SELECT LAST_INSERT_ID()");
    }
}
