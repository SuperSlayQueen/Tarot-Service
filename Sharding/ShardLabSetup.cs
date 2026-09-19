using Dapper;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using test_project.Data;

namespace test_project.Sharding;

// Explicit lab-only command. Never invoked during normal application startup.
public static class ShardLabSetup
{
    public static async Task RunAsync(IConfiguration configuration)
    {
        var options = configuration.GetSection("Sharding").Get<ShardOptions>()!;
        if (!options.Enabled) throw new InvalidOperationException("Enable Sharding for lab setup.");
        var shards = new ShardConnections(options);
        await using var primary = new NpgsqlConnection(configuration.GetConnectionString("DefaultConnection"));
        await primary.OpenAsync();
        var users = (await primary.QueryAsync<int>("SELECT id FROM users ORDER BY id")).ToArray();
        var spreads = (await primary.QueryAsync<int>("SELECT id FROM spreads ORDER BY id")).ToArray();
        var card = await primary.ExecuteScalarAsync<int>("SELECT id FROM cards ORDER BY id LIMIT 1");
        if (users.Length == 0 || spreads.Length == 0 || card == 0)
            throw new InvalidOperationException("Apply the existing service migrations and seed first.");
        // No existing data is deleted; setup refuses nonempty destination tables.
        foreach (var id in shards.Ids)
            await shards.RunAsync(id, async c =>
            {
                await c.ExecuteAsync("""
                    CREATE TABLE IF NOT EXISTS readings (
                        id bigint PRIMARY KEY, user_id integer NOT NULL, spread_id integer NOT NULL,
                        question varchar(1000), status varchar(20) NOT NULL,
                        created_at timestamptz NOT NULL, updated_at timestamptz);
                    CREATE TABLE IF NOT EXISTS reading_cards (
                        id bigserial PRIMARY KEY, reading_id bigint NOT NULL REFERENCES readings(id) ON DELETE CASCADE,
                        card_id integer NOT NULL, position integer NOT NULL, is_reversed boolean NOT NULL,
                        created_at timestamptz NOT NULL, UNIQUE(reading_id,position));
                    CREATE INDEX IF NOT EXISTS idx_readings_user_time ON readings(user_id,created_at DESC,id DESC);
                    CREATE INDEX IF NOT EXISTS idx_readings_time ON readings(created_at DESC,id DESC);
                    CREATE TABLE IF NOT EXISTS shard_topology (singleton boolean PRIMARY KEY DEFAULT true CHECK(singleton), fingerprint text NOT NULL);
                    """);
                if (await c.ExecuteScalarAsync<long>("SELECT count(*) FROM readings") != 0)
                    throw new InvalidOperationException("Lab destination must be empty. Use new volumes rather than deleting existing data.");
                await c.ExecuteAsync("INSERT INTO shard_topology(singleton,fingerprint) VALUES(true,@fingerprint) ON CONFLICT(singleton) DO UPDATE SET fingerprint=excluded.fingerprint",
                    new { fingerprint = shards.Fingerprint });
                return 0;
            });
        await primary.ExecuteAsync("CREATE SEQUENCE IF NOT EXISTS sharding_reading_ids START 1000000000");
        // Generate real service readings for EXISTING service users/spreads/cards.
        // With the default seed there are only two users: imbalance is expected, not concealed.
        var start = await primary.ExecuteScalarAsync<long>("SELECT nextval('sharding_reading_ids')");
        await primary.ExecuteAsync("SELECT setval('sharding_reading_ids',@last,true)", new { last = start + 99999 });
        var now = DateTime.UtcNow;
        foreach (var shard in shards.Ids)
        {
            var rows = Enumerable.Range(0, 100000).Where(i => shards.Router.GetShard(users[i % users.Length]) == shard)
                .Select(i => new { Id = start + i, UserId = users[i % users.Length], SpreadId = spreads[i % spreads.Length],
                    Question = $"Lab 5 service reading #{i}", Status = new[] { "NEW", "COMPLETED", "CANCELLED" }[i % 3],
                    CreatedAt = now.AddSeconds(-i) }).ToArray();
            await shards.RunAsync(shard, async c =>
            {
                await using var tx = await c.BeginTransactionAsync();
                await c.ExecuteAsync("INSERT INTO readings(id,user_id,spread_id,question,status,created_at) VALUES(@Id,@UserId,@SpreadId,@Question,@Status,@CreatedAt)", rows, tx, commandTimeout: 120);
                await c.ExecuteAsync("INSERT INTO reading_cards(reading_id,card_id,position,is_reversed,created_at) SELECT id,@card,1,false,created_at FROM readings", new { card }, tx, commandTimeout: 120);
                await tx.CommitAsync();
                return 0;
            });
            Console.WriteLine($"Shard {shard}: {rows.Length} readings");
        }
        var mod3 = new ShardRouter(new[] { 0, 1, 2 });
        var mod4 = new ShardRouter(new[] { 0, 1, 2, 3 });
        var ring3 = new ShardRouter(new[] { 0, 1, 2 }, "consistent", options.VirtualNodes);
        var ring4 = new ShardRouter(new[] { 0, 1, 2, 3 }, "consistent", options.VirtualNodes);
        var keys = Enumerable.Range(0,100000).Select(i => users[i % users.Length]).ToArray();
        Console.WriteLine($"Existing service users: {users.Length}; rows: {keys.Length}");
        Console.WriteLine($"3->4 mod moved: {keys.Count(k => mod3.GetShard(k) != mod4.GetShard(k))}");
        Console.WriteLine($"3->4 consistent moved: {keys.Count(k => ring3.GetShard(k) != ring4.GetShard(k))}");
    }
}
