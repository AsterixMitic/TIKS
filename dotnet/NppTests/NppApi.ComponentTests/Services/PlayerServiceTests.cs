using Moq;
using NppCore.Models;
using NppCore.Services.Features.Player;
using NppCore.Services.Persistence.Cassandra;
using NUnit.Framework;

namespace NppApi.ComponentTests.Services;

[TestFixture]
public class PlayerServiceTests
{
    private Mock<ICassandraService> _cassandraMock = null!;
    private PlayerService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _cassandraMock = new Mock<ICassandraService>();
        _service = new PlayerService(_cassandraMock.Object);
    }

    [Test]
    public async Task CreateAsync_ReturnsNewPlayerWithGeneratedId()
    {
        var result = await _service.CreateAsync("testuser", "test@example.com");

        Assert.Multiple(() =>
        {
            Assert.That(result.PlayerId, Is.Not.EqualTo(Guid.Empty));
            Assert.That(result.Username, Is.EqualTo("testuser"));
            Assert.That(result.Email, Is.EqualTo("test@example.com"));
            Assert.That(result.CreatedAt, Is.EqualTo(DateTimeOffset.UtcNow).Within(TimeSpan.FromSeconds(5)));
        });

        _cassandraMock.Verify(c => c.ExecuteAsync(
            It.Is<string>(s => s.Contains("INSERT INTO players")),
            It.IsAny<object[]>()), Times.Once);
    }

    [Test]
    public async Task GetByIdAsync_ExistingPlayer_ReturnsPlayer()
    {
        var playerId = Guid.NewGuid();
        var player = new PlayerEntity { PlayerId = playerId, Username = "found" };

        _cassandraMock.Setup(c => c.QueryFirstOrDefaultAsync<PlayerEntity>(
            It.IsAny<string>(), It.IsAny<object[]>()))
            .ReturnsAsync(player);

        var result = await _service.GetByIdAsync(playerId);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Username, Is.EqualTo("found"));
    }

    [Test]
    public async Task GetByIdAsync_NonExistent_ReturnsNull()
    {
        _cassandraMock.Setup(c => c.QueryFirstOrDefaultAsync<PlayerEntity>(
            It.IsAny<string>(), It.IsAny<object[]>()))
            .ReturnsAsync((PlayerEntity?)null);

        var result = await _service.GetByIdAsync(Guid.NewGuid());

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetByEmailAsync_Existing_ReturnsPlayer()
    {
        var playerId = Guid.NewGuid();
        _cassandraMock.Setup(c => c.QueryFirstOrDefaultAsync<PlayerByEmail>(
            It.IsAny<string>(), It.IsAny<object[]>()))
            .ReturnsAsync(new PlayerByEmail { Email = "test@example.com", PlayerId = playerId });

        _cassandraMock.Setup(c => c.QueryFirstOrDefaultAsync<PlayerEntity>(
            It.IsAny<string>(), It.IsAny<object[]>()))
            .ReturnsAsync(new PlayerEntity { PlayerId = playerId, Username = "testuser", Email = "test@example.com" });

        var result = await _service.GetByEmailAsync("test@example.com");

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.PlayerId, Is.EqualTo(playerId));
    }

    [Test]
    public async Task GetByEmailAsync_NonExistent_ReturnsNull()
    {
        _cassandraMock.Setup(c => c.QueryFirstOrDefaultAsync<PlayerByEmail>(
            It.IsAny<string>(), It.IsAny<object[]>()))
            .ReturnsAsync((PlayerByEmail?)null);

        var result = await _service.GetByEmailAsync("missing@example.com");

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetByUsernameAsync_Existing_ReturnsPlayer()
    {
        var playerId = Guid.NewGuid();
        _cassandraMock.Setup(c => c.QueryFirstOrDefaultAsync<PlayerByUsername>(
            It.IsAny<string>(), It.IsAny<object[]>()))
            .ReturnsAsync(new PlayerByUsername { Username = "testuser", PlayerId = playerId });

        _cassandraMock.Setup(c => c.QueryFirstOrDefaultAsync<PlayerEntity>(
            It.IsAny<string>(), It.IsAny<object[]>()))
            .ReturnsAsync(new PlayerEntity { PlayerId = playerId, Username = "testuser" });

        var result = await _service.GetByUsernameAsync("testuser");

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.PlayerId, Is.EqualTo(playerId));
    }

    [Test]
    public async Task GetByUsernameAsync_NonExistent_ReturnsNull()
    {
        _cassandraMock.Setup(c => c.QueryFirstOrDefaultAsync<PlayerByUsername>(
            It.IsAny<string>(), It.IsAny<object[]>()))
            .ReturnsAsync((PlayerByUsername?)null);

        var result = await _service.GetByUsernameAsync("missing");

        Assert.That(result, Is.Null);
    }
}
