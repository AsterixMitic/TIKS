using Moq;
using NppCore.Models;
using NppCore.Services.Features.Auth;
using NppCore.Services.Features.Player;
using NppCore.Services.Persistence.Cassandra;
using NUnit.Framework;

namespace NppApi.ComponentTests.Services;

[TestFixture]
public class AuthServiceTests
{
    private Mock<IPlayerService> _playerServiceMock = null!;
    private Mock<ICassandraService> _cassandraMock = null!;
    private Mock<IJwtService> _jwtServiceMock = null!;
    private AuthService _authService = null!;

    [SetUp]
    public void SetUp()
    {
        _playerServiceMock = new Mock<IPlayerService>();
        _cassandraMock = new Mock<ICassandraService>();
        _jwtServiceMock = new Mock<IJwtService>();
        _authService = new AuthService(
            _playerServiceMock.Object,
            _cassandraMock.Object,
            _jwtServiceMock.Object);
    }

    [Test]
    public async Task RegisterAsync_NewUser_ReturnsPlayerAndToken()
    {
        var playerId = Guid.NewGuid();
        var player = new PlayerEntity
        {
            PlayerId = playerId,
            Username = "testuser",
            Email = "test@example.com",
            CreatedAt = DateTimeOffset.UtcNow
        };

        _playerServiceMock.Setup(s => s.GetByUsernameAsync("testuser")).ReturnsAsync((PlayerEntity?)null);
        _playerServiceMock.Setup(s => s.GetByEmailAsync("test@example.com")).ReturnsAsync((PlayerEntity?)null);
        _playerServiceMock.Setup(s => s.CreateAsync("testuser", "test@example.com", null)).ReturnsAsync(player);
        _jwtServiceMock.Setup(s => s.GenerateToken(playerId, "testuser", "test@example.com")).Returns("fake-jwt");

        var (resultPlayer, token) = await _authService.RegisterAsync("testuser", "test@example.com", "pass123");

        Assert.Multiple(() =>
        {
            Assert.That(resultPlayer.PlayerId, Is.EqualTo(playerId));
            Assert.That(token, Is.EqualTo("fake-jwt"));
        });

        _cassandraMock.Verify(c => c.ExecuteAsync(
            It.Is<string>(s => s.Contains("players_by_email")),
            It.IsAny<object[]>()), Times.Once);

        _cassandraMock.Verify(c => c.ExecuteAsync(
            It.Is<string>(s => s.Contains("players_by_username")),
            It.IsAny<object[]>()), Times.Once);
    }

    [Test]
    public void RegisterAsync_DuplicateUsername_ThrowsException()
    {
        var existing = new PlayerEntity { PlayerId = Guid.NewGuid(), Username = "taken" };
        _playerServiceMock.Setup(s => s.GetByUsernameAsync("taken")).ReturnsAsync(existing);

        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.RegisterAsync("taken", "new@example.com", "pass123"));

        Assert.That(ex!.Message, Does.Contain("username"));
    }

    [Test]
    public void RegisterAsync_DuplicateEmail_ThrowsException()
    {
        _playerServiceMock.Setup(s => s.GetByUsernameAsync("newuser")).ReturnsAsync((PlayerEntity?)null);
        var existing = new PlayerEntity { PlayerId = Guid.NewGuid(), Email = "taken@example.com" };
        _playerServiceMock.Setup(s => s.GetByEmailAsync("taken@example.com")).ReturnsAsync(existing);

        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.RegisterAsync("newuser", "taken@example.com", "pass123"));

        Assert.That(ex!.Message, Does.Contain("email"));
    }

    [Test]
    public async Task LoginAsync_ValidCredentials_ReturnsPlayerAndToken()
    {
        var playerId = Guid.NewGuid();
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("pass123");

        _cassandraMock.Setup(c => c.QueryFirstOrDefaultAsync<PlayerByEmail>(
            It.IsAny<string>(), It.IsAny<object[]>()))
            .ReturnsAsync(new PlayerByEmail { Email = "test@example.com", PasswordHash = passwordHash, PlayerId = playerId });

        var player = new PlayerEntity { PlayerId = playerId, Username = "testuser", Email = "test@example.com" };
        _playerServiceMock.Setup(s => s.GetByIdAsync(playerId)).ReturnsAsync(player);
        _jwtServiceMock.Setup(s => s.GenerateToken(playerId, "testuser", "test@example.com")).Returns("jwt-token");

        var result = await _authService.LoginAsync("test@example.com", "pass123");

        Assert.That(result, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(result!.Value.Player.PlayerId, Is.EqualTo(playerId));
            Assert.That(result.Value.Token, Is.EqualTo("jwt-token"));
        });
    }

    [Test]
    public async Task LoginAsync_InvalidEmail_ReturnsNull()
    {
        _cassandraMock.Setup(c => c.QueryFirstOrDefaultAsync<PlayerByEmail>(
            It.IsAny<string>(), It.IsAny<object[]>()))
            .ReturnsAsync((PlayerByEmail?)null);

        var result = await _authService.LoginAsync("nonexistent@example.com", "pass123");

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task LoginAsync_WrongPassword_ReturnsNull()
    {
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("correct-password");

        _cassandraMock.Setup(c => c.QueryFirstOrDefaultAsync<PlayerByEmail>(
            It.IsAny<string>(), It.IsAny<object[]>()))
            .ReturnsAsync(new PlayerByEmail { Email = "test@example.com", PasswordHash = passwordHash, PlayerId = Guid.NewGuid() });

        var result = await _authService.LoginAsync("test@example.com", "wrong-password");

        Assert.That(result, Is.Null);
    }
}
