using System.Text.RegularExpressions;

using static PhotoMapper.IntegrationTests.AccountSteps;

namespace PhotoMapper.IntegrationTests;

// The make-admin command and the admin user management page (/admin/users).
public sealed class AdminTests(AppHostFixture fixture)
{
    [Fact]
    public async Task MakeAdminRefusesUnknownAndUnconfirmedAccounts()
    {
        (int unknownExit, string unknownOutput) = await fixture.MakeAdminAsync(NewEmail());
        Assert.Equal(1, unknownExit);
        Assert.Contains("Sign up on the web app first", unknownOutput, StringComparison.Ordinal);

        string unconfirmed = NewEmail();
        using BrowserSession browser = fixture.CreateBrowserSession();
        using HttpResponseMessage registered = await RegisterAsync(browser, unconfirmed);
        (int unconfirmedExit, string unconfirmedOutput) = await fixture.MakeAdminAsync(unconfirmed);
        Assert.Equal(1, unconfirmedExit);
        Assert.Contains("hasn't confirmed its email address", unconfirmedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UserManagementIsForAdminsOnly()
    {
        using BrowserSession anonymous = fixture.CreateBrowserSession();
        using HttpResponseMessage signedOut = await anonymous.GetAsync("/admin/users");
        AssertRedirectsTo(signedOut, "/Account/Login");

        (BrowserSession user, _) = await SignedInAsync(fixture);
        using BrowserSession session = user;
        using HttpResponseMessage notAdmin = await user.GetAsync("/admin/users");
        AssertRedirectsTo(notAdmin, "/Account/AccessDenied");

        // The actions are protected too, not just the page.
        string targetId = await UserIdAsync(await CreateConfirmedAccountAsync(fixture));
        using HttpResponseMessage lockAttempt = await user.SubmitFormAsync("/Account/Manage", handler: null,
            new Dictionary<string, string>(), action: $"/admin/users/{targetId}/lock");
        AssertRedirectsTo(lockAttempt, "/Account/AccessDenied");
    }

    [Fact]
    public async Task AdminCanFindUsersBySearchingTheirEmail()
    {
        using BrowserSession admin = await SignedInAdminAsync();
        string target = await CreateConfirmedAccountAsync(fixture);

        string page = await admin.GetStringAsync($"/admin/users?q={Uri.EscapeDataString(target[..12])}");

        Assert.Contains(target, page, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(page, "<tr>").Skip(1)); // one data row after the header
    }

    [Fact]
    public async Task AdminCanLockAndUnlockAnAccount()
    {
        using BrowserSession admin = await SignedInAdminAsync();
        string target = await CreateConfirmedAccountAsync(fixture);
        string targetId = await UserIdAsync(target);

        using HttpResponseMessage locked = await PostActionAsync(admin, targetId, "lock");
        AssertRedirectsTo(locked, "/admin/users");
        Assert.Contains($"{target} is locked", await admin.GetStringAsync("/admin/users"), StringComparison.Ordinal);

        using (BrowserSession lockedOut = fixture.CreateBrowserSession())
        {
            using HttpResponseMessage signIn = await SignInAsync(lockedOut, target, Password);
            AssertRedirectsTo(signIn, "/Account/Lockout");
        }

        using HttpResponseMessage unlocked = await PostActionAsync(admin, targetId, "unlock");
        AssertRedirectsTo(unlocked, "/admin/users");

        using BrowserSession again = fixture.CreateBrowserSession();
        using HttpResponseMessage signedIn = await SignInAsync(again, target, Password);
        AssertRedirectsTo(signedIn, "/");
    }

    [Fact]
    public async Task AdminCanGrantAndRemoveAdmin()
    {
        using BrowserSession admin = await SignedInAdminAsync();
        string target = await CreateConfirmedAccountAsync(fixture);
        string targetId = await UserIdAsync(target);

        using HttpResponseMessage granted = await PostActionAsync(admin, targetId, "grant-admin");
        AssertRedirectsTo(granted, "/admin/users");
        using (BrowserSession newAdmin = await SignInSessionAsync(target))
        {
            using HttpResponseMessage page = await newAdmin.GetAsync("/admin/users");
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        }

        using HttpResponseMessage removed = await PostActionAsync(admin, targetId, "remove-admin");
        AssertRedirectsTo(removed, "/admin/users");
        using BrowserSession formerAdmin = await SignInSessionAsync(target);
        using HttpResponseMessage denied = await formerAdmin.GetAsync("/admin/users");
        AssertRedirectsTo(denied, "/Account/AccessDenied");
    }

    [Fact]
    public async Task AdminCanDeleteAnAccountAfterConfirming()
    {
        using BrowserSession admin = await SignedInAdminAsync();
        string target = await CreateConfirmedAccountAsync(fixture);
        string targetId = await UserIdAsync(target);

        string confirmPage = await admin.GetStringAsync($"/admin/users/{targetId}/delete");
        Assert.Contains($"This permanently deletes <strong>{target}</strong>", confirmPage, StringComparison.Ordinal);

        using HttpResponseMessage deleted = await admin.SubmitFormAsync($"/admin/users/{targetId}/delete", handler: null,
            new Dictionary<string, string> { ["returnUrl"] = "/admin/users" }, action: $"/admin/users/{targetId}/delete");
        AssertRedirectsTo(deleted, "/admin/users");
        Assert.Contains($"{target} has been deleted", await admin.GetStringAsync("/admin/users"), StringComparison.Ordinal);

        using BrowserSession gone = fixture.CreateBrowserSession();
        using HttpResponseMessage signIn = await SignInAsync(gone, target, Password);
        Assert.Contains("Invalid login attempt", await ReadAsync(signIn), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AdminCannotChangeTheirOwnAccount()
    {
        string email = await CreateConfirmedAccountAsync(fixture);
        await GrantAdminAsync(email);
        using BrowserSession admin = await SignInSessionAsync(email);
        string ownId = await UserIdAsync(email, admin);

        string page = await admin.GetStringAsync($"/admin/users?q={Uri.EscapeDataString(email)}");
        Assert.Contains("This is you", page, StringComparison.Ordinal);
        Assert.DoesNotContain($"admin/users/{ownId}/lock", page, StringComparison.Ordinal);

        // Even when posted directly, the action is refused.
        using HttpResponseMessage lockSelf = await PostActionAsync(admin, ownId, "lock");
        AssertRedirectsTo(lockSelf, "/admin/users");
        Assert.Contains("You can&#x27;t change your own account here", await admin.GetStringAsync("/admin/users"), StringComparison.Ordinal);
        using HttpResponseMessage stillAdmin = await admin.GetAsync("/admin/users");
        Assert.Equal(HttpStatusCode.OK, stillAdmin.StatusCode);
    }

    [Fact]
    public async Task ActionsOnlyRedirectBackToUserManagement()
    {
        using BrowserSession admin = await SignedInAdminAsync();
        string targetId = await UserIdAsync(await CreateConfirmedAccountAsync(fixture));

        using HttpResponseMessage response = await PostActionAsync(admin, targetId, "unlock", returnUrl: "https://evil.example.com/");

        AssertRedirectsTo(response, "/admin/users");
        Assert.Equal("/admin/users", response.Headers.Location?.OriginalString);
    }

    private async Task GrantAdminAsync(string email)
    {
        (int exitCode, string output) = await fixture.MakeAdminAsync(email);
        Assert.True(exitCode == 0, output);
        Assert.Contains($"{email} is now an admin", output, StringComparison.Ordinal);
    }

    private async Task<BrowserSession> SignInSessionAsync(string email)
    {
        BrowserSession browser = fixture.CreateBrowserSession();
        using HttpResponseMessage signedIn = await SignInAsync(browser, email, Password);
        AssertRedirectsTo(signedIn, "/");
        return browser;
    }

    // A new account made admin with the setup command, then signed in so its cookie carries the role.
    private async Task<BrowserSession> SignedInAdminAsync()
    {
        string email = await CreateConfirmedAccountAsync(fixture);
        await GrantAdminAsync(email);
        return await SignInSessionAsync(email);
    }

    // Reads a user's id from the user management page (as an admin would see it).
    private async Task<string> UserIdAsync(string email, BrowserSession? admin = null)
    {
        BrowserSession? owned = admin is null ? await SignedInAdminAsync() : null;
        try
        {
            string page = await (admin ?? owned!).GetStringAsync($"/admin/users?q={Uri.EscapeDataString(email)}");
            Match id = Regex.Match(page, "admin/users/([0-9a-f-]{36})/");
            if (!id.Success)
            {
                // Your own row has no action links; use the account's personal data instead.
                return await OwnIdAsync(admin!);
            }

            return id.Groups[1].Value;
        }
        finally
        {
            owned?.Dispose();
        }
    }

    private static async Task<string> OwnIdAsync(BrowserSession browser)
    {
        using HttpResponseMessage download = await browser.SubmitFormAsync("/Account/Manage/PersonalData", handler: null,
            new Dictionary<string, string>(), action: "/Account/Manage/DownloadPersonalData");
        Match id = Regex.Match(await ReadAsync(download), "\"Id\":\"([^\"]+)\"");
        Assert.True(id.Success, "No Id in the personal data download.");
        return id.Groups[1].Value;
    }

    private static Task<HttpResponseMessage> PostActionAsync(BrowserSession admin, string userId, string action, string returnUrl = "/admin/users") =>
        admin.SubmitFormAsync("/admin/users", handler: null,
            new Dictionary<string, string> { ["returnUrl"] = returnUrl }, action: $"/admin/users/{userId}/{action}");
}
