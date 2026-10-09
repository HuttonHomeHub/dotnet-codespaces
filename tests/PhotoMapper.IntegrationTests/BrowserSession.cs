using System.Text.RegularExpressions;

namespace PhotoMapper.IntegrationTests;

// Talks to the web app like a browser without JavaScript: keeps cookies, and submits the account pages' forms
// (statically rendered Blazor forms) with their antiforgery token and form handler name. Redirects are not
// followed, so tests can assert where the app sends the user.
public sealed partial class BrowserSession : IDisposable
{
    private readonly HttpClient _client;

    public BrowserSession(Uri baseAddress)
    {
        _client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = true })
        {
            BaseAddress = baseAddress,
        };
    }

    public Task<HttpResponseMessage> GetAsync(string pathOrUrl) =>
        _client.GetAsync(new Uri(pathOrUrl, UriKind.RelativeOrAbsolute), TestContext.Current.CancellationToken);

    public async Task<string> GetStringAsync(string pathOrUrl)
    {
        using HttpResponseMessage response = await GetAsync(pathOrUrl);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    // Loads the page, then posts its form with the given fields: back to the same URL with the form's handler name
    // (Blazor forms), or to `action` for plain forms that post to an endpoint (handler null).
    public async Task<HttpResponseMessage> SubmitFormAsync(
        string pageUrl, string? handler, IReadOnlyDictionary<string, string> fields, string? action = null)
    {
        string page = await GetStringAsync(pageUrl);
        Match token = AntiforgeryToken().Match(page);
        Assert.True(token.Success, $"No antiforgery token on {pageUrl}.");

        Dictionary<string, string> form = new(fields) { ["__RequestVerificationToken"] = token.Groups[1].Value };
        if (handler is not null)
        {
            form["_handler"] = handler;
        }

        using FormUrlEncodedContent content = new(form);
        return await _client.PostAsync(new Uri(action ?? pageUrl, UriKind.RelativeOrAbsolute), content, TestContext.Current.CancellationToken);
    }

    public void Dispose() => _client.Dispose();

    [GeneratedRegex("name=\"__RequestVerificationToken\" value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryToken();
}
