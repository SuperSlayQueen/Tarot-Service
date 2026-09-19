using Microsoft.EntityFrameworkCore;
using test_project.Data;
using test_project.Models.Entities;
using test_project.Models.DTO;
using test_project.Repositories.Interfaces;

namespace test_project.Repositories;

public class CardRepository : ICardRepository
{
    private readonly TarotDbContext _context;

    public CardRepository(TarotDbContext context)
    {
        _context = context;
    }

    public async Task<Card?> GetByIdAsync(int id)
    {
        return await _context.Cards
            .Include(c => c.Deck)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<List<Card>> GetAllAsync()
    {
        return await _context.Cards
            .Include(c => c.Deck)
            .ToListAsync();
    }

    public async Task<List<Card>> GetByDeckIdAsync(int deckId)
    {
        return await _context.Cards
            .Where(c => c.DeckId == deckId)
            .Include(c => c.Deck)
            .ToListAsync();
    }

    public async Task<Card> CreateAsync(Card card)
    {
        card.CreatedAt = DateTime.UtcNow;
        _context.Cards.Add(card);
        await _context.SaveChangesAsync();
        return card;
    }

    public async Task<Card> UpdateAsync(Card card)
    {
        card.UpdatedAt = DateTime.UtcNow;
        _context.Cards.Update(card);
        await _context.SaveChangesAsync();
        return card;
    }

    public async Task DeleteAsync(int id)
    {
        var card = await _context.Cards.FindAsync(id);
        if (card != null)
        {
            _context.Cards.Remove(card);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(int id)
    {
        return await _context.Cards.AnyAsync(c => c.Id == id);
    }

    public async Task<PagedResponseDto<Card>> GetPagedAsync(PaginationQueryDto query)
    {
        var queryable = _context.Cards.Include(c => c.Deck).AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.ToLower();
            queryable = queryable.Where(c =>
                c.Name.ToLower().Contains(search) ||
                (c.Description != null && c.Description.ToLower().Contains(search)) ||
                c.Suit.ToLower().Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(query.Suit))
            queryable = queryable.Where(c => c.Suit == query.Suit);

        queryable = (query.Sort?.ToLowerInvariant()) switch
        {
            "name" => queryable.OrderBy(c => c.Name),
            "-name" => queryable.OrderByDescending(c => c.Name),
            "created_at" => queryable.OrderBy(c => c.CreatedAt),
            "-created_at" => queryable.OrderByDescending(c => c.CreatedAt),
            "suit" => queryable.OrderBy(c => c.Suit),
            "-suit" => queryable.OrderByDescending(c => c.Suit),
            _ => queryable.OrderBy(c => c.Name)
        };

        var total = await queryable.CountAsync();
        var items = await queryable
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        return new PagedResponseDto<Card>
        {
            Items = items,
            Total = total,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }
}
