using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace test_project.Sharding;

public sealed class ShardOptions
{
    public bool Enabled { get; set; }
    public string Strategy { get; set; } = "mod";
    public int VirtualNodes { get; set; } = 100;
    public Dictionary<int, string> Connections { get; set; } = new();
}

public sealed class ShardRouter
{
    private readonly int[] _ids;
    private readonly (long Point, int Id)[] _ring;
    public string Strategy { get; }

    public ShardRouter(IEnumerable<int> ids, string strategy = "mod", int virtualNodes = 100)
    {
        _ids = ids.OrderBy(x => x).ToArray();
        if (_ids.Length == 0 || _ids.Distinct().Count() != _ids.Length || _ids.Any(x => x < 0))
            throw new ArgumentException("Shard IDs must be nonempty, unique and nonnegative.");
        if (strategy != "mod" && strategy != "consistent")
            throw new ArgumentException("Strategy must be mod or consistent.");
        if (virtualNodes < 1 || virtualNodes > 10000)
            throw new ArgumentOutOfRangeException(nameof(virtualNodes));
        Strategy = strategy;
        _ring = _ids.SelectMany(id => Enumerable.Range(1, virtualNodes)
            .Select(v => (Point: Hash64(FormattableString.Invariant($"shard-{id}-vnode-{v}")), Id: id)))
            .OrderBy(x => x.Point).ThenBy(x => x.Id).ToArray();
    }

    public static uint Hash32(string key) => BinaryPrimitives.ReadUInt32BigEndian(
        MD5.HashData(Encoding.UTF8.GetBytes(key)));

    public static long Hash64(string key) => BinaryPrimitives.ReadInt64BigEndian(
        MD5.HashData(Encoding.UTF8.GetBytes(key)));

    public int GetShard(long userId)
    {
        var key = userId.ToString(CultureInfo.InvariantCulture);
        if (Strategy == "mod") return _ids[(int)(Hash32(key) % (uint)_ids.Length)];
        var point = Hash64(key);
        var low = 0;
        var high = _ring.Length;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (_ring[middle].Point < point) low = middle + 1;
            else high = middle;
        }
        return _ring[low == _ring.Length ? 0 : low].Id;
    }
}
