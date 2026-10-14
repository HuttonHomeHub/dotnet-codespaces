using static PhotoMapper.IntegrationTests.AccountSteps;

namespace PhotoMapper.IntegrationTests;

// Every page needs sign-in except the ones that get you signed in, which use the sign-in screen layout.
public sealed class SignInGateTests(AppHostFixture fixture)
{
    [Theory]
    [InlineData("/")]
    [InlineData("/admin/users")]
    [InlineData("/Account/Manage")]
    [InlineData("/Account/Manage/ChangePassword")]
    [InlineData("/does-not-exist")]
    public async Task SignedOutVisitorsAreSentToSignIn(string path)
    {
        using BrowserSession anonymous = fixture.CreateBrowserSession();

        using HttpResponseMessage response = await anonymous.GetAsync(path);

        AssertRedirectsTo(response, "/Account/Login");
        Assert.Contains($"ReturnUrl={Uri.EscapeDataString(path)}", response.Headers.Location?.OriginalString, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/Account/Login")]
    [InlineData("/Account/Register")]
    [InlineData("/Account/ForgotPassword")]
    [InlineData("/Account/ResendEmailConfirmation")]
    [InlineData("/Account/Lockout")]
    public async Task SignInPagesAreOpenAndUseTheSignInScreen(string path)
    {
        using BrowserSession anonymous = fixture.CreateBrowserSession();

        using HttpResponseMessage response = await anonymous.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string page = await ReadAsync(response);
        Assert.Contains("account-card", page, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"sidebar\"", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SigningInReturnsToThePageYouWanted()
    {
        string email = await CreateApprovedAccountAsync(fixture);
        using BrowserSession browser = fixture.CreateBrowserSession();

        using HttpResponseMessage wanted = await browser.GetAsync("/Account/Manage/ChangePassword");
        string loginUrl = wanted.Headers.Location!.PathAndQuery;
        using HttpResponseMessage signedIn = await browser.SubmitFormAsync(loginUrl, "login",
            new Dictionary<string, string> { ["Input.Email"] = email, ["Input.Password"] = Password });

        AssertRedirectsTo(signedIn, "/Account/Manage/ChangePassword");
    }

    [Fact]
    public async Task SignedInPagesKeepTheAppMenu()
    {
        (BrowserSession browser, _) = await SignedInAsync(fixture);
        using BrowserSession session = browser;

        string page = await browser.GetStringAsync("/Account/Manage");

        Assert.Contains("class=\"sidebar\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain("account-card", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StylesheetsAreAvailableBeforeSigningIn()
    {
        using BrowserSession anonymous = fixture.CreateBrowserSession();

        using HttpResponseMessage response = await anonymous.GetAsync("/lib/bootstrap/dist/css/bootstrap.min.css");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
