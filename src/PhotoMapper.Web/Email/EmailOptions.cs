namespace PhotoMapper.Web.Email;

// The "Email" configuration section.
internal sealed class EmailOptions
{
    // Sender shown on account emails. When deployed, use an address your mail provider allows you to send from.
    public string From { get; set; } = "PhotoMapper <no-reply@photomapper.local>";
}
