using test_project.Models.DTO;
using test_project.Models.Entities;

namespace test_project.Repositories.Interfaces;

public interface IReadingRepository
{
    Task<Reading?> GetByIdAsync(long id);
    Task<PagedResponseDto<Reading>> GetPagedAsync(ReadingQueryDto query);
    Task<List<Reading>> GetByUserIdAsync(int userId);
    Task<Reading> CreateAsync(Reading reading);
    Task<Reading> UpdateAsync(Reading reading);
    Task DeleteAsync(long id);
    Task<bool> ExistsAsync(long id);
}
