using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace PhotoMapper.Data;

// The app's database: ASP.NET Core Identity's tables today, the photo data later. Migrations live in Migrations/
// and are applied by PhotoMapper.MigrationService before the web app starts. Identity builds its tables from the
// IdentityOptions registered in the app's services, so every host must call IdentityStoreSettings.Apply.
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>()
            .Property(user => user.CreatedAt)
            .HasDefaultValueSql("now()");

        builder.Entity<IdentityRole>().HasData(new IdentityRole
        {
            Id = AppRoles.AdminId,
            Name = AppRoles.Admin,
            NormalizedName = AppRoles.Admin.ToUpperInvariant(),
            ConcurrencyStamp = AppRoles.AdminId,
        });
    }
}
