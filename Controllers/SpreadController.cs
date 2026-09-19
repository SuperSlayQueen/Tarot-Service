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
public class SpreadController : ControllerBase
{
    private readonly ISpreadService _spreadService;
    private readonly ILogger<SpreadController> _logger;

    public SpreadController(ISpreadService spreadService, ILogger<SpreadController> logger)
    {
        _spreadService = spreadService;
        _logger = logger;
    }

    private string? GetUserRole()
    {
        return User.FindFirst(ClaimTypes.Role)?.Value;
    }

    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<SpreadDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SpreadDto>>> GetAll()
    {
        var spreads = await _spreadService.GetAllAsync(GetUserRole());
        return Ok(spreads);
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SpreadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SpreadDto>> GetById(int id)
    {
        var spread = await _spreadService.GetByIdAsync(id, GetUserRole());
        if (spread == null)
        {
            return NotFound();
        }

        return Ok(spread);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(SpreadDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SpreadDto>> Create([FromBody] CreateSpreadDto dto)
    {
        var spread = await _spreadService.CreateAsync(dto, GetUserRole());
        return CreatedAtAction(nameof(GetById), new { id = spread.Id }, spread);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(SpreadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SpreadDto>> Update(int id, [FromBody] UpdateSpreadDto dto)
    {
        var spread = await _spreadService.UpdateAsync(id, dto, GetUserRole());
        return Ok(spread);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(int id)
    {
        await _spreadService.DeleteAsync(id, GetUserRole());
        return NoContent();
    }

    [HttpPost("{id}/cards")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(SpreadCardDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SpreadCardDto>> AddCard(int id, [FromBody] AddCardToSpreadDto dto)
    {
        var spreadCard = await _spreadService.AddCardToSpreadAsync(id, dto, GetUserRole());
        return CreatedAtAction(nameof(GetById), new { id = id }, spreadCard);
    }

    [HttpDelete("{spreadId}/cards/{spreadCardId}")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RemoveCard(int spreadId, int spreadCardId)
    {
        await _spreadService.RemoveCardFromSpreadAsync(spreadId, spreadCardId, GetUserRole());
        return NoContent();
    }
}
