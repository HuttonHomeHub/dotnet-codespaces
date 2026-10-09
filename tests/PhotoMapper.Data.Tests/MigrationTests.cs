using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace PhotoMapper.Data.Tests;

// No database needed: these compare the EF Core model with the committed migrations.
public sealed class MigrationTests
{
    [Fact]
    public void ModelHasNoChangesMissingAMigration()
    {
        using ApplicationDbContext context = new DesignTimeDbContextFactory().CreateDbContext([]);

        // Fails when the model changed without `dotnet ef migrations add` (see CLAUDE.md). The migration service
        // would also refuse to start in that state.
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public void IdentitySchemaHasNoPasskeyTable()
    {
        using ApplicationDbContext context = new DesignTimeDbContextFactory().CreateDbContext([]);

        Assert.Null(context.Model.FindEntityType(typeof(IdentityUserPasskey<string>)));
        Assert.NotNull(context.Model.FindEntityType(typeof(ApplicationUser)));
    }
}
