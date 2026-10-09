using Microsoft.AspNetCore.Identity;

using PhotoMapper.Data;

namespace PhotoMapper.MigrationService;

// `make-admin <email>`: gives an existing, confirmed account the Admin role. This is how the first admin is created;
// after that, admins manage each other on the web app's user management page. The account picks up the role on its
// next sign-in, or within a minute (when its sign-in cookie is next re-checked). See deploy/README.md for running it.
internal static class MakeAdminCommand
{
    public const string Name = "make-admin";

    public static async Task<int> RunAsync(IServiceProvider services, string email, TextWriter output)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        ApplicationUser? user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            await output.WriteLineAsync($"No account uses {email}. Sign up on the web app first, then run this again.");
            return 1;
        }

        if (!user.EmailConfirmed)
        {
            await output.WriteLineAsync($"{email} hasn't confirmed its email address yet. Confirm it, then run this again.");
            return 1;
        }

        if (await userManager.IsInRoleAsync(user, AppRoles.Admin))
        {
            await output.WriteLineAsync($"{email} is already an admin.");
            return 0;
        }

        IdentityResult result = await userManager.AddToRoleAsync(user, AppRoles.Admin);
        if (!result.Succeeded)
        {
            await output.WriteLineAsync($"Could not make {email} an admin: {string.Join(" ", result.Errors.Select(error => error.Description))}");
            return 1;
        }

        await output.WriteLineAsync($"{email} is now an admin.");
        return 0;
    }
}
