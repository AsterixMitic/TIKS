using System.Text.RegularExpressions;
using Microsoft.Playwright;
using NUnit.Framework;

namespace Npp.PlaywrightTests.E2E;

[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class LeaderboardContentE2ETests : E2ETestBase
{
    private async Task RegisterAndNavigateToLeaderboard(string username, string email, string password = "pass1234")
    {
        await Page.GotoAsync($"{TestSettings.FrontendUrl}/register");
        await Page.FillAsync("#username", username);
        await Page.FillAsync("#email", email);
        await Page.FillAsync("#password", password);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Register" }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "NPP Ping Pong" }))
            .ToBeVisibleAsync(new() { Timeout = 20_000 });
        await Page.Locator("a.leaderboard-link").ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex(".*/leaderboard$"));
    }

    [Test]
    [Retry(1)]
    public async Task Leaderboard_StreakCard_IsVisible()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"e2e_streak_{suffix}";
        var email = $"{username}@example.com";

        await RegisterAndNavigateToLeaderboard(username, email);

        await Expect(Page.Locator(".streak-card")).ToBeVisibleAsync(new() { Timeout = 5_000 });
    }

    [Test]
    [Retry(1)]
    public async Task Leaderboard_GlobalTab_ShowsPeriodSelector()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"e2e_period_{suffix}";
        var email = $"{username}@example.com";

        await RegisterAndNavigateToLeaderboard(username, email);

        // Global Rankings is active by default — period selector must be visible
        await Expect(Page.Locator("#period")).ToBeVisibleAsync(new() { Timeout = 10_000 });
    }

    [Test]
    [Retry(1)]
    public async Task Leaderboard_GlobalTab_LoadsContent()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"e2e_gtab_{suffix}";
        var email = $"{username}@example.com";

        await RegisterAndNavigateToLeaderboard(username, email);

        // Wait for loading to finish — either table or no-data message appears
        await Expect(Page.Locator(".leaderboard-table")).ToBeVisibleAsync(new() { Timeout = 10_000 });
        await Expect(Page.Locator(".loading")).Not.ToBeVisibleAsync();
    }

    [Test]
    [Retry(1)]
    public async Task Leaderboard_MostWinsTab_LoadsContent()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"e2e_wins_{suffix}";
        var email = $"{username}@example.com";

        await RegisterAndNavigateToLeaderboard(username, email);

        await Page.GetByRole(AriaRole.Button, new() { Name = "Most Wins" }).ClickAsync();

        await Expect(Page.Locator(".leaderboard-table")).ToBeVisibleAsync(new() { Timeout = 10_000 });
        await Expect(Page.Locator("#period")).Not.ToBeVisibleAsync();
    }

    [Test]
    [Retry(1)]
    public async Task Leaderboard_LongestStreaksTab_LoadsContent()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"e2e_lstreak_{suffix}";
        var email = $"{username}@example.com";

        await RegisterAndNavigateToLeaderboard(username, email);

        await Page.GetByRole(AriaRole.Button, new() { Name = "Longest Streaks" }).ClickAsync();

        await Expect(Page.Locator(".leaderboard-table")).ToBeVisibleAsync(new() { Timeout = 10_000 });
        await Expect(Page.Locator("#period")).Not.ToBeVisibleAsync();
    }

    [Test]
    [Retry(1)]
    public async Task Leaderboard_PeriodSelector_AllTime_LoadsContent()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"e2e_psel_{suffix}";
        var email = $"{username}@example.com";

        await RegisterAndNavigateToLeaderboard(username, email);

        var select = Page.Locator("#period");
        await Expect(select).ToBeVisibleAsync(new() { Timeout = 10_000 });
        await select.SelectOptionAsync(new SelectOptionValue { Label = "All Time" });

        await Expect(select).ToHaveValueAsync("ALL_TIME|all");
        await Expect(Page.Locator(".leaderboard-table")).ToBeVisibleAsync(new() { Timeout = 10_000 });
    }

    [Test]
    [Retry(1)]
    public async Task Leaderboard_TabSwitch_GlobalToWins_ChangesActiveTab()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"e2e_tabsw_{suffix}";
        var email = $"{username}@example.com";

        await RegisterAndNavigateToLeaderboard(username, email);

        // Global is active by default
        await Expect(Page.Locator(".tab-button.active")).ToContainTextAsync("Global Rankings");

        await Page.GetByRole(AriaRole.Button, new() { Name = "Most Wins" }).ClickAsync();

        await Expect(Page.Locator(".tab-button.active")).ToContainTextAsync("Most Wins");
    }
}
