using System.Text.RegularExpressions;
using Microsoft.Playwright;
using NUnit.Framework;

namespace Npp.PlaywrightTests.E2E;

[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class AuthAndRoutingE2ETests : E2ETestBase
{
    [Test]
    public async Task RootRoute_WhenAnonymous_ShouldRedirectToLogin()
    {
        await Page.GotoAsync($"{TestSettings.FrontendUrl}/");

        await Expect(Page).ToHaveURLAsync(new Regex(".*/login$"));
    }

    [Test]
    public async Task LoginPage_ShouldRenderMainFields()
    {
        await Page.GotoAsync($"{TestSettings.FrontendUrl}/login");

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Login" })).ToBeVisibleAsync();
        await Expect(Page.Locator("#email")).ToBeVisibleAsync();
        await Expect(Page.Locator("#password")).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Login" })).ToBeVisibleAsync();
    }

    [Test]
    public async Task LoginPage_RegisterLink_ShouldNavigateToRegister()
    {
        await Page.GotoAsync($"{TestSettings.FrontendUrl}/login");

        await Page.GetByRole(AriaRole.Link, new() { Name = "Register" }).ClickAsync();

        await Expect(Page).ToHaveURLAsync(new Regex(".*/register$"));
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Register" })).ToBeVisibleAsync();
    }

    [Test]
    public async Task RegisterPage_LoginLink_ShouldNavigateToLogin()
    {
        await Page.GotoAsync($"{TestSettings.FrontendUrl}/register");

        await Page.GetByRole(AriaRole.Link, new() { Name = "Login" }).ClickAsync();

        await Expect(Page).ToHaveURLAsync(new Regex(".*/login$"));
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Login" })).ToBeVisibleAsync();
    }

    [Test]
    public async Task LeaderboardRoute_WhenAnonymous_ShouldRedirectToLogin()
    {
        await Page.GotoAsync($"{TestSettings.FrontendUrl}/leaderboard");

        await Expect(Page).ToHaveURLAsync(new Regex(".*/login$"));
    }

    [Test]
    [Retry(1)]
    public async Task RegisterFlow_ShouldNavigateToLobby()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"e2e_{suffix}";
        var email = $"{username}@example.com";

        await Page.GotoAsync($"{TestSettings.FrontendUrl}/register");

        await Page.FillAsync("#username", username);
        await Page.FillAsync("#email", email);
        await Page.FillAsync("#password", "pass1234");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Register" }).ClickAsync();

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "NPP Ping Pong" }))
            .ToBeVisibleAsync(new() { Timeout = 20_000 });
    }

    [Test]
    [Retry(1)]
    public async Task Register_ThenOpenLeaderboard_ThenBackToGame_ShouldKeepAuthenticatedFlow()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"e2e_lb_{suffix}";
        var email = $"{username}@example.com";

        await Page.GotoAsync($"{TestSettings.FrontendUrl}/register");

        await Page.FillAsync("#username", username);
        await Page.FillAsync("#email", email);
        await Page.FillAsync("#password", "pass1234");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Register" }).ClickAsync();

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "NPP Ping Pong" }))
            .ToBeVisibleAsync(new() { Timeout = 20_000 });

        await Page.Locator("a.leaderboard-link").ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex(".*/leaderboard$"));

        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Global Rankings" })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Most Wins" })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Longest Streaks" })).ToBeVisibleAsync();

        await Page.Locator("a.back-button").ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex(".*/$"));
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "NPP Ping Pong" }))
            .ToBeVisibleAsync(new() { Timeout = 20_000 });
    }

    [Test]
    public async Task Login_WithWrongCredentials_ShouldShowError()
    {
        await Page.GotoAsync($"{TestSettings.FrontendUrl}/login");

        await Page.FillAsync("#email", $"missing_{Guid.NewGuid():N}@example.com");
        await Page.FillAsync("#password", "wrong-pass");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Login" }).ClickAsync();

        await Expect(Page.Locator(".auth-error")).ToBeVisibleAsync();
        await Expect(Page.Locator(".auth-error")).ToContainTextAsync("Invalid email or password");
    }

    [Test]
    public async Task Register_ThenLogout_ShouldReturnToLogin()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"logout_{suffix}";
        var email = $"{username}@example.com";

        await Page.GotoAsync($"{TestSettings.FrontendUrl}/register");

        await Page.FillAsync("#username", username);
        await Page.FillAsync("#email", email);
        await Page.FillAsync("#password", "pass1234");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Register" }).ClickAsync();

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "NPP Ping Pong" }))
            .ToBeVisibleAsync(new() { Timeout = 20_000 });

        await Page.GetByRole(AriaRole.Button, new() { Name = "Logout" }).ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex(".*/login$"));
    }
}
