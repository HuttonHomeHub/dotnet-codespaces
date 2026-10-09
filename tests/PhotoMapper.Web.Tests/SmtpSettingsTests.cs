using MailKit.Security;

using PhotoMapper.Web.Email;

namespace PhotoMapper.Web.Tests;

public sealed class SmtpSettingsTests
{
    [Fact]
    public void MailpitConnectionStringAllowsUnencryptedSmtpInDevelopment()
    {
        SmtpSettings settings = SmtpSettings.Parse("Endpoint=smtp://localhost:1025", allowUnencrypted: true);

        Assert.Equal(new SmtpSettings("localhost", 1025, SecureSocketOptions.StartTlsWhenAvailable, null, null), settings);
    }

    [Fact]
    public void PlainSmtpRequiresStartTlsOutsideDevelopment()
    {
        SmtpSettings settings = SmtpSettings.Parse("Endpoint=smtp://smtp.example.com:587", allowUnencrypted: false);

        Assert.Equal(SecureSocketOptions.StartTls, settings.Security);
    }

    [Fact]
    public void SmtpsUsesImplicitTlsAndDefaultsToPort465()
    {
        SmtpSettings settings = SmtpSettings.Parse("Endpoint=smtps://smtp.example.com", allowUnencrypted: false);

        Assert.Equal(SecureSocketOptions.SslOnConnect, settings.Security);
        Assert.Equal(465, settings.Port);
    }

    [Fact]
    public void SmtpDefaultsToSubmissionPort587()
    {
        Assert.Equal(587, SmtpSettings.Parse("Endpoint=smtp://smtp.example.com", allowUnencrypted: false).Port);
    }

    [Fact]
    public void CredentialsAreRead()
    {
        // A password containing ';' must be quoted, as in any connection string.
        SmtpSettings settings = SmtpSettings.Parse(
            "Endpoint=smtps://smtp.example.com:465;Username=apikey;Password=\"p@ss;word\"", allowUnencrypted: false);

        Assert.Equal("apikey", settings.Username);
        Assert.Equal("p@ss;word", settings.Password);
    }

    [Theory]
    [InlineData("Host=smtp.example.com")]
    [InlineData("Endpoint=not a url")]
    [InlineData("Endpoint=http://smtp.example.com")]
    public void InvalidConnectionStringsAreRejected(string connectionString)
    {
        Assert.Throws<FormatException>(() => SmtpSettings.Parse(connectionString, allowUnencrypted: false));
    }
}
