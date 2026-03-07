using API.Controllers.DTOs;
using Application.Features.Symbols;
using Application.Features.Symbols.Commands.AddTrackedSymbol;
using Application.Features.Symbols.Commands.RemoveTrackedSymbol;
using Application.Features.Symbols.Queries.GetTrackedSymbols;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>
/// Admin API for managing tracked symbols at runtime — no restart required.
/// Changes take effect on the ingestion worker's next poll cycle.
/// </summary>
[ApiController]
[Route("api/symbols")]
public class SymbolsController(ISender sender) : ControllerBase
{
    /// <summary>
    /// List all symbols currently being tracked by the ingestion pipeline.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TrackedSymbolDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await sender.Send(new GetTrackedSymbolsQuery(), ct);
        return Ok(result);
    }

    /// <summary>
    /// Add a symbol to the tracked list.
    /// The ingestion worker picks it up on its next poll cycle.
    /// Returns 400 if the symbol is already tracked.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(TrackedSymbolDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Add([FromBody] AddTrackedSymbolRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new AddTrackedSymbolCommand(request.Symbol), ct);
        return CreatedAtAction(nameof(GetAll), result);
    }

    /// <summary>
    /// Remove a symbol from the tracked list.
    /// The ingestion worker stops fetching it on the next poll cycle.
    /// Returns 404 if the symbol is not currently tracked.
    /// </summary>
    [HttpDelete("{symbol}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remove(string symbol, CancellationToken ct)
    {
        await sender.Send(new RemoveTrackedSymbolCommand(symbol), ct);
        return NoContent();
    }
}
