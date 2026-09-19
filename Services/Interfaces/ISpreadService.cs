using test_project.Models.DTO;

namespace test_project.Services.Interfaces;

public interface ISpreadService
{
    Task<SpreadDto?> GetByIdAsync(int id, string? userRole);
    Task<List<SpreadDto>> GetAllAsync(string? userRole);
    Task<SpreadDto> CreateAsync(CreateSpreadDto dto, string? userRole);
    Task<SpreadDto> UpdateAsync(int id, UpdateSpreadDto dto, string? userRole);
    Task DeleteAsync(int id, string? userRole);
    Task<SpreadCardDto> AddCardToSpreadAsync(int spreadId, AddCardToSpreadDto dto, string? userRole);
    Task RemoveCardFromSpreadAsync(int spreadId, int spreadCardId, string? userRole);
}
