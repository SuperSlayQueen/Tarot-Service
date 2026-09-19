using Microsoft.EntityFrameworkCore;
using test_project.Data;
using test_project.Models.Entities;
using test_project.Repositories.Interfaces;

namespace test_project.Repositories;

public class SpreadRepository : ISpreadRepository
{
    private readonly TarotDbContext _context;

    public SpreadRepository(TarotDbContext context)
    {
        _context = context;
    }

    public async Task<Spread?> GetByIdAsync(int id)
    {
        return await _context.Spreads
            .Include(s => s.SpreadCards)
                .ThenInclude(sc => sc.Card)
                    .ThenInclude(c => c.Deck)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<List<Spread>> GetAllAsync()
    {
        return await _context.Spreads
            .Include(s => s.SpreadCards)
                .ThenInclude(sc => sc.Card)
            .ToListAsync();
    }

    public async Task<Spread> CreateAsync(Spread spread)
    {
        spread.CreatedAt = DateTime.UtcNow;
        _context.Spreads.Add(spread);
        await _context.SaveChangesAsync();
        return spread;
    }

    public async Task<Spread> UpdateAsync(Spread spread)
    {
        spread.UpdatedAt = DateTime.UtcNow;
        _context.Spreads.Update(spread);
        await _context.SaveChangesAsync();
        return spread;
    }

    public async Task DeleteAsync(int id)
    {
        var spread = await _context.Spreads.FindAsync(id);
        if (spread != null)
        {
            _context.Spreads.Remove(spread);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(int id)
    {
        return await _context.Spreads.AnyAsync(s => s.Id == id);
    }

    public async Task<SpreadCard?> GetSpreadCardByIdAsync(int id)
    {
        return await _context.SpreadCards
            .Include(sc => sc.Card)
            .Include(sc => sc.Spread)
            .FirstOrDefaultAsync(sc => sc.Id == id);
    }

    public async Task<SpreadCard> AddCardToSpreadAsync(SpreadCard spreadCard)
    {
        spreadCard.CreatedAt = DateTime.UtcNow;
        _context.SpreadCards.Add(spreadCard);
        await _context.SaveChangesAsync();
        return spreadCard;
    }

    public async Task RemoveCardFromSpreadAsync(int spreadCardId)
    {
        var spreadCard = await _context.SpreadCards.FindAsync(spreadCardId);
        if (spreadCard != null)
        {
            _context.SpreadCards.Remove(spreadCard);
            await _context.SaveChangesAsync();
        }
    }
}
