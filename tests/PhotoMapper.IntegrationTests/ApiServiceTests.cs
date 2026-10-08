namespace PhotoMapper.IntegrationTests;

public sealed class ApiServiceTests(AppHostFixture fixture)
{
    private readonly HttpClient _client = fixture.CreateHttpClient(AppHostFixture.ApiService);

    [Fact]
    public async Task RootReturnsOk()
    {
        HttpResponseMessage response = await _client.GetAsync(new Uri("/", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task OpenApiDocumentIsServedInDevelopment()
    {
        HttpResponseMessage response = await _client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }
}
