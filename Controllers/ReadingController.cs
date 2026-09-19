using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using test_project.Models.DTO;
using test_project.Services.Interfaces;

namespace test_project.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ReadingController : ControllerBase
{
    private readonly IReadingService _readingService;

    public ReadingController(IReadingService readingService)
    {
        _readingService = readingService;
    }

    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PagedResponseDto<ReadingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponseDto<ReadingDto>>> GetPaged([FromQuery] ReadingQueryDto query)
    {
        return Ok(await _readingService.GetPagedAsync(query));
    }

    [HttpGet("{id:long}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ReadingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReadingDto>> GetById(long id)
    {
        var reading = await _readingService.GetByIdAsync(id);
        return reading == null ? NotFound() : Ok(reading);
    }

    [HttpGet("~/api/users/{userId:int}/readings")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<ReadingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ReadingDto>>> GetByUser(int userId)
    {
        return Ok(await _readingService.GetByUserIdAsync(userId));
    }

    [HttpPost]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ReadingDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<ReadingDto>> Create([FromBody] CreateReadingDto dto)
    {
        var created = await _readingService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:long}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ReadingDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ReadingDto>> Update(long id, [FromBody] UpdateReadingDto dto)
    {
        return Ok(await _readingService.UpdateAsync(id, dto));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(long id)
    {
        await _readingService.DeleteAsync(id);
        return NoContent();
    }
}
