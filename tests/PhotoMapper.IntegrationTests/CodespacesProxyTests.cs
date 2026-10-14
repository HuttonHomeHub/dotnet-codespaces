using static PhotoMapper.IntegrationTests.AccountSteps;

namespace PhotoMapper.IntegrationTests;

// In Codespaces the browser reaches the app through https://<codespace>-<port>.app.github.dev and the forwarding proxy
// passes that address in X-Forwarded-Host/Proto. The fixture sets CodespacesForwardingDomain=app.github.dev, so the
// web app trusts those headers for that domain, as it does in a codespace.
public sealed class CodespacesProxyTests(AppHostFixture fixture)
{
    private const string PublicHost = "test-codespace-8081.app.github.dev";

    [Fact]
    public async Task SigningInRedirectsToThePublicAddress()
    {
        string email = await CreateConfirmedAccountAsync(fixture);
        using BrowserSession browser = ThroughProxy(PublicHost);

        using HttpResponseMessage signedIn = await SignInAsync(browser, email, Password);

        Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);
        Assert.Equal($"https://{PublicHost}/", signedIn.Headers.Location?.AbsoluteUri);
    }

    [Fact]
    public async Task EmailLinksUseThePublicAddress()
    {
        string email = NewEmail();
        using BrowserSession browser = ThroughProxy(PublicHost);
        using MailpitInbox inbox = fixture.CreateMailpitInbox();

        using HttpResponseMessage registered = await RegisterAsync(browser, email);
        string link = await inbox.WaitForLinkAsync(email, ConfirmationSubject);

        Assert.StartsWith($"https://{PublicHost}/Account/ConfirmEmail?", link, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ForwardedHostsOutsideTheCodespacesDomainAreIgnored()
    {
        // Otherwise anyone could make the app put their own host name into password reset links.
        string email = await CreateConfirmedAccountAsync(fixture);
        using BrowserSession browser = ThroughProxy("evil.example.com");

        using HttpResponseMessage signedIn = await SignInAsync(browser, email, Password);

        Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);
        Assert.DoesNotContain("evil.example.com", signedIn.Headers.Location?.OriginalString ?? "", StringComparison.Ordinal);
    }

    private BrowserSession ThroughProxy(string host)
    {
        BrowserSession browser = fixture.CreateBrowserSession();
        browser.AddHeader("X-Forwarded-Host", host);
        browser.AddHeader("X-Forwarded-Proto", "https");
        return browser;
    }
}
