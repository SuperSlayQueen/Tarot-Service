using test_project.Models.Entities;
using test_project.Models.DTO;

namespace test_project.Repositories.Interfaces;

public interface ICardRepository
{
    Task<Card?> GetByIdAsync(int id);
    Task<List<Card>> GetAllAsync();
    Task<List<Card>> GetByDeckIdAsync(int deckId);
    Task<Card> CreateAsync(Card card);
    Task<Card> UpdateAsync(Card card);
    Task DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
    Task<PagedResponseDto<Card>> GetPagedAsync(PaginationQueryDto query);
}
