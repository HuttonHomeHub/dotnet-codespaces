using Aspire.Hosting.Docker.Resources.ServiceNodes;

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

var apiService = builder.AddProject<Projects.PhotoMapper_ApiService>("apiservice")
    .WithHttpHealthCheck("/health")
    .PublishAsDockerComposeService((_, service) => service.Restart = "unless-stopped");

builder.AddProject<Projects.PhotoMapper_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService)
    .PublishAsDockerComposeService((_, service) =>
    {
        service.Restart = "unless-stopped";
        // Not published on the host: the reverse proxy in deploy/compose.proxy.yaml is the only way in.
        service.Ports = [];
        // Keep ASP.NET Core data protection keys (antiforgery tokens, auth cookies) across container replacements.
        // Mounting the image's home directory makes Docker seed the volume with the right (non-root) owner.
        service.AddVolume(new Volume { Name = "webfrontend-home", Source = "webfrontend-home", Target = "/home/app", Type = "volume" });
    });

builder.Build().Run();
