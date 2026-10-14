using static PhotoMapper.IntegrationTests.AccountSteps;

namespace PhotoMapper.IntegrationTests;

public sealed class WebFrontendTests(AppHostFixture fixture)
{
    [Fact]
    public async Task HomePageNeedsSignIn()
    {
        using BrowserSession anonymous = fixture.CreateBrowserSession();
        using HttpResponseMessage signedOut = await anonymous.GetAsync("/");
        AssertRedirectsTo(signedOut, "/Account/Login");

        (BrowserSession browser, _) = await SignedInAsync(fixture);
        using BrowserSession session = browser;
        using HttpResponseMessage home = await browser.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, home.StatusCode);
        Assert.Contains("<h1>PhotoMapper</h1>", await ReadAsync(home), StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownPageShowsNotFoundPageWhenSignedIn()
    {
        (BrowserSession browser, _) = await SignedInAsync(fixture);
        using BrowserSession session = browser;

        using HttpResponseMessage response = await browser.GetAsync("/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("<h1>Not found</h1>", await ReadAsync(response), StringComparison.Ordinal);
    }
}
