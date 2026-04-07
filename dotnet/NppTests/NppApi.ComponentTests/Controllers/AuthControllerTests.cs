using Microsoft.AspNetCore.Mvc;
using Moq;
using NppApi.Controllers;
using NppCore.Models;
using NppCore.Services.Features.Auth;
using NUnit.Framework;

namespace NppApi.ComponentTests.Controllers;

[TestFixture]
public class AuthControllerTests
{
    private Mock<IAuthService> _authServiceMock = null!;
    private AuthController _controller = null!;

    [SetUp]
    public void SetUp()
    {
        _authServiceMock = new Mock<IAuthService>();
        _controller = new AuthController(_authServiceMock.Object);
    }

    [Test]
    public async Task Register_WhenRequestIsValid_ReturnsOkWithRegisterResponse()
    {
        var request = new RegisterRequest("tester_1", "tester_1@example.com", "pass123");
        var playerId = Guid.NewGuid();
        var player = new PlayerEntity
        {
            PlayerId = playerId,
            Username = request.Username,
            Email = request.Email,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _authServiceMock
            .Setup(s => s.RegisterAsync(request.Username, request.Email, request.Password))
            .ReturnsAsync((player, "jwt-token"));

        var result = await _controller.Register(request);

        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var ok = (OkObjectResult)result.Result!;

        Assert.That(ok.Value, Is.TypeOf<RegisterResponse>());
        var response = (RegisterResponse)ok.Value!;

        Assert.Multiple(() =>
        {
            Assert.That(response.PlayerId, Is.EqualTo(playerId));
            Assert.That(response.Username, Is.EqualTo(request.Username));
            Assert.That(response.Email, Is.EqualTo(request.Email));
            Assert.That(response.Token, Is.EqualTo("jwt-token"));
        });
    }

    [Test]
    public void Register_WhenServiceThrows_PropagatesException()
    {
        var request = new RegisterRequest("tester_1", "tester_1@example.com", "pass123");

        _authServiceMock
            .Setup(s => s.RegisterAsync(request.Username, request.Email, request.Password))
            .ThrowsAsync(new InvalidOperationException("A user with this username already exists"));

        Assert.ThrowsAsync<InvalidOperationException>(() => _controller.Register(request));
    }

    [Test]
    public async Task Login_WhenCredentialsAreValid_ReturnsOkWithLoginResponse()
    {
        var request = new LoginRequest("tester_1@example.com", "pass123");
        var playerId = Guid.NewGuid();
        var player = new PlayerEntity
        {
            PlayerId = playerId,
            Username = "tester_1",
            Email = request.Email,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _authServiceMock
            .Setup(s => s.LoginAsync(request.Email, request.Password))
            .ReturnsAsync(((PlayerEntity Player, string Token)?)(player, "jwt-token"));

        var result = await _controller.Login(request);

        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var ok = (OkObjectResult)result.Result!;
        var response = ok.Value as LoginResponse;

        Assert.That(response, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(response!.PlayerId, Is.EqualTo(playerId));
            Assert.That(response.Username, Is.EqualTo("tester_1"));
            Assert.That(response.Email, Is.EqualTo(request.Email));
            Assert.That(response.Token, Is.EqualTo("jwt-token"));
        });
    }

    [Test]
    public async Task Login_WhenCredentialsAreInvalid_ReturnsUnauthorized()
    {
        var request = new LoginRequest("tester_1@example.com", "wrong");

        _authServiceMock
            .Setup(s => s.LoginAsync(request.Email, request.Password))
            .ReturnsAsync(((PlayerEntity Player, string Token)?)null);

        var result = await _controller.Login(request);

        Assert.That(result.Result, Is.TypeOf<UnauthorizedObjectResult>());
        var unauthorized = (UnauthorizedObjectResult)result.Result!;
        Assert.That(unauthorized.Value, Is.EqualTo("Invalid email or password"));
    }

    [Test]
    public async Task Register_WhenServiceSucceeds_CallsServiceWithCorrectArguments()
    {
        var request = new RegisterRequest("tester_2", "tester_2@example.com", "pass456");
        var playerId = Guid.NewGuid();
        var player = new PlayerEntity { PlayerId = playerId, Username = request.Username, Email = request.Email, CreatedAt = DateTimeOffset.UtcNow };

        _authServiceMock
            .Setup(s => s.RegisterAsync(request.Username, request.Email, request.Password))
            .ReturnsAsync((player, "token-xyz"));

        await _controller.Register(request);

        _authServiceMock.Verify(s => s.RegisterAsync("tester_2", "tester_2@example.com", "pass456"), Times.Once);
    }

    [Test]
    public async Task Login_WhenServiceSucceeds_CallsServiceWithCorrectArguments()
    {
        var request = new LoginRequest("tester_1@example.com", "pass123");
        var playerId = Guid.NewGuid();
        var player = new PlayerEntity { PlayerId = playerId, Username = "tester_1", Email = request.Email, CreatedAt = DateTimeOffset.UtcNow };

        _authServiceMock
            .Setup(s => s.LoginAsync(request.Email, request.Password))
            .ReturnsAsync(((PlayerEntity Player, string Token)?)(player, "jwt-token"));

        await _controller.Login(request);

        _authServiceMock.Verify(s => s.LoginAsync("tester_1@example.com", "pass123"), Times.Once);
    }
}
