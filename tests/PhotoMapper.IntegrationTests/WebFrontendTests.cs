namespace PhotoMapper.IntegrationTests;

public sealed class WebFrontendTests(AppHostFixture fixture)
{
    private readonly HttpClient _client = fixture.CreateHttpClient(AppHostFixture.WebFrontend);

    [Fact]
    public async Task HomePageReturnsOk()
    {
        HttpResponseMessage response = await _client.GetAsync(new Uri("/", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<h1>PhotoMapper</h1>", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownPageReturnsNotFoundPage()
    {
        HttpResponseMessage response = await _client.GetAsync(new Uri("/does-not-exist", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("<h1>Not found</h1>", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }
}
