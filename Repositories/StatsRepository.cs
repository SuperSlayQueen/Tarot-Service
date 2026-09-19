using Microsoft.EntityFrameworkCore;
using test_project.Data;
using test_project.Models.DTO;
using test_project.Repositories.Interfaces;

namespace test_project.Repositories;

/// <summary>
/// Агрегирующие и JOIN-запросы для аналитики.
/// </summary>
public class StatsRepository : IStatsRepository
{
    private readonly TarotDbContext _context;

    public StatsRepository(TarotDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// JOIN cards + decks, GROUP BY suit — количество карт и колод по масти.
    /// </summary>
    public async Task<List<CardsBySuitDto>> GetCardsBySuitAsync()
    {
        return await (
            from c in _context.Cards
            join d in _context.Decks on c.DeckId equals d.Id into dj
            from d in dj.DefaultIfEmpty()
            group new { c, d } by c.Suit into g
            select new CardsBySuitDto
            {
                Suit = g.Key,
                CardsCount = g.Count(),
                DecksCount = g.Select(x => x.d != null ? x.d.Id : 0).Distinct().Count()
            }
        ).OrderByDescending(x => x.CardsCount).ToListAsync();
    }

    /// <summary>
    /// Агрегация readings по статусу с фильтром по периоду.
    /// </summary>
    public async Task<List<ReadingsByStatusDto>> GetReadingsByStatusAsync(DateTime? from = null, DateTime? to = null)
    {
        var q = _context.Readings.AsQueryable();
        if (from.HasValue) q = q.Where(r => r.CreatedAt >= from.Value);
        if (to.HasValue) q = q.Where(r => r.CreatedAt < to.Value);

        return await q
            .GroupBy(r => r.Status)
            .Select(g => new ReadingsByStatusDto { Status = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync();
    }

    /// <summary>
    /// JOIN decks → cards → reading_cards → readings: использование колод в гаданиях.
    /// </summary>
    public async Task<List<DeckUsageDto>> GetDeckUsageAsync()
    {
        return await (
            from d in _context.Decks
            join c in _context.Cards on d.Id equals c.DeckId into cards
            select new DeckUsageDto
            {
                DeckId = d.Id,
                DeckName = d.Name,
                CardsCount = cards.Count(),
                ReadingsCount = (
                    from c2 in _context.Cards
                    join rc in _context.ReadingCards on c2.Id equals rc.CardId
                    join r in _context.Readings on rc.ReadingId equals r.Id
                    where c2.DeckId == d.Id
                    select r.Id
                ).Distinct().Count()
            }
        ).OrderByDescending(x => x.ReadingsCount).ToListAsync();
    }

    /// <summary>
    /// JOIN spreads → readings → reading_cards: статистика шаблонов раскладов.
    /// </summary>
    public async Task<List<SpreadStatsDto>> GetSpreadStatsAsync()
    {
        return await (
            from s in _context.Spreads
            join r in _context.Readings on s.Id equals r.SpreadId into rj
            select new SpreadStatsDto
            {
                SpreadId = s.Id,
                SpreadName = s.Name,
                ReadingsCount = rj.Count(),
                AvgCardsPerReading = rj.Any()
                    ? rj.SelectMany(x => x.ReadingCards).Count() / (double)rj.Count()
                    : 0
            }
        ).OrderByDescending(x => x.ReadingsCount).ToListAsync();
    }
}
