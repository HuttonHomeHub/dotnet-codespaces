using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace PhotoMapper.Data;

// Lets `dotnet ef migrations add` build the model without starting the app. No database is contacted.
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        ServiceProvider services = new ServiceCollection()
            .Configure<IdentityOptions>(options => IdentityStoreSettings.Apply(options.Stores))
            .BuildServiceProvider();

        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=photomapperdb")
            .UseApplicationServiceProvider(services)
            .Options;

        return new ApplicationDbContext(options);
    }
}
