using Dapper;
using Microsoft.EntityFrameworkCore;
using test_project.Data;
using test_project.Models.DTO;
using test_project.Models.Entities;
using test_project.Repositories.Interfaces;
using test_project.Sharding;

namespace test_project.Repositories;

public sealed class ShardedReadingRepository : IReadingRepository
{
    private readonly ShardConnections _shards;
    private readonly TarotDbContext _primary;
    private const string Columns = "id, user_id AS UserId, spread_id AS SpreadId, question, status, created_at AS CreatedAt, updated_at AS UpdatedAt";
    private const string CardColumns = "id, reading_id AS ReadingId, card_id AS CardId, position, is_reversed AS IsReversed, created_at AS CreatedAt";

    public ShardedReadingRepository(ShardConnections shards, TarotDbContext primary)
    {
        _shards = shards;
        _primary = primary;
    }

    private async Task HydrateAsync(List<Reading> rows)
    {
        if (rows.Count == 0) return;
        var userIds = rows.Select(r => r.UserId).Distinct().ToArray();
        var spreadIds = rows.Select(r => r.SpreadId).Distinct().ToArray();
        // One EF context must not execute concurrent queries.
        var users = await _primary.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id);
        var spreads = await _primary.Spreads.AsNoTracking().Where(s => spreadIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id);
        var cardIds = rows.SelectMany(r => r.ReadingCards).Select(c => c.CardId).Distinct().ToArray();
        var cards = await _primary.Cards.AsNoTracking().Where(c => cardIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id);
        foreach (var row in rows)
        {
            row.User = users.GetValueOrDefault(row.UserId);
            row.Spread = spreads.GetValueOrDefault(row.SpreadId);
            foreach (var card in row.ReadingCards) card.Card = cards.GetValueOrDefault(card.CardId);
        }
    }

    private async Task<List<Reading>> ReadAsync(int shard, string where, object args, string suffix = "")
    {
        return await _shards.RunAsync(shard, async c =>
        {
            var rows = (await c.QueryAsync<Reading>($"SELECT {Columns} FROM readings {where} {suffix}", args)).ToList();
            if (rows.Count == 0) return rows;
            var ids = rows.Select(r => r.Id).ToArray();
            var cards = (await c.QueryAsync<ReadingCard>($"SELECT {CardColumns} FROM reading_cards WHERE reading_id = ANY(@ids)", new { ids })).ToLookup(rc => rc.ReadingId);
            foreach (var row in rows) row.ReadingCards = cards[row.Id].ToList();
            return rows;
        });
    }

    public async Task<Reading?> GetByIdAsync(long id)
    {
        var parts = await Task.WhenAll(_shards.Ids.Select(s => ReadAsync(s, "WHERE id=@id", new { id })));
        var rows = parts.SelectMany(x => x).ToList();
        if (rows.Count > 1) throw new InvalidOperationException("Duplicate global reading ID across shards.");
        await HydrateAsync(rows);
        return rows.SingleOrDefault();
    }

    public async Task<List<Reading>> GetByUserIdAsync(int userId)
    {
        var rows = await ReadAsync(_shards.Router.GetShard(userId), "WHERE user_id=@userId", new { userId }, "ORDER BY created_at DESC, id DESC");
        await HydrateAsync(rows);
        return rows;
    }

    public async Task<PagedResponseDto<Reading>> GetPagedAsync(ReadingQueryDto query)
    {
        if (query.Page < 1 || query.PageSize < 1 || query.PageSize > 1000)
            throw new ArgumentException("Page must be positive and PageSize must be 1..1000.");
        var take = checked(query.Page * query.PageSize);
        if (take > 100000) throw new ArgumentException("Deep offset pagination is limited to 100000 candidates per shard.");
        var where = "WHERE 1=1";
        if (query.UserId.HasValue) where += " AND user_id=@UserId";
        if (!string.IsNullOrWhiteSpace(query.Status)) where += " AND status=@Status";
        if (query.From.HasValue) where += " AND created_at>=@From";
        if (query.To.HasValue) where += " AND created_at<@To";
        var sort = query.Sort?.ToLowerInvariant();
        var order = sort switch
        {
            "created_at" => "created_at ASC, id ASC",
            "status" => "status COLLATE \"C\" ASC, id ASC",
            "-status" => "status COLLATE \"C\" DESC, id DESC",
            _ => "created_at DESC, id DESC"
        };
        var ids = query.UserId.HasValue ? new[] { _shards.Router.GetShard(query.UserId.Value) } : _shards.Ids.ToArray();
        var args = new DynamicParameters(query);
        args.Add("take", take);
        var parts = await Task.WhenAll(ids.Select(async id =>
        {
            var total = await _shards.RunAsync(id, c => c.ExecuteScalarAsync<long>($"SELECT count(*) FROM readings {where}", query));
            var rows = await ReadAsync(id, where, args, $"ORDER BY {order} LIMIT @take");
            return (total, rows);
        }));
        var all = parts.SelectMany(p => p.rows);
        var sorted = sort switch
        {
            "created_at" => all.OrderBy(r => r.CreatedAt).ThenBy(r => r.Id),
            "status" => all.OrderBy(r => r.Status, StringComparer.Ordinal).ThenBy(r => r.Id),
            "-status" => all.OrderByDescending(r => r.Status, StringComparer.Ordinal).ThenByDescending(r => r.Id),
            _ => all.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
        };
        var items = sorted.Skip(take - query.PageSize).Take(query.PageSize).ToList();
        await HydrateAsync(items);
        return new PagedResponseDto<Reading> { Items = items, Total = checked((int)parts.Sum(p => p.total)), Page = query.Page, PageSize = query.PageSize };
    }

    public async Task<Reading> CreateAsync(Reading reading)
    {
        // Central sequence allocates global IDs; gaps after failed writes are harmless.
        var cs = _primary.Database.GetConnectionString()!;
        await using var primary = new Npgsql.NpgsqlConnection(cs);
        await primary.OpenAsync();
        reading.Id = await primary.ExecuteScalarAsync<long>("SELECT nextval('sharding_reading_ids')");
        reading.CreatedAt = DateTime.UtcNow;
        return await _shards.RunAsync(_shards.Router.GetShard(reading.UserId), async c =>
        {
            await using var tx = await c.BeginTransactionAsync();
            await c.ExecuteAsync("INSERT INTO readings(id,user_id,spread_id,question,status,created_at) VALUES (@Id,@UserId,@SpreadId,@Question,@Status,@CreatedAt)", reading, tx);
            foreach (var card in reading.ReadingCards)
            {
                card.ReadingId = reading.Id;
                card.CreatedAt = reading.CreatedAt;
                card.Id = await c.ExecuteScalarAsync<long>("INSERT INTO reading_cards(reading_id,card_id,position,is_reversed,created_at) VALUES (@ReadingId,@CardId,@Position,@IsReversed,@CreatedAt) RETURNING id", card, tx);
            }
            await tx.CommitAsync();
            return reading;
        });
    }

    public async Task<Reading> UpdateAsync(Reading reading)
    {
        reading.UpdatedAt = DateTime.UtcNow;
        var changed = await _shards.RunAsync(_shards.Router.GetShard(reading.UserId), c => c.ExecuteAsync(
            "UPDATE readings SET question=@Question,status=@Status,updated_at=@UpdatedAt WHERE id=@Id AND user_id=@UserId", reading));
        if (changed != 1) throw new KeyNotFoundException("Reading not found on its configured shard.");
        return reading;
    }

    public async Task DeleteAsync(long id)
    {
        var reading = await GetByIdAsync(id) ?? throw new KeyNotFoundException("Reading not found.");
        await _shards.RunAsync(_shards.Router.GetShard(reading.UserId), c => c.ExecuteAsync(
            "DELETE FROM readings WHERE id=@id AND user_id=@userId", new { id, userId = reading.UserId }));
    }

    public async Task<bool> ExistsAsync(long id) => await GetByIdAsync(id) != null;

}
