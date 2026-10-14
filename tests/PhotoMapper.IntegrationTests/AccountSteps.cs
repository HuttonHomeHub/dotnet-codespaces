using System.Text.RegularExpressions;

namespace PhotoMapper.IntegrationTests;

// Shared steps for the account tests: register, confirm, sign in, and response assertions.
internal static partial class AccountSteps
{
    public const string Password = "Sup3r-Secret!pw";
    public const string ConfirmationSubject = "Confirm your PhotoMapper account";

    public static string NewEmail() => $"test-{Guid.NewGuid():N}@example.com";

    public static Task<HttpResponseMessage> RegisterAsync(BrowserSession browser, string email) =>
        browser.SubmitFormAsync("/Account/Register", "register", new Dictionary<string, string>
        {
            ["Input.Email"] = email,
            ["Input.Password"] = Password,
            ["Input.ConfirmPassword"] = Password,
        });

    public static Task<HttpResponseMessage> SignInAsync(BrowserSession browser, string email, string password) =>
        browser.SubmitFormAsync("/Account/Login", "login", new Dictionary<string, string>
        {
            ["Input.Email"] = email,
            ["Input.Password"] = password,
        });

    // Registers and confirms a new account (still waiting for approval); returns its email address.
    public static async Task<string> CreateConfirmedAccountAsync(AppHostFixture fixture)
    {
        string email = NewEmail();
        using BrowserSession browser = fixture.CreateBrowserSession();
        using MailpitInbox inbox = fixture.CreateMailpitInbox();

        using HttpResponseMessage registered = await RegisterAsync(browser, email);
        AssertRedirectsTo(registered, "/Account/RegisterConfirmation");
        string confirmationLink = await inbox.WaitForLinkAsync(email, ConfirmationSubject);
        Assert.Contains("Thank you for confirming your email", await browser.GetStringAsync(confirmationLink), StringComparison.Ordinal);

        return email;
    }

    // Approves an account the way an admin does: the Approve button on the user management page.
    public static async Task ApproveAsync(AppHostFixture fixture, string email)
    {
        BrowserSession admin = await fixture.GetAdminSessionAsync();
        string page = await admin.GetStringAsync($"/admin/users?q={Uri.EscapeDataString(email)}");
        Match approve = Regex.Match(page, "action=\"admin/users/([0-9a-f-]{36})/approve\"");
        Assert.True(approve.Success, $"No Approve button for {email}.");

        using HttpResponseMessage approved = await admin.SubmitFormAsync("/admin/users", handler: null,
            new Dictionary<string, string> { ["returnUrl"] = "/admin/users" }, action: $"/admin/users/{approve.Groups[1].Value}/approve");
        AssertRedirectsTo(approved, "/admin/users");
    }

    // Registers, confirms and approves a new account, so it can sign in; returns its email address.
    public static async Task<string> CreateApprovedAccountAsync(AppHostFixture fixture)
    {
        string email = await CreateConfirmedAccountAsync(fixture);
        await ApproveAsync(fixture, email);
        return email;
    }

    // A new approved account, signed in. Dispose the returned session.
    public static async Task<(BrowserSession Browser, string Email)> SignedInAsync(AppHostFixture fixture)
    {
        string email = await CreateApprovedAccountAsync(fixture);
        BrowserSession browser = fixture.CreateBrowserSession();
        using HttpResponseMessage signedIn = await SignInAsync(browser, email, Password);
        AssertRedirectsTo(signedIn, "/");
        return (browser, email);
    }

    public static Task<string> ReadAsync(HttpResponseMessage response) =>
        response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

    public static void AssertRedirectsTo(HttpResponseMessage response, string path)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Uri location = Assert.IsType<Uri>(response.Headers.Location);
        // The header may be relative; resolve it against any base to compare paths.
        Assert.Equal(path, new Uri(new Uri("http://localhost/"), location).AbsolutePath);
    }

    // The value of the <input> with the given name, whatever order its attributes render in.
    public static string FormFieldValue(string html, string name)
    {
        Match input = Regex.Match(html, $"<input[^>]*\\sname=\"{Regex.Escape(name)}\"[^>]*>");
        Assert.True(input.Success, $"No {name} field on the page.");
        return System.Net.WebUtility.HtmlDecode(Regex.Match(input.Value, "\\svalue=\"([^\"]*)\"").Groups[1].Value);
    }
}
