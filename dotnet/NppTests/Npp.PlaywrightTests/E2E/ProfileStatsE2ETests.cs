using Microsoft.Playwright;
using NUnit.Framework;

namespace Npp.PlaywrightTests.E2E;

[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class ProfileStatsE2ETests : E2ETestBase
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
    public async Task ProfileStats_ShowsZeroStats_ForNewUser()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"e2e_stats_{suffix}";
        var email = $"{username}@example.com";

        await RegisterAndNavigateToLobby(username, email);

        var statValues = Page.Locator(".stat-value");
        // Wins and Losses are 0, Win Rate is "0%"
        await Expect(statValues.Nth(0)).ToHaveTextAsync("0", new() { Timeout = 5_000 });
        await Expect(statValues.Nth(1)).ToHaveTextAsync("0");
        await Expect(Page.Locator(".stat-value.highlight")).ToHaveTextAsync("0%");
    }

    [Test]
    [Retry(1)]
    public async Task ProfileStats_ShowsMatchHistorySection()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"e2e_mhsec_{suffix}";
        var email = $"{username}@example.com";

        await RegisterAndNavigateToLobby(username, email);

        await Expect(Page.Locator(".matches-history")).ToBeVisibleAsync(new() { Timeout = 5_000 });
        await Expect(Page.Locator(".matches-history")).ToContainTextAsync("Last 5 Matches");
    }

    [Test]
    [Retry(1)]
    public async Task ProfileStats_EditForm_CancelHidesForm()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"e2e_cancel_{suffix}";
        var email = $"{username}@example.com";

        await RegisterAndNavigateToLobby(username, email);

        await Page.GetByRole(AriaRole.Button, new() { Name = "Edit Profile" }).ClickAsync();
        await Expect(Page.Locator("#edit-username")).ToBeVisibleAsync();

        await Page.Locator("#cancel-edit-btn").ClickAsync();

        await Expect(Page.Locator("#edit-username")).Not.ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Edit Profile" })).ToBeVisibleAsync();
    }

    [Test]
    [Retry(1)]
    public async Task ProfileStats_ProfileWidget_IsVisible()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"e2e_widget_{suffix}";
        var email = $"{username}@example.com";

        await RegisterAndNavigateToLobby(username, email);

        await Expect(Page.Locator(".profile-widget")).ToBeVisibleAsync();
        await Expect(Page.Locator(".profile-avatar")).ToBeVisibleAsync();
        await Expect(Page.Locator(".profile-status")).ToHaveTextAsync("Online");
    }
}
