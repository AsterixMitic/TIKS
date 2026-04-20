using Microsoft.Playwright;
using NUnit.Framework;

namespace Npp.PlaywrightTests.E2E;

[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class LobbyE2ETests : E2ETestBase
{
    private async Task RegisterAndNavigateToLobby(string username, string email, string password = "pass1234")
    {
        await Page.GotoAsync($"{TestSettings.FrontendUrl}/register");
        await Page.FillAsync("#username", username);
        await Page.FillAsync("#email", email);
        await Page.FillAsync("#password", password);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Register" }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "NPP Ping Pong" }))
            .ToBeVisibleAsync(new() { Timeout = 20_000 });
    }

    [Test]
    [Retry(1)]
    public async Task Lobby_ShowsUsername_AfterRegister()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"e2e_lobby_{suffix}";
        var email = $"{username}@example.com";

        await RegisterAndNavigateToLobby(username, email);

        await Expect(Page.Locator(".profile-name")).ToContainTextAsync(username, new() { Timeout = 5_000 });
    }

    [Test]
    [Retry(1)]
    public async Task Lobby_ShowsConnectionStatus()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"e2e_conn_{suffix}";
        var email = $"{username}@example.com";

        await RegisterAndNavigateToLobby(username, email);

        await Expect(Page.Locator(".connection-status")).ToBeVisibleAsync();
    }

    [Test]
    [Retry(1)]
    public async Task Lobby_CreateGameButton_IsVisible()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"e2e_cg_{suffix}";
        var email = $"{username}@example.com";

        await RegisterAndNavigateToLobby(username, email);

        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Create Game" })).ToBeVisibleAsync();
    }

    [Test]
    [Retry(1)]
    public async Task Lobby_OpenGamesSection_IsVisible()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"e2e_og_{suffix}";
        var email = $"{username}@example.com";

        await RegisterAndNavigateToLobby(username, email);

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Open Games" })).ToBeVisibleAsync();
    }

    [Test]
    [Retry(1)]
    public async Task Lobby_OpenGamesSection_ShowsNoGamesMessage_WhenEmpty()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"e2e_nog_{suffix}";
        var email = $"{username}@example.com";

        await RegisterAndNavigateToLobby(username, email);

        await Expect(Page.Locator(".no-games")).ToBeVisibleAsync(new() { Timeout = 5_000 });
        await Expect(Page.Locator(".no-games")).ToContainTextAsync("No open games");
    }
}
