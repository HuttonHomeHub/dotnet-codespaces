using Microsoft.AspNetCore.Identity;

using PhotoMapper.Data;
using PhotoMapper.MigrationService;

// Normally applies migrations and exits. With `make-admin <email>` it runs that command instead (see MakeAdminCommand).
string? makeAdminEmail = args is [MakeAdminCommand.Name, string email] ? email : null;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(makeAdminEmail is null ? args : []);

builder.AddServiceDefaults();

builder.Services.Configure<IdentityOptions>(options => IdentityStoreSettings.Apply(options.Stores));
builder.AddNpgsqlDbContext<ApplicationDbContext>("photomapperdb", settings =>
    settings.ConnectionString = DatabaseConnection.WithoutGssEncryption(settings.ConnectionString));

if (makeAdminEmail is not null)
{
    builder.Services.AddIdentityCore<ApplicationUser>()
        .AddRoles<IdentityRole>()
        .AddEntityFrameworkStores<ApplicationDbContext>();

    using IHost commandHost = builder.Build();
    return await MakeAdminCommand.RunAsync(commandHost.Services, makeAdminEmail, Console.Out);
}

builder.Services.AddHostedService<Worker>();
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(Worker.ActivitySourceName));

await builder.Build().RunAsync();
return 0;
