using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Identity;

using PhotoMapper.Data;

namespace PhotoMapper.Web.Email;

// Emails around account approval: admins hear about new accounts waiting for them, and people hear when they can
// sign in. Links are absolute (built by the caller from the current request) so they work from any mail client.
internal sealed class AccountNotifications(SmtpEmailSender sender, UserManager<ApplicationUser> userManager)
{
    public async Task NewAccountWaitingAsync(ApplicationUser newUser, Uri reviewLink)
    {
        ArgumentNullException.ThrowIfNull(newUser);
        ArgumentNullException.ThrowIfNull(reviewLink);

        string email = newUser.Email ?? "(no email)";
        foreach (ApplicationUser admin in await userManager.GetUsersInRoleAsync(AppRoles.Admin))
        {
            if (admin.Email is null)
            {
                continue;
            }

            await sender.SendAsync(
                admin.Email,
                "New PhotoMapper account waiting for approval",
                $"<strong>{HtmlEncoder.Default.Encode(email)}</strong> has signed up and confirmed their email address. " +
                $"<a href=\"{HtmlEncoder.Default.Encode(reviewLink.AbsoluteUri)}\">Review it</a> to approve the account.",
                $"{email} has signed up and confirmed their email address. Review it to approve the account: {reviewLink.AbsoluteUri}");
        }
    }

    public Task AccountApprovedAsync(ApplicationUser user, Uri signInLink)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(signInLink);

        return user.Email is null
            ? Task.CompletedTask
            : sender.SendAsync(
                user.Email,
                "Your PhotoMapper account is approved",
                $"Your PhotoMapper account has been approved. You can now <a href=\"{HtmlEncoder.Default.Encode(signInLink.AbsoluteUri)}\">sign in</a>.",
                $"Your PhotoMapper account has been approved. You can now sign in: {signInLink.AbsoluteUri}");
    }
}
