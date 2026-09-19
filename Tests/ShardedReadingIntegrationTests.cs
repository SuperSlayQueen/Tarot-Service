using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using test_project.Data;
using test_project.Repositories;
using test_project.Sharding;
using Xunit;

namespace test_project.Tests;

public class ShardedReadingIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task EnabledSharding_UserReadMatchesExpectedPostgresShard()
    {
        // Requires real initialized PostgreSQL shards and a seeded user.
        // Environment overrides allow testing an existing installation without editing code.
        var settings = new Dictionary<string, string?>
        {
            ["Sharding:Enabled"] = "true",
            ["Sharding:Strategy"] = Environment.GetEnvironmentVariable("LAB_SHARD_STRATEGY") ?? "mod"
        };
        for (var id = 0; id < 3; id++)
            settings[$"Sharding:Connections:{id}"] =
                Environment.GetEnvironmentVariable($"LAB_SHARD_{id}") ??
                $"Host=localhost;Port={5441 + id};Database=tarot_shard;Username=tarot;Password=tarot";

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var options = configuration.GetSection("Sharding").Get<ShardOptions>()!;
        Assert.True(options.Enabled);
        var shards = new ShardConnections(options);
        var userId = int.Parse(Environment.GetEnvironmentVariable("LAB_USER_ID") ?? "1");
        var expectedShard = shards.Router.GetShard(userId);
        if (options.Strategy == "mod" && userId == 42) Assert.Equal(1, expectedShard);

        var expected = (await shards.RunAsync(expectedShard, c => c.QueryAsync<long>(
            "SELECT id FROM readings WHERE user_id=@userId ORDER BY created_at DESC,id DESC",
            new { userId }))).ToArray();
        Assert.True(expected.Length > 0,
            $"Seed real readings for user {userId} on shard {expectedShard} before running this test.");

        var primaryConnection = Environment.GetEnvironmentVariable("LAB_PRIMARY") ??
            "Host=localhost;Port=5437;Database=test_project_db;Username=test_project_db;Password=test_project_db";
        await using var primary = new TarotDbContext(
            new DbContextOptionsBuilder<TarotDbContext>().UseNpgsql(primaryConnection).Options);
        var repository = new ShardedReadingRepository(shards, primary);
        var actual = await repository.GetByUserIdAsync(userId);
        Assert.Equal(expected, actual.Select(r => r.Id).ToArray());
        Assert.All(actual, r => Assert.Equal(userId, r.UserId));

        foreach (var other in shards.Ids.Where(id => id != expectedShard))
        {
            var misplaced = await shards.RunAsync(other, c => c.ExecuteScalarAsync<long>(
                "SELECT count(*) FROM readings WHERE user_id=@userId", new { userId }));
            Assert.Equal(0L, misplaced);
        }
    }
}
