using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace PhotoMapper.Data;

// The app's database: ASP.NET Core Identity's tables today, the photo data later. Migrations live in Migrations/
// and are applied by PhotoMapper.MigrationService before the web app starts. Identity builds its tables from the
// IdentityOptions registered in the app's services, so every host must call IdentityStoreSettings.Apply.
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
}
