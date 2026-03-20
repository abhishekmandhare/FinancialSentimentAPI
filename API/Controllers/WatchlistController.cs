using API.Controllers.DTOs;
using Application.Features.Watchlist;
using Application.Features.Watchlist.Commands.AddToWatchlist;
using Application.Features.Watchlist.Commands.RemoveFromWatchlist;
using Application.Features.Watchlist.Queries.GetWatchlist;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/watchlist")]
public class WatchlistController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<WatchlistSymbolDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWatchlist(CancellationToken ct)
    {
        var result = await sender.Send(new GetWatchlistQuery(), ct);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(WatchlistSymbolDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Add([FromBody] AddToWatchlistRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new AddToWatchlistCommand(request.Symbol), ct);
        return CreatedAtAction(nameof(GetWatchlist), result);
    }

    [HttpDelete("{symbol}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remove(string symbol, CancellationToken ct)
    {
        await sender.Send(new RemoveFromWatchlistCommand(symbol), ct);
        return NoContent();
    }
}
