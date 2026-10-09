using System.Net;
using System.Net.Sockets;
using System.Text.Encodings.Web;

using MailKit;
using MailKit.Net.Smtp;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

using MimeKit;

using PhotoMapper.Data;

namespace PhotoMapper.Web.Email;

// Sends Identity's account emails (confirmation, password reset) over SMTP with MailKit.
internal sealed partial class SmtpEmailSender(
    SmtpSettings settings,
    IOptions<EmailOptions> options,
    ILogger<SmtpEmailSender> logger) : IEmailSender<ApplicationUser>
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // Identity's contract: the links passed in are already HTML-encoded (the account pages encode them), so they go
    // into the HTML part as they are and are decoded for the plain-text part.
    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink)
    {
        confirmationLink = ToPublicLink(confirmationLink, options.Value.PublicBaseUrl);
        return SendAsync(
            email,
            "Confirm your PhotoMapper account",
            $"Please confirm your PhotoMapper account by <a href=\"{confirmationLink}\">clicking here</a>.",
            $"Please confirm your PhotoMapper account by opening this link: {WebUtility.HtmlDecode(confirmationLink)}");
    }

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink)
    {
        resetLink = ToPublicLink(resetLink, options.Value.PublicBaseUrl);
        return SendAsync(
            email,
            "Reset your PhotoMapper password",
            $"Reset your PhotoMapper password by <a href=\"{resetLink}\">clicking here</a>. If you didn't ask for this, you can ignore this email.",
            $"Reset your PhotoMapper password by opening this link: {WebUtility.HtmlDecode(resetLink)}\n\nIf you didn't ask for this, you can ignore this email.");
    }

    // Points an (HTML-encoded) link at localhost to the public address instead; other links are left alone.
    internal static string ToPublicLink(string htmlEncodedLink, Uri? publicBaseUrl)
    {
        if (publicBaseUrl is null
            || !Uri.TryCreate(WebUtility.HtmlDecode(htmlEncodedLink), UriKind.Absolute, out Uri? link)
            || !link.IsLoopback)
        {
            return htmlEncodedLink;
        }

        UriBuilder rewritten = new(link) { Scheme = publicBaseUrl.Scheme, Host = publicBaseUrl.Host, Port = publicBaseUrl.Port };
        return HtmlEncoder.Default.Encode(rewritten.Uri.AbsoluteUri);
    }

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
        SendAsync(
            email,
            "Reset your PhotoMapper password",
            $"Your PhotoMapper password reset code is: {HtmlEncoder.Default.Encode(resetCode)}",
            $"Your PhotoMapper password reset code is: {resetCode}");

    // Failures are logged, not thrown: the account pages then carry on (the user can ask for the email again)
    // instead of showing an error page after the account was already created.
    private async Task SendAsync(string to, string subject, string htmlBody, string textBody)
    {
        using MimeMessage message = new();
        message.From.Add(MailboxAddress.Parse(options.Value.From));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody, TextBody = textBody }.ToMessageBody();

        try
        {
            using SmtpClient client = new() { Timeout = (int)Timeout.TotalMilliseconds };
            await client.ConnectAsync(settings.Host, settings.Port, settings.Security);
            if (settings.Username is not null)
            {
                await client.AuthenticateAsync(settings.Username, settings.Password ?? string.Empty);
            }

            await client.SendAsync(message);
            await client.DisconnectAsync(quit: true);
        }
        catch (Exception ex) when (ex is IOException or SocketException or TimeoutException
            or ProtocolException or CommandException or MailKit.Security.AuthenticationException
            or MailKit.Security.SslHandshakeException or ServiceNotConnectedException)
        {
            LogSendFailed(logger, ex, subject, settings.Host, settings.Port);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not send email \"{Subject}\" via {Host}:{Port}")]
    private static partial void LogSendFailed(ILogger logger, Exception exception, string subject, string host, int port);
}
