using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using PhotoMapper.Web.Components.Account.Shared;

namespace PhotoMapper.Web.Tests;

public sealed class PasswordRequirementsTests : BunitContext
{
    [Fact]
    public void DescribesIdentitysDefaultRules()
    {
        Services.AddSingleton(Options.Create(new IdentityOptions()));

        IRenderedComponent<PasswordRequirements> hint = Render<PasswordRequirements>();

        hint.MarkupMatches("""
            <p class="auth-hint" id="password-requirements">At least 6 characters, including an uppercase letter, a lowercase letter, a number and a symbol.</p>
            """);
    }

    [Theory]
    [InlineData(false, false, "At least 12 characters.")]
    [InlineData(true, false, "At least 12 characters, including a number.")]
    [InlineData(true, true, "At least 12 characters, including a number and a symbol.")]
    public void FollowsTheConfiguredRules(bool requireDigit, bool requireSymbol, string expected)
    {
        IdentityOptions options = new();
        options.Password.RequiredLength = 12;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireDigit = requireDigit;
        options.Password.RequireNonAlphanumeric = requireSymbol;
        Services.AddSingleton(Options.Create(options));

        IRenderedComponent<PasswordRequirements> hint = Render<PasswordRequirements>(parameters => parameters.Add(p => p.Id, "new-password-rules"));

        hint.MarkupMatches($"""<p class="auth-hint" id="new-password-rules">{expected}</p>""");
    }
}
