using test_project.Models.DTO;
using test_project.Models.Entities;
using test_project.Repositories.Interfaces;
using test_project.Services.Interfaces;

namespace test_project.Services;

public class ReadingService : IReadingService
{
    private readonly IReadingRepository _readingRepository;
    private readonly IUserRepository _userRepository;
    private readonly ISpreadRepository _spreadRepository;
    private readonly ICardRepository _cardRepository;
    private readonly ILogger<ReadingService> _logger;

    public ReadingService(
        IReadingRepository readingRepository,
        IUserRepository userRepository,
        ISpreadRepository spreadRepository,
        ICardRepository cardRepository,
        ILogger<ReadingService> logger)
    {
        _readingRepository = readingRepository;
        _userRepository = userRepository;
        _spreadRepository = spreadRepository;
        _cardRepository = cardRepository;
        _logger = logger;
    }

    public async Task<ReadingDto?> GetByIdAsync(long id)
    {
        var reading = await _readingRepository.GetByIdAsync(id);
        return reading == null ? null : MapToDto(reading);
    }

    public async Task<PagedResponseDto<ReadingDto>> GetPagedAsync(ReadingQueryDto query)
    {
        var result = await _readingRepository.GetPagedAsync(query);
        return new PagedResponseDto<ReadingDto>
        {
            Items = result.Items.Select(MapToDto).ToList(),
            Total = result.Total,
            Page = result.Page,
            PageSize = result.PageSize
        };
    }

    public async Task<List<ReadingDto>> GetByUserIdAsync(int userId)
    {
        var readings = await _readingRepository.GetByUserIdAsync(userId);
        return readings.Select(MapToDto).ToList();
    }

    public async Task<ReadingDto> CreateAsync(CreateReadingDto dto)
    {
        var user = await _userRepository.GetByIdAsync(dto.UserId);
        if (user == null)
            throw new KeyNotFoundException("Пользователь не найден");

        if (!await _spreadRepository.ExistsAsync(dto.SpreadId))
            throw new KeyNotFoundException("Шаблон расклада не найден");

        var reading = new Reading
        {
            UserId = dto.UserId,
            SpreadId = dto.SpreadId,
            Question = dto.Question,
            Status = string.IsNullOrWhiteSpace(dto.Status) ? "NEW" : dto.Status
        };

        if (dto.Cards != null)
        {
            foreach (var cardDto in dto.Cards)
            {
                if (!await _cardRepository.ExistsAsync(cardDto.CardId))
                    throw new KeyNotFoundException($"Карта {cardDto.CardId} не найдена");

                reading.ReadingCards.Add(new ReadingCard
                {
                    CardId = cardDto.CardId,
                    Position = cardDto.Position,
                    IsReversed = cardDto.IsReversed
                });
            }
        }

        var created = await _readingRepository.CreateAsync(reading);
        _logger.LogInformation("Создано гадание {ReadingId}", created.Id);

        var full = await _readingRepository.GetByIdAsync(created.Id);
        return MapToDto(full!);
    }

    public async Task<ReadingDto> UpdateAsync(long id, UpdateReadingDto dto)
    {
        var reading = await _readingRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Гадание не найдено");

        reading.Question = dto.Question;
        reading.Status = dto.Status;

        await _readingRepository.UpdateAsync(reading);
        var full = await _readingRepository.GetByIdAsync(id);
        return MapToDto(full!);
    }

    public async Task DeleteAsync(long id)
    {
        if (!await _readingRepository.ExistsAsync(id))
            throw new KeyNotFoundException("Гадание не найдено");

        await _readingRepository.DeleteAsync(id);
    }

    private static ReadingDto MapToDto(Reading reading) => new()
    {
        Id = reading.Id,
        UserId = reading.UserId,
        Username = reading.User?.Username,
        SpreadId = reading.SpreadId,
        SpreadName = reading.Spread?.Name,
        Question = reading.Question,
        Status = reading.Status,
        CreatedAt = reading.CreatedAt,
        UpdatedAt = reading.UpdatedAt,
        Cards = reading.ReadingCards
            .OrderBy(rc => rc.Position)
            .Select(rc => new ReadingCardDto
            {
                Id = rc.Id,
                CardId = rc.CardId,
                CardName = rc.Card?.Name ?? "",
                Position = rc.Position,
                IsReversed = rc.IsReversed
            }).ToList()
    };
}

public class StatsService : IStatsService
{
    private readonly IStatsRepository _statsRepository;

    public StatsService(IStatsRepository statsRepository)
    {
        _statsRepository = statsRepository;
    }

    public Task<List<CardsBySuitDto>> GetCardsBySuitAsync() =>
        _statsRepository.GetCardsBySuitAsync();

    public Task<List<ReadingsByStatusDto>> GetReadingsByStatusAsync(DateTime? from = null, DateTime? to = null) =>
        _statsRepository.GetReadingsByStatusAsync(from, to);

    public Task<List<DeckUsageDto>> GetDeckUsageAsync() =>
        _statsRepository.GetDeckUsageAsync();

    public Task<List<SpreadStatsDto>> GetSpreadStatsAsync() =>
        _statsRepository.GetSpreadStatsAsync();
}
