using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

using PhotoMapper.Web.Components.Account.Shared;

namespace PhotoMapper.Web.Tests;

public sealed class RedirectToLoginTests : BunitContext
{
    [Fact]
    public void SendsTheUserToLoginWithTheCurrentPageAsReturnUrl()
    {
        NavigationManager navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("account/manage");

        Render<RedirectToLogin>();

        Assert.Equal("http://localhost/Account/Login?returnUrl=http%3A%2F%2Flocalhost%2Faccount%2Fmanage", navigation.Uri);
    }
}
