using test_project.Models.DTO;

namespace test_project.Services.Interfaces;

public interface IReadingService
{
    Task<ReadingDto?> GetByIdAsync(long id);
    Task<PagedResponseDto<ReadingDto>> GetPagedAsync(ReadingQueryDto query);
    Task<List<ReadingDto>> GetByUserIdAsync(int userId);
    Task<ReadingDto> CreateAsync(CreateReadingDto dto);
    Task<ReadingDto> UpdateAsync(long id, UpdateReadingDto dto);
    Task DeleteAsync(long id);
}

public interface IStatsService
{
    Task<List<CardsBySuitDto>> GetCardsBySuitAsync();
    Task<List<ReadingsByStatusDto>> GetReadingsByStatusAsync(DateTime? from = null, DateTime? to = null);
    Task<List<DeckUsageDto>> GetDeckUsageAsync();
    Task<List<SpreadStatsDto>> GetSpreadStatsAsync();
}
