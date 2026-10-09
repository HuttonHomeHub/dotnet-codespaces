using Npgsql;

namespace PhotoMapper.Data;

public static class DatabaseConnection
{
    // We don't use Kerberos, and the chiseled container images don't include its library, so without this Npgsql
    // logs "Cannot load library libgssapi_krb5.so.2" as an error whenever it opens a connection.
    public static string? WithoutGssEncryption(string? connectionString) =>
        connectionString is null
            ? null
            : new NpgsqlConnectionStringBuilder(connectionString) { GssEncryptionMode = GssEncryptionMode.Disable }.ConnectionString;
}
