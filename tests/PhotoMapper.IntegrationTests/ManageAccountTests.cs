using static PhotoMapper.IntegrationTests.AccountSteps;

namespace PhotoMapper.IntegrationTests;

// The signed-in account pages under /Account/Manage.
public sealed class ManageAccountTests(AppHostFixture fixture)
{
    [Fact]
    public async Task ChangingPasswordReplacesTheOldOne()
    {
        (BrowserSession browser, string email) = await SignedInAsync(fixture);
        using BrowserSession session = browser;
        const string newPassword = "Chang3d-Secret!pw";

        using HttpResponseMessage wrongCurrent = await browser.SubmitFormAsync("/Account/Manage/ChangePassword", "change-password",
            new Dictionary<string, string>
            {
                ["Input.OldPassword"] = "Not-The-Passw0rd!",
                ["Input.NewPassword"] = newPassword,
                ["Input.ConfirmPassword"] = newPassword,
            });
        Assert.Contains("Incorrect password", await ReadAsync(wrongCurrent), StringComparison.Ordinal);

        using HttpResponseMessage changed = await browser.SubmitFormAsync("/Account/Manage/ChangePassword", "change-password",
            new Dictionary<string, string>
            {
                ["Input.OldPassword"] = Password,
                ["Input.NewPassword"] = newPassword,
                ["Input.ConfirmPassword"] = newPassword,
            });
        AssertRedirectsTo(changed, "/Account/Manage/ChangePassword");

        using BrowserSession other = fixture.CreateBrowserSession();
        using HttpResponseMessage oldPassword = await SignInAsync(other, email, Password);
        Assert.Contains("Invalid login attempt", await ReadAsync(oldPassword), StringComparison.Ordinal);
        using HttpResponseMessage signedIn = await SignInAsync(other, email, newPassword);
        AssertRedirectsTo(signedIn, "/");
    }

    [Fact]
    public async Task NewEmailAddressMustBeConfirmedBeforeItIsUsed()
    {
        (BrowserSession browser, string email) = await SignedInAsync(fixture);
        using BrowserSession session = browser;
        using MailpitInbox inbox = fixture.CreateMailpitInbox();
        string newEmail = NewEmail();

        using HttpResponseMessage requested = await browser.SubmitFormAsync("/Account/Manage/Email", "change-email",
            new Dictionary<string, string> { ["Input.NewEmail"] = newEmail });
        Assert.Contains("Confirmation link to change email sent", await ReadAsync(requested), StringComparison.Ordinal);

        // Until the new address is confirmed, the account still signs in with the old one.
        using BrowserSession other = fixture.CreateBrowserSession();
        using HttpResponseMessage newBeforeConfirming = await SignInAsync(other, newEmail, Password);
        Assert.Contains("Invalid login attempt", await ReadAsync(newBeforeConfirming), StringComparison.Ordinal);

        string link = await inbox.WaitForLinkAsync(newEmail, ConfirmationSubject);
        Assert.Contains("Thank you for confirming your email change", await browser.GetStringAsync(link), StringComparison.Ordinal);

        using HttpResponseMessage oldEmail = await SignInAsync(other, email, Password);
        Assert.Contains("Invalid login attempt", await ReadAsync(oldEmail), StringComparison.Ordinal);
        using HttpResponseMessage signedIn = await SignInAsync(other, newEmail, Password);
        AssertRedirectsTo(signedIn, "/");
    }

    [Fact]
    public async Task PersonalDataDownloadContainsTheAccountEmail()
    {
        (BrowserSession browser, string email) = await SignedInAsync(fixture);
        using BrowserSession session = browser;

        using HttpResponseMessage download = await browser.SubmitFormAsync("/Account/Manage/PersonalData", handler: null,
            new Dictionary<string, string>(), action: "/Account/Manage/DownloadPersonalData");

        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("application/json", download.Content.Headers.ContentType?.MediaType);
        Assert.Contains(email, await ReadAsync(download), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeletingTheAccountNeedsThePasswordAndRemovesIt()
    {
        (BrowserSession browser, string email) = await SignedInAsync(fixture);
        using BrowserSession session = browser;

        using HttpResponseMessage wrongPassword = await browser.SubmitFormAsync("/Account/Manage/DeletePersonalData", "delete-user",
            new Dictionary<string, string> { ["Input.Password"] = "Not-The-Passw0rd!" });
        Assert.Contains("Incorrect password", await ReadAsync(wrongPassword), StringComparison.Ordinal);

        using HttpResponseMessage deleted = await browser.SubmitFormAsync("/Account/Manage/DeletePersonalData", "delete-user",
            new Dictionary<string, string> { ["Input.Password"] = Password });
        // Signed out and sent back to the (now protected) page.
        AssertRedirectsTo(deleted, "/Account/Manage/DeletePersonalData");

        using BrowserSession other = fixture.CreateBrowserSession();
        using HttpResponseMessage signIn = await SignInAsync(other, email, Password);
        Assert.Contains("Invalid login attempt", await ReadAsync(signIn), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SigningOutEndsTheSession()
    {
        (BrowserSession browser, _) = await SignedInAsync(fixture);
        using BrowserSession session = browser;

        using HttpResponseMessage signedOut = await browser.SubmitFormAsync("/Account/Manage", handler: null,
            new Dictionary<string, string> { ["ReturnUrl"] = "" }, action: "/Account/Logout");
        AssertRedirectsTo(signedOut, "/");

        using HttpResponseMessage manage = await browser.GetAsync("/Account/Manage");
        AssertRedirectsTo(manage, "/Account/Login");
    }

    [Fact]
    public async Task ProfilePhoneNumberCanBeUpdated()
    {
        (BrowserSession browser, _) = await SignedInAsync(fixture);
        using BrowserSession session = browser;

        using HttpResponseMessage saved = await browser.SubmitFormAsync("/Account/Manage", "profile",
            new Dictionary<string, string> { ["Input.PhoneNumber"] = "+44 7700 900123" });
        AssertRedirectsTo(saved, "/Account/Manage");

        string page = await browser.GetStringAsync("/Account/Manage");
        Assert.Contains("Your profile has been updated", page, StringComparison.Ordinal);
        Assert.Equal("+44 7700 900123", FormFieldValue(page, "Input.PhoneNumber"));
    }
}
