using Microsoft.AspNetCore.Mvc;
using Moq;
using NppApi.ComponentTests.TestHelpers;
using NppApi.Controllers;
using NppCore.Models;
using NppCore.Services.Features.Match;
using NUnit.Framework;

namespace NppApi.ComponentTests.Controllers;

[TestFixture]
public class PlayerMatchesControllerTests
{
    private Mock<IPlayerMatchesService> _matchesServiceMock = null!;
    private PlayerMatchesController _controller = null!;

    [SetUp]
    public void SetUp()
    {
        _matchesServiceMock = new Mock<IPlayerMatchesService>();
        _controller = new PlayerMatchesController(_matchesServiceMock.Object);
    }

    [Test]
    public async Task GetPlayerMatchesByYear_WhenYearIsInvalid_ReturnsBadRequest()
    {
        ControllerTestContextFactory.SetUser(_controller, Guid.NewGuid());

        var result = await _controller.GetPlayerMatchesByYearAsync("26", 1, 5);

        Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
        var badRequest = (BadRequestObjectResult)result.Result!;
        Assert.That(badRequest.Value, Is.EqualTo("Year must be in YYYY format."));
    }

    [Test]
    public async Task GetPlayerMatchesByYear_WhenUserClaimIsMissing_ReturnsUnauthorized()
    {
        ControllerTestContextFactory.SetUser(_controller);

        var result = await _controller.GetPlayerMatchesByYearAsync("2026", 1, 5);

        Assert.That(result.Result, Is.TypeOf<UnauthorizedObjectResult>());
        var unauthorized = (UnauthorizedObjectResult)result.Result!;
        Assert.That(unauthorized.Value, Is.EqualTo("Could not identify user from token."));
    }

    [Test]
    public async Task GetPlayerMatchesByYear_WhenUserClaimIsInvalidGuid_ReturnsUnauthorized()
    {
        ControllerTestContextFactory.SetUser(_controller, rawPlayerId: "not-a-guid");

        var result = await _controller.GetPlayerMatchesByYearAsync("2026", 1, 5);

        Assert.That(result.Result, Is.TypeOf<UnauthorizedObjectResult>());
        var unauthorized = (UnauthorizedObjectResult)result.Result!;
        Assert.That(unauthorized.Value, Is.EqualTo("Invalid token."));
    }

    [Test]
    public async Task GetPlayerMatchesByYear_WhenDataExists_ReturnsMappedResponse()
    {
        var playerId = Guid.NewGuid();
        var opponentId = Guid.NewGuid();
        var matchTime = DateTimeOffset.UtcNow;

        ControllerTestContextFactory.SetUser(_controller, playerId);

        _matchesServiceMock
            .Setup(s => s.GetByYearAsync("2026", playerId, 1, 5))
            .ReturnsAsync(new List<PlayerMatches>
            {
                new()
                {
                    PlayerId = playerId,
                    Year = "2026",
                    Match_time = matchTime,
                    MatchId = Guid.NewGuid(),
                    OpponentId = opponentId,
                    OpponentUsername = "opponent_1",
                    Score = "5:3",
                    Result = "WIN"
                }
            });

        var result = await _controller.GetPlayerMatchesByYearAsync("2026", 1, 5);

        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var ok = (OkObjectResult)result.Result!;
        var response = ok.Value as IEnumerable<PlayerMatchesResponse>;

        Assert.That(response, Is.Not.Null);
        var row = response!.Single();

        Assert.Multiple(() =>
        {
            Assert.That(row.PlayerId, Is.EqualTo(playerId));
            Assert.That(row.OpponentUsername, Is.EqualTo("opponent_1"));
            Assert.That(row.Result, Is.EqualTo("WIN"));
            Assert.That(row.Score, Is.EqualTo("5:3"));
            Assert.That(row.Match_time, Is.EqualTo(matchTime));
        });
    }

    [Test]
    public async Task GetHistory_WhenLimitOver100_ClampsLimitTo100()
    {
        ControllerTestContextFactory.SetUser(_controller, Guid.NewGuid());

        _matchesServiceMock
            .Setup(s => s.GetHistoryAsync(It.IsAny<string>(), 1, 100))
            .ReturnsAsync(new List<MatchHistory>());

        var result = await _controller.GetHistoryAsync(page: 1, limit: 1000);

        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        _matchesServiceMock.Verify(s => s.GetHistoryAsync(It.IsAny<string>(), 1, 100), Times.Once);
    }

    [Test]
    public async Task GetHistory_WhenLimitWithinRange_UsesProvidedLimit()
    {
        ControllerTestContextFactory.SetUser(_controller, Guid.NewGuid());

        _matchesServiceMock
            .Setup(s => s.GetHistoryAsync(It.IsAny<string>(), 2, 25))
            .ReturnsAsync(new List<MatchHistory>());

        var result = await _controller.GetHistoryAsync(page: 2, limit: 25);

        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        _matchesServiceMock.Verify(s => s.GetHistoryAsync(It.IsAny<string>(), 2, 25), Times.Once);
    }

    [Test]
    public async Task GetHistory_WhenDataExists_ReturnsMappedResponse()
    {
        ControllerTestContextFactory.SetUser(_controller, Guid.NewGuid());

        _matchesServiceMock
            .Setup(s => s.GetHistoryAsync(It.IsAny<string>(), 1, 10))
            .ReturnsAsync(new List<MatchHistory>
            {
                new()
                {
                    player1Username = "player_1",
                    player2Username = "player_2",
                    Score = "5:4",
                    Result = "player_1"
                }
            });

        var result = await _controller.GetHistoryAsync(page: 1, limit: 10);

        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var ok = (OkObjectResult)result.Result!;
        var response = ok.Value as IEnumerable<MatchHistoryResponse>;

        Assert.That(response, Is.Not.Null);
        var row = response!.Single();

        Assert.Multiple(() =>
        {
            Assert.That(row.P1Username, Is.EqualTo("player_1"));
            Assert.That(row.P2Username, Is.EqualTo("player_2"));
            Assert.That(row.Score, Is.EqualTo("5:4"));
            Assert.That(row.Result, Is.EqualTo("player_1"));
        });
    }
}
