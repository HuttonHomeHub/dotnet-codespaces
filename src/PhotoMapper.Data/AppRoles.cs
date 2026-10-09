namespace PhotoMapper.Data;

// Identity roles. The rows are created by a migration (see ApplicationDbContext), so they always exist.
public static class AppRoles
{
    public const string Admin = "Admin";

    // Fixed so the seed data in the migrations never changes.
    internal const string AdminId = "6f0f3c84-1b6e-4c7f-9d3a-2a5b9a7c1e01";
}
