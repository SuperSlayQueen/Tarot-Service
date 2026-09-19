using test_project.Models.DTO;
using test_project.Models.Entities;
using test_project.Repositories.Interfaces;
using test_project.Services.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace test_project.Services;

public class DeckService : IDeckService
{
    private readonly IDeckRepository _deckRepository;
    private readonly IDistributedCache _cache;
    private readonly ILogger<DeckService> _logger;

    public DeckService(
        IDeckRepository deckRepository,
        IDistributedCache cache,
        ILogger<DeckService> logger)
    {
        _deckRepository = deckRepository;
        _cache = cache;
        _logger = logger;
    }

    public async Task<DeckDto?> GetByIdAsync(int id, string? userRole)
    {
        _logger.LogInformation("Получение колоды по ID: {DeckId}, роль: {Role}", id, userRole);

        var cacheKey = $"deck_{id}";
        var cachedDeck = await _cache.GetStringAsync(cacheKey);
        if (!string.IsNullOrEmpty(cachedDeck))
        {
            return JsonSerializer.Deserialize<DeckDto>(cachedDeck);
        }

        var deck = await _deckRepository.GetByIdAsync(id);
        if (deck == null)
        {
            _logger.LogWarning("Колода не найдена: {DeckId}", id);
            return null;
        }

        var dto = MapToDto(deck);
        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(dto),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) });

        return dto;
    }

    public async Task<List<DeckDto>> GetAllAsync(string? userRole)
    {
        _logger.LogInformation("Получение всех колод, роль: {Role}", userRole);

        var decks = await _deckRepository.GetAllAsync();
        return decks.Select(MapToDto).ToList();
    }

    public async Task<DeckDto> CreateAsync(CreateDeckDto dto, string? userRole)
    {
        _logger.LogInformation("Создание колоды: {Name}, роль: {Role}", dto.Name, userRole);

        if (userRole != "Admin" && userRole != "Manager")
        {
            _logger.LogWarning("Доступ запрещен для создания колоды. Роль: {Role}", userRole);
            throw new UnauthorizedAccessException("Доступ запрещен. Требуется роль Admin или Manager");
        }

        var deck = new Deck
        {
            Name = dto.Name,
            Description = dto.Description,
            Author = dto.Author
        };

        var created = await _deckRepository.CreateAsync(deck);
        _logger.LogInformation("Колода создана: {DeckId}", created.Id);

        return MapToDto(created);
    }

    public async Task<DeckDto> UpdateAsync(int id, UpdateDeckDto dto, string? userRole)
    {
        _logger.LogInformation("Обновление колоды: {DeckId}, роль: {Role}", id, userRole);

        if (userRole != "Admin" && userRole != "Manager")
        {
            _logger.LogWarning("Доступ запрещен для обновления колоды. Роль: {Role}", userRole);
            throw new UnauthorizedAccessException("Доступ запрещен. Требуется роль Admin или Manager");
        }

        var deck = await _deckRepository.GetByIdAsync(id);
        if (deck == null)
        {
            throw new KeyNotFoundException("Колода не найдена");
        }

        deck.Name = dto.Name;
        deck.Description = dto.Description;
        deck.Author = dto.Author;

        var updated = await _deckRepository.UpdateAsync(deck);
        _logger.LogInformation("Колода обновлена: {DeckId}", id);

        await _cache.RemoveAsync($"deck_{id}");

        return MapToDto(updated);
    }

    public async Task DeleteAsync(int id, string? userRole)
    {
        _logger.LogInformation("Удаление колоды: {DeckId}, роль: {Role}", id, userRole);

        if (userRole != "Admin")
        {
            _logger.LogWarning("Доступ запрещен для удаления колоды. Роль: {Role}", userRole);
            throw new UnauthorizedAccessException("Доступ запрещен. Требуется роль Admin");
        }

        var exists = await _deckRepository.ExistsAsync(id);
        if (!exists)
        {
            throw new KeyNotFoundException("Колода не найдена");
        }

        await _deckRepository.DeleteAsync(id);
        _logger.LogInformation("Колода удалена: {DeckId}", id);

        await _cache.RemoveAsync($"deck_{id}");
    }

    private static DeckDto MapToDto(Deck deck)
    {
        return new DeckDto
        {
            Id = deck.Id,
            Name = deck.Name,
            Description = deck.Description,
            Author = deck.Author,
            CardsCount = deck.Cards?.Count ?? 0,
            CreatedAt = deck.CreatedAt,
            UpdatedAt = deck.UpdatedAt
        };
    }
}
