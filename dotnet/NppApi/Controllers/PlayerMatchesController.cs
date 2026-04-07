using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NppCore.Models;
using NppCore.Services.Features.Match;
using System.Security.Claims;

namespace NppApi.Controllers;


[ApiController]
[Route("api/[controller]")]
public class PlayerMatchesController : ControllerBase
{
    private readonly IPlayerMatchesService _playerMatchesService;

    public PlayerMatchesController(IPlayerMatchesService matchService)
    {
        _playerMatchesService = matchService;
    }

    [Authorize]
    [HttpGet("{year}")]
    public async Task<ActionResult<IEnumerable<PlayerMatchesResponse>>> GetPlayerMatchesByYearAsync([FromRoute] string year,[FromQuery] int page=1,[FromQuery] int limit=5)
    {
        if (string.IsNullOrEmpty(year) || year.Length != 4)
        {
            return BadRequest("Year must be in YYYY format.");
        }

        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdString))
        {
            return Unauthorized("Could not identify user from token.");
        }

        if (!Guid.TryParse(userIdString, out Guid playerId))
        {
            return Unauthorized("Invalid token.");
        }

        var matches= await _playerMatchesService.GetByYearAsync(year,playerId,page,limit);

        var respone= matches.Select(match=>new PlayerMatchesResponse(match.PlayerId, match.OpponentUsername, match.Result, match.Score, match.Match_time));

        return Ok(respone);
    }

    [Authorize]
    [HttpGet("history")]
    public async Task<ActionResult<IEnumerable<MatchHistoryResponse>>> GetHistoryAsync([FromQuery] int page=1,[FromQuery] int limit=10)
    {
        if (limit>100) limit=100;


        string period=DateTimeOffset.UtcNow.ToString("yyyy-MM");
        var matches= await _playerMatchesService.GetHistoryAsync(period,page,limit);
        var respone=matches.Select(match=>new MatchHistoryResponse(match.player1Username,match.player2Username,match.Score,match.Result));

        return Ok(respone);
    }

    [Authorize]
    [HttpDelete("{year}/{matchId:guid}")]
    public async Task<IActionResult> DeleteMatchAsync([FromRoute] string year, [FromRoute] Guid matchId)
    {
        var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString))
            return Unauthorized("Could not identify user from token.");

        if (!Guid.TryParse(userIdString, out Guid playerId))
            return Unauthorized("Invalid token.");

        var deleted = await _playerMatchesService.DeleteAsync(playerId, year, matchId);
        if (!deleted)
            return NotFound("Match not found.");

        return NoContent();
    }
}
