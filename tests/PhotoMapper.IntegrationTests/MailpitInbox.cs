using System.Text.Json;
using System.Text.RegularExpressions;

namespace PhotoMapper.IntegrationTests;

// Reads the emails the app sent, through Mailpit's HTTP API (https://mailpit.axllent.org/docs/api-v1/).
public sealed partial class MailpitInbox(Uri baseAddress) : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private readonly HttpClient _client = new() { BaseAddress = baseAddress };

    // Waits for the newest email to `to` whose subject contains `subject`, and returns the first link in its text part.
    // Pass `skipping` (a link already seen) to wait for a newer email.
    public async Task<string> WaitForLinkAsync(string to, string subject, string? skipping = null)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string query = Uri.EscapeDataString($"to:\"{to}\" subject:\"{subject}\"");
        DateTime deadline = DateTime.UtcNow + Timeout;

        while (true)
        {
            using JsonDocument search = JsonDocument.Parse(
                await _client.GetStringAsync(new Uri($"api/v1/search?query={query}", UriKind.Relative), cancellationToken));
            JsonElement messages = search.RootElement.GetProperty("messages");

            if (messages.GetArrayLength() > 0)
            {
                string id = messages[0].GetProperty("ID").GetString()!;
                using JsonDocument message = JsonDocument.Parse(
                    await _client.GetStringAsync(new Uri($"api/v1/message/{id}", UriKind.Relative), cancellationToken));
                string text = message.RootElement.GetProperty("Text").GetString()!;

                Match link = Link().Match(text);
                Assert.True(link.Success, $"No link in the \"{subject}\" email to {to}:\n{text}");
                if (link.Value != skipping)
                {
                    return link.Value;
                }
            }

            Assert.True(DateTime.UtcNow < deadline, $"No \"{subject}\" email to {to} within {Timeout.TotalSeconds}s.");
            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
        }
    }

    public void Dispose() => _client.Dispose();

    [GeneratedRegex(@"https?://\S+")]
    private static partial Regex Link();
}
