using Microsoft.EntityFrameworkCore;
using test_project.Data;
using test_project.Models.Entities;
using test_project.Repositories.Interfaces;

namespace test_project.Repositories;

public class DeckRepository : IDeckRepository
{
    private readonly TarotDbContext _context;

    public DeckRepository(TarotDbContext context)
    {
        _context = context;
    }

    public async Task<Deck?> GetByIdAsync(int id)
    {
        return await _context.Decks
            .Include(d => d.Cards)
            .FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task<List<Deck>> GetAllAsync()
    {
        return await _context.Decks
            .Include(d => d.Cards)
            .ToListAsync();
    }

    public async Task<Deck> CreateAsync(Deck deck)
    {
        deck.CreatedAt = DateTime.UtcNow;
        _context.Decks.Add(deck);
        await _context.SaveChangesAsync();
        return deck;
    }

    public async Task<Deck> UpdateAsync(Deck deck)
    {
        deck.UpdatedAt = DateTime.UtcNow;
        _context.Decks.Update(deck);
        await _context.SaveChangesAsync();
        return deck;
    }

    public async Task DeleteAsync(int id)
    {
        var deck = await _context.Decks.FindAsync(id);
        if (deck != null)
        {
            _context.Decks.Remove(deck);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(int id)
    {
        return await _context.Decks.AnyAsync(d => d.Id == id);
    }
}
