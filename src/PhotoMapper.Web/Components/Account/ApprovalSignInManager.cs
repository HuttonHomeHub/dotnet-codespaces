using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

using PhotoMapper.Data;

namespace PhotoMapper.Web.Components.Account;

// Identity's sign-in manager plus one rule: an account must be approved by an admin before it can sign in. Identity
// asks CanSignInAsync on every sign-in path (password, after reset, refresh), so the rule can't be bypassed.
internal sealed class ApprovalSignInManager(
    UserManager<ApplicationUser> userManager,
    IHttpContextAccessor contextAccessor,
    IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory,
    IOptions<IdentityOptions> optionsAccessor,
    ILogger<SignInManager<ApplicationUser>> logger,
    IAuthenticationSchemeProvider schemes,
    IUserConfirmation<ApplicationUser> confirmation)
    : SignInManager<ApplicationUser>(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
{
    public override async Task<bool> CanSignInAsync(ApplicationUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return user.IsApproved && await base.CanSignInAsync(user);
    }
}
