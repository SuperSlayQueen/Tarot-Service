using Npgsql;

namespace test_project.Sharding;

public sealed class ShardUnavailableException : Exception
{
    public ShardUnavailableException(int id, Exception inner)
        : base($"Shard {id} is unavailable; no partial result was returned.", inner) { }
}

public sealed class ShardConnections
{
    private readonly Dictionary<int, string> _connections;
    public IReadOnlyList<int> Ids { get; }
    public ShardRouter Router { get; }
    public string Fingerprint { get; }

    public async Task VerifyTopologyAsync()
    {
        foreach (var id in Ids)
            await RunAsync(id, async c =>
            {
                await using var command = new NpgsqlCommand("SELECT fingerprint FROM shard_topology WHERE singleton=true", c);
                var stored = await command.ExecuteScalarAsync();
                if (!Equals(stored, Fingerprint))
                    throw new InvalidOperationException("Shard topology differs from stored data. Migrate data before changing strategy, IDs or virtual nodes.");
                return 0;
            });
    }

    public ShardConnections(ShardOptions options)
    {
        _connections = options.Connections.ToDictionary(x => x.Key, x =>
        {
            var cs = new NpgsqlConnectionStringBuilder(x.Value)
            {
                Timeout = 5, CommandTimeout = 15
            };
            return cs.ConnectionString;
        });
        Ids = Array.AsReadOnly(_connections.Keys.OrderBy(x => x).ToArray());
        Router = new ShardRouter(Ids, options.Strategy, options.VirtualNodes);
        Fingerprint = $"md5-v1:{options.Strategy}:{options.VirtualNodes}:{string.Join(',', Ids)}";
    }

    public async Task<T> RunAsync<T>(int shardId, Func<NpgsqlConnection, Task<T>> action)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connections[shardId]);
            await connection.OpenAsync();
            return await action(connection);
        }
        catch (Exception ex) when (ex is TimeoutException || ex is NpgsqlException { IsTransient: true })
        {
            throw new ShardUnavailableException(shardId, ex);
        }
    }
}
