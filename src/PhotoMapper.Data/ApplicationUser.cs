using Microsoft.AspNetCore.Identity;

namespace PhotoMapper.Data;

// Add profile data for application users by adding properties to the ApplicationUser class
public class ApplicationUser : IdentityUser
{
    // Set by the database when the account is created.
    [PersonalData]
    public DateTimeOffset CreatedAt { get; set; }

    // New accounts can't sign in until an admin approves them (on /admin/users, or by making them an admin).
    public bool IsApproved { get; set; }
}
