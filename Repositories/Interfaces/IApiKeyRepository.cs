using test_project.Models.Entities;

namespace test_project.Repositories.Interfaces;

public interface IApiKeyRepository
{
    Task<ApiKey?> GetByKeyAsync(string key);
    Task<ApiKey?> GetByIdAsync(int id);
    Task<List<ApiKey>> GetAllAsync();
    Task<ApiKey> CreateAsync(ApiKey apiKey);
    Task<ApiKey> UpdateAsync(ApiKey apiKey);
    Task DeleteAsync(int id);
}
