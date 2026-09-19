using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using test_project.Models.DTO;
using test_project.Services.Interfaces;

namespace test_project.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
[Produces("application/json")]
public class StatsController : ControllerBase
{
    private readonly IStatsService _statsService;

    public StatsController(IStatsService statsService)
    {
        _statsService = statsService;
    }

    /// <summary>Агрегация: количество карт по мастям (JOIN cards/decks + GROUP BY).</summary>
    [HttpGet("cards-by-suit")]
    [ProducesResponseType(typeof(List<CardsBySuitDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CardsBySuitDto>>> CardsBySuit() =>
        Ok(await _statsService.GetCardsBySuitAsync());

    /// <summary>Агрегация: количество гаданий по статусам.</summary>
    [HttpGet("readings-by-status")]
    [ProducesResponseType(typeof(List<ReadingsByStatusDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ReadingsByStatusDto>>> ReadingsByStatus(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null) =>
        Ok(await _statsService.GetReadingsByStatusAsync(from, to));

    /// <summary>JOIN decks/cards/reading_cards/readings — использование колод.</summary>
    [HttpGet("deck-usage")]
    [ProducesResponseType(typeof(List<DeckUsageDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<DeckUsageDto>>> DeckUsage() =>
        Ok(await _statsService.GetDeckUsageAsync());

    /// <summary>JOIN spreads/readings — статистика шаблонов раскладов.</summary>
    [HttpGet("spread-stats")]
    [ProducesResponseType(typeof(List<SpreadStatsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SpreadStatsDto>>> SpreadStats() =>
        Ok(await _statsService.GetSpreadStatsAsync());
}
