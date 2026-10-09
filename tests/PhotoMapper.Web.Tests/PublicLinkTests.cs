using PhotoMapper.Web.Email;

namespace PhotoMapper.Web.Tests;

// Email links in GitHub Codespaces: the app sees localhost, the browser uses the forwarded address.
public sealed class PublicLinkTests
{
    private static readonly Uri Codespace = new("https://my-codespace-8081.app.github.dev");

    [Fact]
    public void LocalhostLinksPointAtThePublicAddress()
    {
        string link = SmtpEmailSender.ToPublicLink(
            "http://localhost:8081/Account/ConfirmEmail?userId=1&amp;code=abc-_123", Codespace);

        Assert.Equal("https://my-codespace-8081.app.github.dev/Account/ConfirmEmail?userId=1&amp;code=abc-_123", link);
    }

    [Fact]
    public void LinksAreLeftAloneWithoutAPublicAddress()
    {
        const string link = "http://localhost:8081/Account/ConfirmEmail?userId=1&amp;code=abc";

        Assert.Equal(link, SmtpEmailSender.ToPublicLink(link, publicBaseUrl: null));
    }

    [Fact]
    public void LinksToARealHostAreLeftAlone()
    {
        const string link = "https://photos.example.com/Account/ResetPassword?code=abc";

        Assert.Equal(link, SmtpEmailSender.ToPublicLink(link, Codespace));
    }
}
