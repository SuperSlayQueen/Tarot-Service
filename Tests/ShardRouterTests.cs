using test_project.Sharding;
using Xunit;

namespace test_project.Tests;

public class ShardRouterTests
{
    [Theory]
    [InlineData(1, 1, 2)]
    [InlineData(2, 2, 1)]
    public void KnownUsers_MapToExpectedShard(int user, int modShard, int ringShard)
    {
        Assert.Equal(modShard, new ShardRouter(new[] { 0, 1, 2 }).GetShard(user));
        Assert.Equal(ringShard, new ShardRouter(new[] { 0, 1, 2 }, "consistent").GetShard(user));
    }

    [Fact]
    public void AddedNode_OnlyTakesKeysForNewNode()
    {
        var before = new ShardRouter(new[] { 0, 1, 2 }, "consistent");
        var after = new ShardRouter(new[] { 0, 1, 2, 3 }, "consistent");
        var reordered = new ShardRouter(new[] { 2, 0, 1 }, "consistent");
        foreach (var key in Enumerable.Range(1, 5000))
        {
            Assert.Equal(before.GetShard(key), reordered.GetShard(key));
            if (before.GetShard(key) != after.GetShard(key)) Assert.Equal(3, after.GetShard(key));
        }
    }

    [Fact]
    public void InvalidConfiguration_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => new ShardRouter(Array.Empty<int>()));
        Assert.Throws<ArgumentException>(() => new ShardRouter(new[] { 0, 0 }));
        Assert.Throws<ArgumentException>(() => new ShardRouter(new[] { 0 }, "random"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ShardRouter(new[] { 0 }, "consistent", 0));
    }
}
