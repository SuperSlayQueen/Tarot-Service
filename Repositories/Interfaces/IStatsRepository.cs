using test_project.Models.DTO;

namespace test_project.Repositories.Interfaces;

public interface IStatsRepository
{
    Task<List<CardsBySuitDto>> GetCardsBySuitAsync();
    Task<List<ReadingsByStatusDto>> GetReadingsByStatusAsync(DateTime? from = null, DateTime? to = null);
    Task<List<DeckUsageDto>> GetDeckUsageAsync();
    Task<List<SpreadStatsDto>> GetSpreadStatsAsync();
}
