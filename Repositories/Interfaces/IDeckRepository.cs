using test_project.Models.Entities;

namespace test_project.Repositories.Interfaces;

public interface IDeckRepository
{
    Task<Deck?> GetByIdAsync(int id);
    Task<List<Deck>> GetAllAsync();
    Task<Deck> CreateAsync(Deck deck);
    Task<Deck> UpdateAsync(Deck deck);
    Task DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
}
