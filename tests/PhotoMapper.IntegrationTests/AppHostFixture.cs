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

    // Runs the migration service's `make-admin <email>` command against the test database, as an operator would.
    public async Task<(int ExitCode, string Output)> MakeAdminAsync(string email)
    {
        string connectionString = await App.GetConnectionStringAsync("photomapperdb", TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException("No connection string for photomapperdb.");
        string projectDirectory = Path.GetDirectoryName(new Projects.PhotoMapper_MigrationService().ProjectPath)!;
#if DEBUG
        const string configuration = "Debug";
#else
        const string configuration = "Release";
#endif
        string assembly = Path.Join(projectDirectory, "bin", configuration, "net10.0", "PhotoMapper.MigrationService.dll");

        System.Diagnostics.ProcessStartInfo start = new("dotnet", [assembly, "make-admin", email])
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            Environment = { ["ConnectionStrings__photomapperdb"] = connectionString, ["DOTNET_ENVIRONMENT"] = "Development" },
        };
        using System.Diagnostics.Process process = System.Diagnostics.Process.Start(start)!;
        string output = await process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        return (process.ExitCode, output);
    }

    public async ValueTask InitializeAsync()
    {
        using CancellationTokenSource timeout = new(StartupTimeout);
        CancellationToken cancellationToken = timeout.Token;

        // UseVolumes=false: an empty database for every test run, separate from the development database.
        // PublicEmailLinks=false: in Codespaces, keep email links on localhost, which is where these tests run.
        IDistributedApplicationTestingBuilder appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.PhotoMapper_AppHost>(
            ["UseVolumes=false", "PublicEmailLinks=false"], cancellationToken);
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
