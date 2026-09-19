using test_project.Models.DTO;

namespace test_project.Services.Interfaces;

public interface ICardService
{
    Task<CardDto?> GetByIdAsync(int id, string? userRole);
    Task<PagedResponseDto<CardDto>> GetPagedAsync(PaginationQueryDto query, string? userRole);
    Task<List<CardDto>> GetByDeckIdAsync(int deckId, string? userRole);
    Task<CardDto> CreateAsync(CreateCardDto dto, string? userRole);
    Task<CardDto> UpdateAsync(int id, UpdateCardDto dto, string? userRole);
    Task DeleteAsync(int id, string? userRole);
}
