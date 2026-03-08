using Application.Features.Admin.Queries.GetSystemStats;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>
/// Admin endpoints for system observability and capacity planning.
/// Thin controller — delegates to MediatR query handlers.
/// </summary>
[ApiController]
[Route("api/admin")]
public class AdminController(ISender sender) : ControllerBase
{
    /// <summary>
    /// System statistics: throughput, symbol counts, DB growth projections.
    /// Used for NFR capacity planning as tracked symbols scale.
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(SystemStatsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var result = await sender.Send(new GetSystemStatsQuery(), ct);
        return Ok(result);
    }
}
