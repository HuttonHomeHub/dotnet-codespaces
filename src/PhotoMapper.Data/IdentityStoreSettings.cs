using Microsoft.AspNetCore.Identity;

namespace PhotoMapper.Data;

// Identity's table layout depends on these store options, which IdentityDbContext reads from the app's services
// (overriding SchemaVersion on the context is ignored). The web app, the migration service and the design-time
// factory all apply them here, so the model, the migrations and the running app always agree.
public static class IdentityStoreSettings
{
    public static void Apply(StoreOptions stores)
    {
        ArgumentNullException.ThrowIfNull(stores);

        // Version 2: the current layout without passkeys (Version 3 adds an AspNetUserPasskeys table). Moving to
        // Version 3 later means changing this and adding a migration.
        stores.SchemaVersion = IdentitySchemaVersions.Version2;
    }
}
