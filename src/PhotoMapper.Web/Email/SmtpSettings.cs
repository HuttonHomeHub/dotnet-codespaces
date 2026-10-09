using System.Data.Common;

using MailKit.Security;

namespace PhotoMapper.Web.Email;

// Parsed "mail" connection string: Endpoint=smtp://host:port or smtps://host:port, plus optional Username and
// Password. Mailpit (local) supplies the endpoint only; a real provider adds credentials (see deploy/README.md).
internal sealed record SmtpSettings(string Host, int Port, SecureSocketOptions Security, string? Username, string? Password)
{
    public static SmtpSettings Parse(string connectionString, bool allowUnencrypted)
    {
        DbConnectionStringBuilder values = new() { ConnectionString = connectionString };

        if (!values.TryGetValue("Endpoint", out object? endpointValue)
            || !Uri.TryCreate(endpointValue as string, UriKind.Absolute, out Uri? endpoint))
        {
            throw new FormatException("The mail connection string needs Endpoint=smtp://host:port or smtps://host:port.");
        }

        SecureSocketOptions security = endpoint.Scheme switch
        {
            // Implicit TLS, usually port 465.
            "smtps" => SecureSocketOptions.SslOnConnect,
            // STARTTLS, usually port 587. Only development (Mailpit) may fall back to an unencrypted connection.
            "smtp" => allowUnencrypted ? SecureSocketOptions.StartTlsWhenAvailable : SecureSocketOptions.StartTls,
            _ => throw new FormatException($"Unsupported mail endpoint scheme '{endpoint.Scheme}'; use smtp or smtps."),
        };

        return new SmtpSettings(
            endpoint.Host,
            endpoint.IsDefaultPort ? (security == SecureSocketOptions.SslOnConnect ? 465 : 587) : endpoint.Port,
            security,
            values.TryGetValue("Username", out object? username) ? username as string : null,
            values.TryGetValue("Password", out object? password) ? password as string : null);
    }
}
