using test_project.Models.DTO;
using test_project.Models.Entities;
using test_project.Repositories.Interfaces;
using test_project.Services.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace test_project.Services;

public class CardService : ICardService
{
    private readonly ICardRepository _cardRepository;
    private readonly IDeckRepository _deckRepository;
    private readonly IDistributedCache _cache;
    private readonly ILogger<CardService> _logger;

    public CardService(
        ICardRepository cardRepository,
        IDeckRepository deckRepository,
        IDistributedCache cache,
        ILogger<CardService> logger)
    {
        _cardRepository = cardRepository;
        _deckRepository = deckRepository;
        _cache = cache;
        _logger = logger;
    }

    public async Task<CardDto?> GetByIdAsync(int id, string? userRole)
    {
        _logger.LogInformation("Получение карты по ID: {CardId}, роль: {Role}", id, userRole);

        var cacheKey = $"card_{id}";
        var cachedCard = await _cache.GetStringAsync(cacheKey);
        if (!string.IsNullOrEmpty(cachedCard))
        {
            return JsonSerializer.Deserialize<CardDto>(cachedCard);
        }

        var card = await _cardRepository.GetByIdAsync(id);
        if (card == null)
        {
            _logger.LogWarning("Карта не найдена: {CardId}", id);
            return null;
        }

        var dto = MapToDto(card);
        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(dto),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) });

        return dto;
    }

    public async Task<PagedResponseDto<CardDto>> GetPagedAsync(PaginationQueryDto query, string? userRole)
    {
        _logger.LogInformation("Получение страницы карт, страница: {Page}, размер: {PageSize}, роль: {Role}",
            query.Page, query.PageSize, userRole);

        var result = await _cardRepository.GetPagedAsync(query);
        var dtos = result.Items.Select(MapToDto).ToList();

        return new PagedResponseDto<CardDto>
        {
            Items = dtos,
            Total = result.Total,
            Page = result.Page,
            PageSize = result.PageSize
        };
    }

    public async Task<List<CardDto>> GetByDeckIdAsync(int deckId, string? userRole)
    {
        _logger.LogInformation("Получение карт колоды: {DeckId}, роль: {Role}", deckId, userRole);

        var cards = await _cardRepository.GetByDeckIdAsync(deckId);
        return cards.Select(MapToDto).ToList();
    }

    public async Task<CardDto> CreateAsync(CreateCardDto dto, string? userRole)
    {
        _logger.LogInformation("Создание карты: {Name}, роль: {Role}", dto.Name, userRole);

        if (userRole != "Admin" && userRole != "Manager")
        {
            _logger.LogWarning("Доступ запрещен для создания карты. Роль: {Role}", userRole);
            throw new UnauthorizedAccessException("Доступ запрещен. Требуется роль Admin или Manager");
        }

        if (dto.DeckId.HasValue)
        {
            var deckExists = await _deckRepository.ExistsAsync(dto.DeckId.Value);
            if (!deckExists)
            {
                throw new KeyNotFoundException("Колода не найдена");
            }
        }

        var card = new Card
        {
            Name = dto.Name,
            Description = dto.Description,
            ImageUrl = dto.ImageUrl,
            Suit = dto.Suit,
            Number = dto.Number,
            UprightMeaning = dto.UprightMeaning,
            ReversedMeaning = dto.ReversedMeaning,
            DeckId = dto.DeckId
        };

        var created = await _cardRepository.CreateAsync(card);
        _logger.LogInformation("Карта создана: {CardId}", created.Id);

        await InvalidateCacheAsync();

        return MapToDto(created);
    }

    public async Task<CardDto> UpdateAsync(int id, UpdateCardDto dto, string? userRole)
    {
        _logger.LogInformation("Обновление карты: {CardId}, роль: {Role}", id, userRole);

        if (userRole != "Admin" && userRole != "Manager")
        {
            _logger.LogWarning("Доступ запрещен для обновления карты. Роль: {Role}", userRole);
            throw new UnauthorizedAccessException("Доступ запрещен. Требуется роль Admin или Manager");
        }

        var card = await _cardRepository.GetByIdAsync(id);
        if (card == null)
        {
            throw new KeyNotFoundException("Карта не найдена");
        }

        if (dto.DeckId.HasValue)
        {
            var deckExists = await _deckRepository.ExistsAsync(dto.DeckId.Value);
            if (!deckExists)
            {
                throw new KeyNotFoundException("Колода не найдена");
            }
        }

        card.Name = dto.Name;
        card.Description = dto.Description;
        card.ImageUrl = dto.ImageUrl;
        card.Suit = dto.Suit;
        card.Number = dto.Number;
        card.UprightMeaning = dto.UprightMeaning;
        card.ReversedMeaning = dto.ReversedMeaning;
        card.DeckId = dto.DeckId;

        var updated = await _cardRepository.UpdateAsync(card);
        _logger.LogInformation("Карта обновлена: {CardId}", id);

        await _cache.RemoveAsync($"card_{id}");
        await InvalidateCacheAsync();

        return MapToDto(updated);
    }

    public async Task DeleteAsync(int id, string? userRole)
    {
        _logger.LogInformation("Удаление карты: {CardId}, роль: {Role}", id, userRole);

        if (userRole != "Admin")
        {
            _logger.LogWarning("Доступ запрещен для удаления карты. Роль: {Role}", userRole);
            throw new UnauthorizedAccessException("Доступ запрещен. Требуется роль Admin");
        }

        var exists = await _cardRepository.ExistsAsync(id);
        if (!exists)
        {
            throw new KeyNotFoundException("Карта не найдена");
        }

        await _cardRepository.DeleteAsync(id);
        _logger.LogInformation("Карта удалена: {CardId}", id);

        await _cache.RemoveAsync($"card_{id}");
        await InvalidateCacheAsync();
    }

    private static CardDto MapToDto(Card card)
    {
        return new CardDto
        {
            Id = card.Id,
            Name = card.Name,
            Description = card.Description,
            ImageUrl = card.ImageUrl,
            Suit = card.Suit,
            Number = card.Number,
            UprightMeaning = card.UprightMeaning,
            ReversedMeaning = card.ReversedMeaning,
            DeckId = card.DeckId,
            DeckName = card.Deck?.Name,
            CreatedAt = card.CreatedAt,
            UpdatedAt = card.UpdatedAt
        };
    }

    private async Task InvalidateCacheAsync()
    {
        await Task.CompletedTask;
    }
}
