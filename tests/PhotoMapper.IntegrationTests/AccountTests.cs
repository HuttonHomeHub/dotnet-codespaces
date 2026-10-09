using System.Text.RegularExpressions;

using static PhotoMapper.IntegrationTests.AccountSteps;

namespace PhotoMapper.IntegrationTests;

// Sign-up, sign-in and password reset through the real web app, PostgreSQL and Mailpit.
public sealed class AccountTests(AppHostFixture fixture)
{
    [Fact]
    public async Task NewAccountCanSignInOnlyAfterConfirmingEmail()
    {
        string email = NewEmail();
        using BrowserSession browser = fixture.CreateBrowserSession();
        using MailpitInbox inbox = fixture.CreateMailpitInbox();

        using HttpResponseMessage registered = await RegisterAsync(browser, email);
        AssertRedirectsTo(registered, "/Account/RegisterConfirmation");

        using HttpResponseMessage beforeConfirming = await SignInAsync(browser, email, Password);
        Assert.Equal(HttpStatusCode.OK, beforeConfirming.StatusCode);
        Assert.Contains("Invalid login attempt", await ReadAsync(beforeConfirming), StringComparison.Ordinal);

        string confirmationLink = await inbox.WaitForLinkAsync(email, ConfirmationSubject);
        Assert.Contains("Thank you for confirming your email", await browser.GetStringAsync(confirmationLink), StringComparison.Ordinal);

        using HttpResponseMessage signedIn = await SignInAsync(browser, email, Password);
        AssertRedirectsTo(signedIn, "/");

        using HttpResponseMessage manage = await browser.GetAsync("/Account/Manage");
        Assert.Equal(HttpStatusCode.OK, manage.StatusCode);
    }

    [Fact]
    public async Task PasswordResetEmailLetsUserChooseANewPassword()
    {
        string email = await CreateConfirmedAccountAsync(fixture);
        const string newPassword = "N3w-Secret!pw";
        using BrowserSession browser = fixture.CreateBrowserSession();
        using MailpitInbox inbox = fixture.CreateMailpitInbox();

        using HttpResponseMessage requested = await browser.SubmitFormAsync(
            "/Account/ForgotPassword", "forgot-password", new Dictionary<string, string> { ["Input.Email"] = email });
        AssertRedirectsTo(requested, "/Account/ForgotPasswordConfirmation");

        string resetLink = await inbox.WaitForLinkAsync(email, "Reset your PhotoMapper password");
        string code = FormFieldValue(await browser.GetStringAsync(resetLink), "Input.Code");

        using HttpResponseMessage reset = await browser.SubmitFormAsync(resetLink, "reset-password", new Dictionary<string, string>
        {
            ["Input.Code"] = code,
            ["Input.Email"] = email,
            ["Input.Password"] = newPassword,
            ["Input.ConfirmPassword"] = newPassword,
        });
        AssertRedirectsTo(reset, "/Account/ResetPasswordConfirmation");

        using HttpResponseMessage oldPassword = await SignInAsync(browser, email, Password);
        Assert.Contains("Invalid login attempt", await ReadAsync(oldPassword), StringComparison.Ordinal);

        using HttpResponseMessage signedIn = await SignInAsync(browser, email, newPassword);
        AssertRedirectsTo(signedIn, "/");
    }

    [Fact]
    public async Task ForgotPasswordDoesNotRevealWhetherAnAccountExists()
    {
        using BrowserSession browser = fixture.CreateBrowserSession();

        using HttpResponseMessage response = await browser.SubmitFormAsync(
            "/Account/ForgotPassword", "forgot-password", new Dictionary<string, string> { ["Input.Email"] = NewEmail() });

        AssertRedirectsTo(response, "/Account/ForgotPasswordConfirmation");
    }

    [Fact]
    public async Task RepeatedWrongPasswordsLockTheAccount()
    {
        string email = await CreateConfirmedAccountAsync(fixture);
        using BrowserSession browser = fixture.CreateBrowserSession();

        // Identity's default: the fifth failed attempt locks the account for five minutes.
        for (int attempt = 1; attempt <= 4; attempt++)
        {
            using HttpResponseMessage failed = await SignInAsync(browser, email, "Wrong-Passw0rd!");
            Assert.Contains("Invalid login attempt", await ReadAsync(failed), StringComparison.Ordinal);
        }

        using HttpResponseMessage fifthFailure = await SignInAsync(browser, email, "Wrong-Passw0rd!");
        AssertRedirectsTo(fifthFailure, "/Account/Lockout");

        // Locked out: even the right password is refused.
        using HttpResponseMessage correctPassword = await SignInAsync(browser, email, Password);
        AssertRedirectsTo(correctPassword, "/Account/Lockout");
    }

    [Fact]
    public async Task AccountPagesRequireSignIn()
    {
        using BrowserSession browser = fixture.CreateBrowserSession();

        using HttpResponseMessage response = await browser.GetAsync("/Account/Manage");

        AssertRedirectsTo(response, "/Account/Login");
    }

    [Fact]
    public async Task RegisterConfirmationDoesNotRevealWhetherAnAccountExists()
    {
        using BrowserSession browser = fixture.CreateBrowserSession();

        using HttpResponseMessage response = await browser.GetAsync("/Account/RegisterConfirmation?email=nobody@example.com");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Please check your email to confirm your account.", await ReadAsync(response), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResendingConfirmationSendsAWorkingLink()
    {
        string email = NewEmail();
        using BrowserSession browser = fixture.CreateBrowserSession();
        using MailpitInbox inbox = fixture.CreateMailpitInbox();
        using HttpResponseMessage registered = await RegisterAsync(browser, email);
        string firstLink = await inbox.WaitForLinkAsync(email, ConfirmationSubject);

        using HttpResponseMessage resent = await browser.SubmitFormAsync("/Account/ResendEmailConfirmation",
            "resend-email-confirmation", new Dictionary<string, string> { ["Input.Email"] = email });
        Assert.Contains("Verification email sent", await ReadAsync(resent), StringComparison.Ordinal);

        string secondLink = await inbox.WaitForLinkAsync(email, ConfirmationSubject, skipping: firstLink);
        Assert.Contains("Thank you for confirming your email", await browser.GetStringAsync(secondLink), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResendConfirmationDoesNotRevealWhetherAnAccountExists()
    {
        using BrowserSession browser = fixture.CreateBrowserSession();

        using HttpResponseMessage response = await browser.SubmitFormAsync("/Account/ResendEmailConfirmation",
            "resend-email-confirmation", new Dictionary<string, string> { ["Input.Email"] = NewEmail() });

        Assert.Contains("Verification email sent", await ReadAsync(response), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConfirmationLinkWithATamperedCodeIsRejected()
    {
        string email = NewEmail();
        using BrowserSession browser = fixture.CreateBrowserSession();
        using MailpitInbox inbox = fixture.CreateMailpitInbox();
        using HttpResponseMessage registered = await RegisterAsync(browser, email);
        string link = await inbox.WaitForLinkAsync(email, ConfirmationSubject);

        // A well-formed but wrong code: base64url of "not-the-code".
        string tampered = Regex.Replace(link, "code=[^&]+", "code=bm90LXRoZS1jb2Rl");

        Assert.Contains("Error confirming your email", await browser.GetStringAsync(tampered), StringComparison.Ordinal);
        using HttpResponseMessage signIn = await SignInAsync(browser, email, Password);
        Assert.Contains("Invalid login attempt", await ReadAsync(signIn), StringComparison.Ordinal);
    }
}
