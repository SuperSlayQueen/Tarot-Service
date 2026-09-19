using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using test_project.Models.DTO;
using test_project.Services.Interfaces;

namespace test_project.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class CardController : ControllerBase
{
    private readonly ICardService _cardService;
    private readonly ILogger<CardController> _logger;

    public CardController(ICardService cardService, ILogger<CardController> logger)
    {
        _cardService = cardService;
        _logger = logger;
    }

    private string? GetUserRole()
    {
        return User.FindFirst(ClaimTypes.Role)?.Value;
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(CardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CardDto>> GetById(int id)
    {
        var card = await _cardService.GetByIdAsync(id, GetUserRole());
        if (card == null)
        {
            return NotFound();
        }

        return Ok(card);
    }

    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PagedResponseDto<CardDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponseDto<CardDto>>> GetPaged([FromQuery] PaginationQueryDto query)
    {
        var result = await _cardService.GetPagedAsync(query, GetUserRole());
        return Ok(result);
    }

    [HttpGet("deck/{deckId}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<CardDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CardDto>>> GetByDeckId(int deckId)
    {
        var cards = await _cardService.GetByDeckIdAsync(deckId, GetUserRole());
        return Ok(cards);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(CardDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CardDto>> Create([FromBody] CreateCardDto dto)
    {
        var card = await _cardService.CreateAsync(dto, GetUserRole());
        return CreatedAtAction(nameof(GetById), new { id = card.Id }, card);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(CardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CardDto>> Update(int id, [FromBody] UpdateCardDto dto)
    {
        var card = await _cardService.UpdateAsync(id, dto, GetUserRole());
        return Ok(card);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(int id)
    {
        await _cardService.DeleteAsync(id, GetUserRole());
        return NoContent();
    }
}
