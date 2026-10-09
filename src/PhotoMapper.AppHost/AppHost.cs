using Aspire.Hosting.Docker.Resources.ServiceNodes;

using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

// Deployment target: `aspire publish` turns the app model into docker-compose.yaml + .env (see deploy/README.md).
builder.AddDockerComposeEnvironment("compose")
    .WithDashboard(dashboard =>
    {
        // Use the released dashboard image, at the tag Aspire picked, rather than the default nightly feed.
        string? tag = dashboard.Resource.Annotations.OfType<ContainerImageAnnotation>().Last().Tag;
        dashboard.WithImage("dotnet/aspire-dashboard", tag).WithImageRegistry("mcr.microsoft.com");
    })
    .ConfigureComposeFile(file =>
    {
        // The dashboard has no TLS of its own: only reachable from the server itself (use an SSH tunnel).
        file.Services["compose-dashboard"].Ports = ["127.0.0.1:18888:18888"];
        file.AddVolume(new Volume { Name = "webfrontend-home" });
    });

// PostgreSQL: user accounts now, photo data later. The data volume keeps the database between runs (and on the
// server); the integration tests turn it off with UseVolumes=false so every test run starts from an empty database.
var postgres = builder.AddPostgres("postgres")
    .PublishAsDockerComposeService((_, service) =>
    {
        service.Restart = "unless-stopped";
        // Lets the migration service wait until PostgreSQL accepts connections, not just until its container starts.
        service.Healthcheck = new Healthcheck
        {
            Test = ["CMD-SHELL", "pg_isready -U postgres"],
            Interval = "5s",
            Timeout = "5s",
            Retries = 12,
            StartPeriod = "10s",
        };
    });
if (builder.Configuration.GetValue("UseVolumes", defaultValue: true))
{
    // A fixed name: the default includes a hash of the AppHost's path, so a deployment built from a different
    // checkout path would silently start on a new, empty database.
    postgres.WithDataVolume("photomapper-postgres-data");
}

var database = postgres.AddDatabase("photomapperdb");

// Applies EF Core migrations, then exits; the web app waits for it to finish.
var migrations = builder.AddProject<Projects.PhotoMapper_MigrationService>("migrations")
    .WithReference(database)
    .WaitFor(database)
    .PublishAsDockerComposeService((_, service) => service.DependsOn["postgres"].Condition = "service_healthy");

// Email: a local Mailpit inbox when running (http://localhost:8025, also linked from the dashboard);
// when deployed, a "mail" connection string for a real SMTP provider (see deploy/README.md).
IResourceBuilder<IResourceWithConnectionString> mail = builder.ExecutionContext.IsRunMode
    ? builder.AddMailPit("mail", httpPort: 8025)
    : builder.AddConnectionString("mail");

var apiService = builder.AddProject<Projects.PhotoMapper_ApiService>("apiservice")
    .WithHttpHealthCheck("/health")
    .PublishAsDockerComposeService((_, service) => service.Restart = "unless-stopped");

var web = builder.AddProject<Projects.PhotoMapper_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService)
    .WithReference(database)
    .WithReference(mail)
    .WaitForCompletion(migrations)
    .PublishAsDockerComposeService((_, service) =>
    {
        service.Restart = "unless-stopped";
        // Not published on the host: the reverse proxy in deploy/compose.proxy.yaml is the only way in.
        service.Ports = [];
        // Keep ASP.NET Core data protection keys (antiforgery tokens, auth cookies) across container replacements.
        // Mounting the image's home directory makes Docker seed the volume with the right (non-root) owner.
        service.AddVolume(new Volume { Name = "webfrontend-home", Source = "webfrontend-home", Target = "/home/app", Type = "volume" });
    });

if (builder.ExecutionContext.IsPublishMode)
{
    // Sender address for account emails; must be one the mail provider lets you send from.
    web.WithEnvironment("Email__From", builder.AddParameter("mail-from"));
}

builder.Build().Run();
