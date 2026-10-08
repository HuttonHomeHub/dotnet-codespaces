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

    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(2);

    private DistributedApplication? _app;

    public HttpClient CreateHttpClient(string resourceName)
    {
        if (_app is null)
        {
            throw new InvalidOperationException("The app has not been started.");
        }

        return _app.CreateHttpClient(resourceName);
    }

    public async ValueTask InitializeAsync()
    {
        using CancellationTokenSource timeout = new(StartupTimeout);
        CancellationToken cancellationToken = timeout.Token;

        IDistributedApplicationTestingBuilder appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.PhotoMapper_AppHost>(cancellationToken);
        appHost.Services.ConfigureHttpClientDefaults(clientBuilder => clientBuilder.AddStandardResilienceHandler());

        _app = await appHost.BuildAsync(cancellationToken);
        await _app.StartAsync(cancellationToken);

        await Task.WhenAll(
            _app.ResourceNotifications.WaitForResourceHealthyAsync(ApiService, cancellationToken),
            _app.ResourceNotifications.WaitForResourceHealthyAsync(WebFrontend, cancellationToken));
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }
}
