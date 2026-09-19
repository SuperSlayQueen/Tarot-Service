using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using test_project.Services.Interfaces;

namespace test_project.Controllers;

[ApiController]
[Route("api/partitions")]
[Produces("application/json")]
public class PartitionController : ControllerBase
{
    private readonly IPartitionService _partitionService;
    private readonly IAlertService _alertService;

    public PartitionController(IPartitionService partitionService, IAlertService alertService)
    {
        _partitionService = partitionService;
        _alertService = alertService;
    }

    [HttpPost("ensure")]
    [AllowAnonymous]
    public async Task<IActionResult> Ensure(CancellationToken ct)
    {
        var result = await _partitionService.EnsureFuturePartitionsAsync(ct);
        return Ok(result);
    }

    [HttpGet("health")]
    [AllowAnonymous]
    public async Task<IActionResult> Health(CancellationToken ct)
    {
        var health = await _partitionService.CheckHealthAsync(ct);
        if (!health.IsHealthy)
        {
            await _alertService.SendAlertAsync(
                "Partition health CRITICAL",
                $"Missing: {string.Join(", ", health.Missing)}",
                ct);
        }
        else
        {
            await _alertService.SendRecoveryAsync(
                "Partition health OK",
                "All required partitions exist.",
                ct);
        }

        return Ok(health);
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        return Ok(await _partitionService.GetExistingPartitionsAsync(ct));
    }
}
