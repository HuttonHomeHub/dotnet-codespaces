using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

using PhotoMapper.Data;
using PhotoMapper.Web.Components.Account;

namespace PhotoMapper.Web.Components.Admin;

// The actions behind the user management page (Users.razor). Each is a POST from a form with an antiforgery token,
// limited to admins, refused when aimed at the admin's own account, logged, and followed by a redirect back to the
// list with a status message.
internal static partial class AdminUserEndpoints
{
    public static IEndpointConventionBuilder MapAdminUserEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder users = endpoints.MapGroup("/admin/users")
            .RequireAuthorization(policy => policy.RequireRole(AppRoles.Admin));

        users.MapPost("/{id}/lock", (string id, [FromForm] string? returnUrl, HttpContext context, UserManager<ApplicationUser> userManager, ILogger<Marker> logger) =>
            ChangeAsync(id, returnUrl, context, userManager, async user =>
            {
                await userManager.SetLockoutEnabledAsync(user, true);
                await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
                // Ends the user's sessions at their next security stamp check (within a minute).
                await userManager.UpdateSecurityStampAsync(user);
                LogAction(logger, "locked", user.Id, userManager.GetUserId(context.User));
                return $"{user.Email} is locked and can't sign in.";
            }));

        users.MapPost("/{id}/unlock", (string id, [FromForm] string? returnUrl, HttpContext context, UserManager<ApplicationUser> userManager, ILogger<Marker> logger) =>
            ChangeAsync(id, returnUrl, context, userManager, async user =>
            {
                await userManager.SetLockoutEndDateAsync(user, null);
                await userManager.ResetAccessFailedCountAsync(user);
                LogAction(logger, "unlocked", user.Id, userManager.GetUserId(context.User));
                return $"{user.Email} is unlocked.";
            }));

        users.MapPost("/{id}/grant-admin", (string id, [FromForm] string? returnUrl, HttpContext context, UserManager<ApplicationUser> userManager, ILogger<Marker> logger) =>
            ChangeAsync(id, returnUrl, context, userManager, async user =>
            {
                await userManager.AddToRoleAsync(user, AppRoles.Admin);
                LogAction(logger, "made admin", user.Id, userManager.GetUserId(context.User));
                return $"{user.Email} is now an admin (from their next sign-in, or within a minute).";
            }));

        users.MapPost("/{id}/remove-admin", (string id, [FromForm] string? returnUrl, HttpContext context, UserManager<ApplicationUser> userManager, ILogger<Marker> logger) =>
            ChangeAsync(id, returnUrl, context, userManager, async user =>
            {
                await userManager.RemoveFromRoleAsync(user, AppRoles.Admin);
                await userManager.UpdateSecurityStampAsync(user);
                LogAction(logger, "removed admin from", user.Id, userManager.GetUserId(context.User));
                return $"{user.Email} is no longer an admin.";
            }));

        users.MapPost("/{id}/delete", (string id, [FromForm] string? returnUrl, HttpContext context, UserManager<ApplicationUser> userManager, ILogger<Marker> logger) =>
            ChangeAsync(id, returnUrl, context, userManager, async user =>
            {
                IdentityResult result = await userManager.DeleteAsync(user);
                if (!result.Succeeded)
                {
                    return $"Error: {user.Email} could not be deleted.";
                }

                LogAction(logger, "deleted", user.Id, userManager.GetUserId(context.User));
                return $"{user.Email} has been deleted.";
            }));

        return users;
    }

    private static async Task<IResult> ChangeAsync(
        string id,
        string? returnUrl,
        HttpContext context,
        UserManager<ApplicationUser> userManager,
        Func<ApplicationUser, Task<string>> change)
    {
        // Only ever redirect within the user management page.
        string back = returnUrl is not null && returnUrl.StartsWith("/admin/users", StringComparison.Ordinal)
            && !returnUrl.StartsWith("//", StringComparison.Ordinal)
            ? returnUrl
            : "/admin/users";

        string message;
        ApplicationUser? user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            message = "Error: That account no longer exists.";
        }
        else if (user.Id == userManager.GetUserId(context.User))
        {
            // Stops an admin locking, deleting or demoting themselves, so there is always an admin who can act.
            message = "Error: You can't change your own account here.";
        }
        else
        {
            message = await change(user);
        }

        IdentityRedirectManager.SetStatusMessage(context, message);
        return TypedResults.LocalRedirect(back);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Admin action: user {UserId} {Action} by admin {AdminId}")]
    private static partial void LogAction(ILogger logger, string action, string userId, string? adminId);

    // Logger category for the admin actions.
    internal sealed class Marker;
}
