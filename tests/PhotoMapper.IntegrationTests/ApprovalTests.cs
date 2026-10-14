using static PhotoMapper.IntegrationTests.AccountSteps;

namespace PhotoMapper.IntegrationTests;

// New accounts wait for an admin's approval before they can sign in.
public sealed class ApprovalTests(AppHostFixture fixture)
{
    [Fact]
    public async Task AdminsHearAboutNewAccountsAndApprovalLetsThePersonIn()
    {
        string adminEmail = await CreateConfirmedAccountAsync(fixture);
        (int exitCode, string output) = await fixture.MakeAdminAsync(adminEmail);
        Assert.True(exitCode == 0, output);
        using MailpitInbox inbox = fixture.CreateMailpitInbox();

        string newcomer = await CreateConfirmedAccountAsync(fixture);

        string reviewLink = await inbox.WaitForLinkAsync(adminEmail, "New PhotoMapper account waiting for approval", containing: newcomer);
        Assert.Contains($"/admin/users?q={Uri.EscapeDataString(newcomer)}", reviewLink, StringComparison.Ordinal);

        await ApproveAsync(fixture, newcomer);

        string signInLink = await inbox.WaitForLinkAsync(newcomer, "Your PhotoMapper account is approved");
        Assert.EndsWith("/Account/Login", signInLink, StringComparison.Ordinal);
        using BrowserSession browser = fixture.CreateBrowserSession();
        using HttpResponseMessage signedIn = await SignInAsync(browser, newcomer, Password);
        AssertRedirectsTo(signedIn, "/");
    }

    [Fact]
    public async Task WaitingAccountsAreListedForAdmins()
    {
        string waiting = await CreateConfirmedAccountAsync(fixture);
        BrowserSession admin = await fixture.GetAdminSessionAsync();

        string pending = await admin.GetStringAsync($"/admin/users?status=pending&q={Uri.EscapeDataString(waiting)}");
        Assert.Contains(waiting, pending, StringComparison.Ordinal);
        Assert.Contains("Waiting for approval", pending, StringComparison.Ordinal);

        await ApproveAsync(fixture, waiting);

        string afterwards = await admin.GetStringAsync($"/admin/users?status=pending&q={Uri.EscapeDataString(waiting)}");
        // (The page also shows a confirmation message naming the account, so check the list itself is empty.)
        Assert.Contains("No users found.", afterwards, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AWaitingAccountGivesNothingAwayToWrongPasswords()
    {
        string email = await CreateConfirmedAccountAsync(fixture);
        using BrowserSession browser = fixture.CreateBrowserSession();

        // A wrong password gets the usual message, not "waiting for approval", and counts towards lockout.
        for (int attempt = 1; attempt <= 4; attempt++)
        {
            using HttpResponseMessage failed = await SignInAsync(browser, email, "Wrong-Passw0rd!");
            string page = await ReadAsync(failed);
            Assert.Contains("Incorrect email or password", page, StringComparison.Ordinal);
            Assert.DoesNotContain("waiting for an admin", page, StringComparison.Ordinal);
        }

        using HttpResponseMessage fifth = await SignInAsync(browser, email, "Wrong-Passw0rd!");
        using HttpResponseMessage correctPassword = await SignInAsync(browser, email, Password);
        AssertRedirectsTo(correctPassword, "/Account/Lockout");
    }
}
