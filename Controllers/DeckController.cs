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
public class DeckController : ControllerBase
{
    private readonly IDeckService _deckService;
    private readonly ILogger<DeckController> _logger;

    public DeckController(IDeckService deckService, ILogger<DeckController> logger)
    {
        _deckService = deckService;
        _logger = logger;
    }

    private string? GetUserRole()
    {
        return User.FindFirst(ClaimTypes.Role)?.Value;
    }

    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<DeckDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<DeckDto>>> GetAll()
    {
        var decks = await _deckService.GetAllAsync(GetUserRole());
        return Ok(decks);
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(DeckDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeckDto>> GetById(int id)
    {
        var deck = await _deckService.GetByIdAsync(id, GetUserRole());
        if (deck == null)
        {
            return NotFound();
        }

        return Ok(deck);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(DeckDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DeckDto>> Create([FromBody] CreateDeckDto dto)
    {
        var deck = await _deckService.CreateAsync(dto, GetUserRole());
        return CreatedAtAction(nameof(GetById), new { id = deck.Id }, deck);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(DeckDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DeckDto>> Update(int id, [FromBody] UpdateDeckDto dto)
    {
        var deck = await _deckService.UpdateAsync(id, dto, GetUserRole());
        return Ok(deck);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(int id)
    {
        await _deckService.DeleteAsync(id, GetUserRole());
        return NoContent();
    }
}
