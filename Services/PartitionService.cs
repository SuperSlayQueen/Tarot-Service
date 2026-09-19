using Npgsql;
using test_project.Services.Interfaces;

namespace test_project.Services;

/// <summary>
/// Управление месячными RANGE-партициями таблицы readings.
/// </summary>
public class PartitionService : IPartitionService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<PartitionService> _logger;
    private const string ParentTable = "readings";
    private const int HorizonMonths = 3;

    public PartitionService(IConfiguration configuration, ILogger<PartitionService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<IReadOnlyList<string>> GetExistingPartitionsAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT c.relname
            FROM pg_inherits i
            JOIN pg_class c ON c.oid = i.inhrelid
            JOIN pg_class p ON p.oid = i.inhparent
            WHERE p.relname = 'readings'
            ORDER BY c.relname
            """;

        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        var list = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            list.Add(reader.GetString(0));
        return list;
    }

    public async Task<PartitionJobResult> EnsureFuturePartitionsAsync(CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;
        var startMonth = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var required = Enumerable.Range(0, HorizonMonths + 1)
            .Select(i => startMonth.AddMonths(i))
            .Select(PartitionNameFor)
            .ToList();

        var existing = await GetExistingPartitionsAsync(ct);
        var missing = required.Where(r => !existing.Contains(r, StringComparer.OrdinalIgnoreCase)).ToList();
        var created = new List<string>();

        _logger.LogInformation(
            "Partition job started. Existing: {Existing}, Required: {Required}, Missing: {Missing}",
            existing.Count, required.Count, missing.Count);

        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        foreach (var name in missing)
        {
            var month = ParseMonthFromName(name);
            var from = month.ToString("yyyy-MM-dd");
            var to = month.AddMonths(1).ToString("yyyy-MM-dd");
            var sql = $"""
                CREATE TABLE IF NOT EXISTS {name}
                PARTITION OF {ParentTable}
                FOR VALUES FROM ('{from}') TO ('{to}');
                """;
            await using var cmd = new NpgsqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync(ct);
            created.Add(name);
            _logger.LogInformation("Partition created: {Partition}", name);
        }

        _logger.LogInformation("Partition job finished. Created: {Count}", created.Count);
        return new PartitionJobResult(existing.Count, required.Count, created, missing);
    }

    public async Task<PartitionHealthResult> CheckHealthAsync(CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;
        var startMonth = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var expected = Enumerable.Range(0, HorizonMonths + 1)
            .Select(i => startMonth.AddMonths(i))
            .Select(PartitionNameFor)
            .ToList();

        var existing = await GetExistingPartitionsAsync(ct);
        var missing = expected.Where(e => !existing.Contains(e, StringComparer.OrdinalIgnoreCase)).ToList();

        return new PartitionHealthResult(missing.Count == 0, expected, missing, DateTime.UtcNow);
    }

    private NpgsqlConnection CreateConnection()
    {
        var cs = _configuration.GetConnectionString("DefaultConnection")
                 ?? throw new InvalidOperationException("DefaultConnection is missing");
        return new NpgsqlConnection(cs);
    }

    private static string PartitionNameFor(DateTime month) =>
        $"readings_{month:yyyy_MM}";

    private static DateTime ParseMonthFromName(string name)
    {
        // readings_2026_09
        var parts = name.Split('_');
        var year = int.Parse(parts[1]);
        var month = int.Parse(parts[2]);
        return new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
    }
}
