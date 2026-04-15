using System.Text.RegularExpressions;
using Microsoft.Playwright;
using NUnit.Framework;

namespace Npp.PlaywrightTests.E2E;

[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class ProfileE2ETests : E2ETestBase
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
    public async Task EditProfile_ShouldUpdateDisplayedUsername()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"e2e_edit_{suffix}";
        var email = $"{username}@example.com";
        var newUsername = $"updated_{suffix}";

        await RegisterAndNavigateToLobby(username, email);

        await Page.GetByRole(AriaRole.Button, new() { Name = "Edit Profile" }).ClickAsync();

        await Expect(Page.Locator("#edit-username")).ToBeVisibleAsync();
        await Page.FillAsync("#edit-username", newUsername);
        await Page.Locator("#save-profile-btn").ClickAsync();

        await Expect(Page.Locator(".profile-name")).ToContainTextAsync(newUsername, new() { Timeout = 5_000 });
    }

    [Test]
    [Retry(1)]
    public async Task DeleteAccount_ShouldRedirectToLogin()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"e2e_del_{suffix}";
        var email = $"{username}@example.com";

        await RegisterAndNavigateToLobby(username, email);

        await Page.Locator("#delete-account-btn").ClickAsync();

        await Expect(Page.Locator("#confirm-delete-btn")).ToBeVisibleAsync();
        await Page.Locator("#confirm-delete-btn").ClickAsync();

        await Expect(Page).ToHaveURLAsync(new Regex(".*/login$"), new() { Timeout = 10_000 });
    }

    [Test]
    [Retry(1)]
    public async Task EditProfile_WithTakenUsername_ShouldShowError()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];

        // Register user A via API (to occupy their username)
        var playwright = await Playwright.CreateAsync();
        var api = await playwright.APIRequest.NewContextAsync(new APIRequestNewContextOptions
        {
            BaseURL = TestSettings.BackendUrl
        });
        var takenUsername = $"taken_{suffix}";
        await api.PostAsync("/auth/register", new APIRequestContextOptions
        {
            DataObject = new { username = takenUsername, email = $"{takenUsername}@example.com", password = "pass1234" }
        });
        await api.DisposeAsync();
        playwright.Dispose();

        // Register user B via UI
        var usernameB = $"user_b_{suffix}";
        var emailB = $"{usernameB}@example.com";
        await RegisterAndNavigateToLobby(usernameB, emailB);

        // Try to update profile to user A's username
        await Page.GetByRole(AriaRole.Button, new() { Name = "Edit Profile" }).ClickAsync();
        await Expect(Page.Locator("#edit-username")).ToBeVisibleAsync();
        await Page.FillAsync("#edit-username", takenUsername);
        await Page.Locator("#save-profile-btn").ClickAsync();

        await Expect(Page.Locator(".profile-error")).ToBeVisibleAsync(new() { Timeout = 5_000 });
    }
}
