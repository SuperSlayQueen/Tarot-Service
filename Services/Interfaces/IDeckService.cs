using test_project.Models.DTO;

namespace test_project.Services.Interfaces;

public interface IDeckService
{
    Task<DeckDto?> GetByIdAsync(int id, string? userRole);
    Task<List<DeckDto>> GetAllAsync(string? userRole);
    Task<DeckDto> CreateAsync(CreateDeckDto dto, string? userRole);
    Task<DeckDto> UpdateAsync(int id, UpdateDeckDto dto, string? userRole);
    Task DeleteAsync(int id, string? userRole);
}
