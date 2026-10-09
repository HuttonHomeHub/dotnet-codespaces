namespace PhotoMapper.Web.Email;

// The "Email" configuration section.
internal sealed class EmailOptions
{
    // Sender shown on account emails. When deployed, use an address your mail provider allows you to send from.
    public string From { get; set; } = "PhotoMapper <no-reply@photomapper.local>";

    // Where people reach the app, when that differs from the address the app sees. In GitHub Codespaces the browser
    // uses a forwarded https://<codespace>-<port>.app.github.dev address while the app sees localhost, so the AppHost
    // sets this there; links in emails that point at localhost are rewritten to it. Unset when deployed (the proxy
    // passes the real host name through).
    public Uri? PublicBaseUrl { get; set; }
}
