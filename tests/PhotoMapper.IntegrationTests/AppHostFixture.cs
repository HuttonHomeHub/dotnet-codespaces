using Aspire.Hosting;

using Microsoft.Extensions.DependencyInjection;

[assembly: AssemblyFixture(typeof(PhotoMapper.IntegrationTests.AppHostFixture))]

namespace PhotoMapper.IntegrationTests;

// Starts the whole distributed app once per test run. Every test class shares it by taking an
// AppHostFixture constructor parameter, so add tests here rather than booting another AppHost.
public sealed class AppHostFixture : IAsyncLifetime
{
    public const string ApiService = "apiservice";
    public const string WebFrontend = "webfrontend";
    public const string Mail = "mail";

    // First runs pull the PostgreSQL and Mailpit images.
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(5);

    private DistributedApplication? _app;

    private DistributedApplication App => _app ?? throw new InvalidOperationException("The app has not been started.");

    public HttpClient CreateHttpClient(string resourceName) => App.CreateHttpClient(resourceName);

    // A browser-like client for the web app with its own cookies, so tests don't share a signed-in session.
    public BrowserSession CreateBrowserSession() => new(App.GetEndpoint(WebFrontend, "http"));

    // The Mailpit inbox that receives every email the app sends.
    public MailpitInbox CreateMailpitInbox() => new(App.GetEndpoint(Mail, "http"));

    public async ValueTask InitializeAsync()
    {
        using CancellationTokenSource timeout = new(StartupTimeout);
        CancellationToken cancellationToken = timeout.Token;

        // UseVolumes=false: an empty database for every test run, separate from the development database.
        IDistributedApplicationTestingBuilder appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.PhotoMapper_AppHost>(
            ["UseVolumes=false"], cancellationToken);
        appHost.Services.ConfigureHttpClientDefaults(clientBuilder => clientBuilder.AddStandardResilienceHandler());

        _app = await appHost.BuildAsync(cancellationToken);
        await _app.StartAsync(cancellationToken);

        // The web app only starts once the migration service has finished, so healthy means the schema is ready.
        await Task.WhenAll(
            _app.ResourceNotifications.WaitForResourceHealthyAsync(ApiService, cancellationToken),
            _app.ResourceNotifications.WaitForResourceHealthyAsync(WebFrontend, cancellationToken),
            _app.ResourceNotifications.WaitForResourceHealthyAsync(Mail, cancellationToken));
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }
}
