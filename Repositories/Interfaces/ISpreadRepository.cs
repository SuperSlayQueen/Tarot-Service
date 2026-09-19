using test_project.Models.Entities;

namespace test_project.Repositories.Interfaces;

public interface ISpreadRepository
{
    Task<Spread?> GetByIdAsync(int id);
    Task<List<Spread>> GetAllAsync();
    Task<Spread> CreateAsync(Spread spread);
    Task<Spread> UpdateAsync(Spread spread);
    Task DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
    Task<SpreadCard?> GetSpreadCardByIdAsync(int id);
    Task<SpreadCard> AddCardToSpreadAsync(SpreadCard spreadCard);
    Task RemoveCardFromSpreadAsync(int spreadCardId);
}
