using test_project.Models.DTO;
using test_project.Models.Entities;
using test_project.Repositories.Interfaces;
using test_project.Services.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace test_project.Services;

public class SpreadService : ISpreadService
{
    private readonly ISpreadRepository _spreadRepository;
    private readonly ICardRepository _cardRepository;
    private readonly IDistributedCache _cache;
    private readonly ILogger<SpreadService> _logger;

    public SpreadService(
        ISpreadRepository spreadRepository,
        ICardRepository cardRepository,
        IDistributedCache cache,
        ILogger<SpreadService> logger)
    {
        _spreadRepository = spreadRepository;
        _cardRepository = cardRepository;
        _cache = cache;
        _logger = logger;
    }

    public async Task<SpreadDto?> GetByIdAsync(int id, string? userRole)
    {
        _logger.LogInformation("Получение расклада по ID: {SpreadId}, роль: {Role}", id, userRole);

        var spread = await _spreadRepository.GetByIdAsync(id);
        if (spread == null)
        {
            _logger.LogWarning("Расклад не найден: {SpreadId}", id);
            return null;
        }

        return MapToDto(spread);
    }

    public async Task<List<SpreadDto>> GetAllAsync(string? userRole)
    {
        _logger.LogInformation("Получение всех раскладов, роль: {Role}", userRole);

        var spreads = await _spreadRepository.GetAllAsync();
        return spreads.Select(MapToDto).ToList();
    }

    public async Task<SpreadDto> CreateAsync(CreateSpreadDto dto, string? userRole)
    {
        _logger.LogInformation("Создание расклада: {Name}, роль: {Role}", dto.Name, userRole);

        if (userRole != "Admin" && userRole != "Manager")
        {
            _logger.LogWarning("Доступ запрещен для создания расклада. Роль: {Role}", userRole);
            throw new UnauthorizedAccessException("Доступ запрещен. Требуется роль Admin или Manager");
        }

        var spread = new Spread
        {
            Name = dto.Name,
            Description = dto.Description,
            NumberOfPositions = dto.NumberOfPositions,
            PositionNames = dto.PositionNames
        };

        var created = await _spreadRepository.CreateAsync(spread);
        _logger.LogInformation("Расклад создан: {SpreadId}", created.Id);

        return MapToDto(created);
    }

    public async Task<SpreadDto> UpdateAsync(int id, UpdateSpreadDto dto, string? userRole)
    {
        _logger.LogInformation("Обновление расклада: {SpreadId}, роль: {Role}", id, userRole);

        if (userRole != "Admin" && userRole != "Manager")
        {
            _logger.LogWarning("Доступ запрещен для обновления расклада. Роль: {Role}", userRole);
            throw new UnauthorizedAccessException("Доступ запрещен. Требуется роль Admin или Manager");
        }

        var spread = await _spreadRepository.GetByIdAsync(id);
        if (spread == null)
        {
            throw new KeyNotFoundException("Расклад не найден");
        }

        spread.Name = dto.Name;
        spread.Description = dto.Description;
        spread.NumberOfPositions = dto.NumberOfPositions;
        spread.PositionNames = dto.PositionNames;

        var updated = await _spreadRepository.UpdateAsync(spread);
        _logger.LogInformation("Расклад обновлен: {SpreadId}", id);

        return MapToDto(updated);
    }

    public async Task DeleteAsync(int id, string? userRole)
    {
        _logger.LogInformation("Удаление расклада: {SpreadId}, роль: {Role}", id, userRole);

        if (userRole != "Admin")
        {
            _logger.LogWarning("Доступ запрещен для удаления расклада. Роль: {Role}", userRole);
            throw new UnauthorizedAccessException("Доступ запрещен. Требуется роль Admin");
        }

        var exists = await _spreadRepository.ExistsAsync(id);
        if (!exists)
        {
            throw new KeyNotFoundException("Расклад не найден");
        }

        await _spreadRepository.DeleteAsync(id);
        _logger.LogInformation("Расклад удален: {SpreadId}", id);
    }

    public async Task<SpreadCardDto> AddCardToSpreadAsync(int spreadId, AddCardToSpreadDto dto, string? userRole)
    {
        _logger.LogInformation("Добавление карты в расклад: {SpreadId}, карта: {CardId}, роль: {Role}",
            spreadId, dto.CardId, userRole);

        if (userRole != "Admin" && userRole != "Manager")
        {
            _logger.LogWarning("Доступ запрещен для добавления карты в расклад. Роль: {Role}", userRole);
            throw new UnauthorizedAccessException("Доступ запрещен. Требуется роль Admin или Manager");
        }

        var spread = await _spreadRepository.GetByIdAsync(spreadId);
        if (spread == null)
        {
            throw new KeyNotFoundException("Расклад не найден");
        }

        var cardExists = await _cardRepository.ExistsAsync(dto.CardId);
        if (!cardExists)
        {
            throw new KeyNotFoundException("Карта не найдена");
        }

        var spreadCard = new SpreadCard
        {
            SpreadId = spreadId,
            CardId = dto.CardId,
            Position = dto.Position,
            IsReversed = dto.IsReversed
        };

        var created = await _spreadRepository.AddCardToSpreadAsync(spreadCard);
        
        var card = await _cardRepository.GetByIdAsync(dto.CardId);
        _logger.LogInformation("Карта добавлена в расклад: {SpreadCardId}", created.Id);

        return new SpreadCardDto
        {
            Id = created.Id,
            CardId = created.CardId,
            CardName = card?.Name ?? "",
            Position = created.Position,
            IsReversed = created.IsReversed
        };
    }

    public async Task RemoveCardFromSpreadAsync(int spreadId, int spreadCardId, string? userRole)
    {
        _logger.LogInformation("Удаление карты из расклада: {SpreadId}, {SpreadCardId}, роль: {Role}",
            spreadId, spreadCardId, userRole);

        if (userRole != "Admin" && userRole != "Manager")
        {
            _logger.LogWarning("Доступ запрещен для удаления карты из расклада. Роль: {Role}", userRole);
            throw new UnauthorizedAccessException("Доступ запрещен. Требуется роль Admin или Manager");
        }

        var spreadCard = await _spreadRepository.GetSpreadCardByIdAsync(spreadCardId);
        if (spreadCard == null || spreadCard.SpreadId != spreadId)
        {
            throw new KeyNotFoundException("Связь карты с раскладом не найдена");
        }

        await _spreadRepository.RemoveCardFromSpreadAsync(spreadCardId);
        _logger.LogInformation("Карта удалена из расклада: {SpreadCardId}", spreadCardId);
    }

    private static SpreadDto MapToDto(Spread spread)
    {
        return new SpreadDto
        {
            Id = spread.Id,
            Name = spread.Name,
            Description = spread.Description,
            NumberOfPositions = spread.NumberOfPositions,
            PositionNames = spread.PositionNames,
            Cards = spread.SpreadCards.Select(sc => new SpreadCardDto
            {
                Id = sc.Id,
                CardId = sc.CardId,
                CardName = sc.Card?.Name ?? "",
                Position = sc.Position,
                IsReversed = sc.IsReversed
            }).OrderBy(sc => sc.Position).ToList(),
            CreatedAt = spread.CreatedAt,
            UpdatedAt = spread.UpdatedAt
        };
    }
}
