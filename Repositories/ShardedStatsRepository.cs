using Dapper;
using Microsoft.EntityFrameworkCore;
using test_project.Data;
using test_project.Models.DTO;
using test_project.Repositories.Interfaces;
using test_project.Sharding;

namespace test_project.Repositories;

public sealed class ShardedStatsRepository : IStatsRepository
{
    private readonly ShardConnections _shards;
    private readonly TarotDbContext _primary;
    public ShardedStatsRepository(ShardConnections shards, TarotDbContext primary)
    {
        _shards = shards;
        _primary = primary;
    }

    public Task<List<CardsBySuitDto>> GetCardsBySuitAsync() => new StatsRepository(_primary).GetCardsBySuitAsync();

    public async Task<List<ReadingsByStatusDto>> GetReadingsByStatusAsync(DateTime? from = null, DateTime? to = null)
    {
        var where = "WHERE 1=1";
        if (from.HasValue) where += " AND created_at >= @from";
        if (to.HasValue) where += " AND created_at < @to";
        var parts = await Task.WhenAll(_shards.Ids.Select(id => _shards.RunAsync(id, c =>
            c.QueryAsync<StatusCount>($"SELECT status, count(*) AS Count FROM readings {where} GROUP BY status", new { from, to }))));
        return parts.SelectMany(p => p).GroupBy(p => p.Status)
            .Select(g => new ReadingsByStatusDto { Status = g.Key, Count = checked((int)g.Sum(p => p.Count)) })
            .OrderByDescending(p => p.Count).ThenBy(p => p.Status, StringComparer.Ordinal).ToList();
    }

    public async Task<List<DeckUsageDto>> GetDeckUsageAsync()
    {
        var decks = await _primary.Decks.AsNoTracking().ToListAsync();
        var cards = await _primary.Cards.AsNoTracking().Where(c => c.DeckId != null).ToListAsync();
        var cardIds = cards.Select(c => c.Id).ToArray();
        var deckIds = cards.Select(c => c.DeckId!.Value).ToArray();
        // Reference mapping is small; aggregation and DISTINCT execute on each shard.
        var parts = await Task.WhenAll(_shards.Ids.Select(id => _shards.RunAsync(id, c => c.QueryAsync<KeyCount>(
            "SELECT m.deck_id AS Key, count(DISTINCT r.id) AS Count " +
            "FROM unnest(@cardIds::integer[], @deckIds::integer[]) AS m(card_id,deck_id) " +
            "JOIN reading_cards rc ON rc.card_id=m.card_id JOIN readings r ON r.id=rc.reading_id GROUP BY m.deck_id",
            new { cardIds, deckIds }))));
        var counts = parts.SelectMany(p => p).GroupBy(p => p.Key).ToDictionary(g => g.Key, g => g.Sum(x => x.Count));
        return decks.Select(d => new DeckUsageDto
        {
            DeckId = d.Id, DeckName = d.Name, CardsCount = cards.Count(c => c.DeckId == d.Id),
            ReadingsCount = checked((int)counts.GetValueOrDefault(d.Id))
        }).OrderByDescending(d => d.ReadingsCount).ThenBy(d => d.DeckId).ToList();
    }

    public async Task<List<SpreadStatsDto>> GetSpreadStatsAsync()
    {
        var spreads = await _primary.Spreads.AsNoTracking().ToListAsync();
        var parts = await Task.WhenAll(_shards.Ids.Select(id => _shards.RunAsync(id, c => c.QueryAsync<SpreadCount>(
            "SELECT r.spread_id AS Key, count(*) AS Count, sum(coalesce(rc.cnt,0))::bigint AS Cards " +
            "FROM readings r LEFT JOIN (SELECT reading_id, count(*) AS cnt FROM reading_cards GROUP BY reading_id) rc " +
            "ON rc.reading_id=r.id GROUP BY r.spread_id"))));
        var counts = parts.SelectMany(p => p).GroupBy(p => p.Key)
            .ToDictionary(g => g.Key, g => (Count: g.Sum(x => x.Count), Cards: g.Sum(x => x.Cards)));
        return spreads.Select(s =>
        {
            var result = counts.GetValueOrDefault(s.Id);
            return new SpreadStatsDto { SpreadId = s.Id, SpreadName = s.Name,
                ReadingsCount = checked((int)result.Count),
                AvgCardsPerReading = result.Count == 0 ? 0 : result.Cards / (double)result.Count };
        }).OrderByDescending(s => s.ReadingsCount).ThenBy(s => s.SpreadId).ToList();
    }

    private sealed class StatusCount { public string Status { get; set; } = ""; public long Count { get; set; } }
    private sealed class KeyCount { public int Key { get; set; } public long Count { get; set; } }
    private sealed class SpreadCount { public int Key { get; set; } public long Count { get; set; } public long Cards { get; set; } }
}
