using Microsoft.AspNetCore.Identity;

using PhotoMapper.Data;
using PhotoMapper.MigrationService;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

builder.Services.Configure<IdentityOptions>(options => IdentityStoreSettings.Apply(options.Stores));
builder.AddNpgsqlDbContext<ApplicationDbContext>("photomapperdb", settings =>
    settings.ConnectionString = DatabaseConnection.WithoutGssEncryption(settings.ConnectionString));

builder.Services.AddHostedService<Worker>();
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(Worker.ActivitySourceName));

builder.Build().Run();
