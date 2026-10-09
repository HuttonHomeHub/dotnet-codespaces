using MailKit.Security;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using PhotoMapper.Data;
using PhotoMapper.Web.Email;

namespace PhotoMapper.Web.Tests;

public sealed class SmtpEmailSenderTests
{
    // Nothing listens on port 1, so connecting fails straight away.
    private static readonly SmtpSettings Unreachable = new("127.0.0.1", 1, SecureSocketOptions.None, null, null);

    [Fact]
    public async Task FailureToSendIsLoggedNotThrown()
    {
        RecordingLogger<SmtpEmailSender> logger = new();
        SmtpEmailSender sender = new(Unreachable, Options.Create(new EmailOptions()), logger);

        await sender.SendPasswordResetLinkAsync(new ApplicationUser(), "user@example.com", "https://example.com/reset?code=1&amp;x=2");
        await sender.SendConfirmationLinkAsync(new ApplicationUser(), "user@example.com", "https://example.com/confirm");
        await sender.SendPasswordResetCodeAsync(new ApplicationUser(), "user@example.com", "123456");

        Assert.Equal(3, logger.Errors.Count);
        Assert.Contains("Reset your PhotoMapper password", logger.Errors[0], StringComparison.Ordinal);
        Assert.Contains("127.0.0.1:1", logger.Errors[0], StringComparison.Ordinal);
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Errors { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Error)
            {
                Errors.Add(formatter(state, exception));
            }
        }
    }
}
