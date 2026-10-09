using System.Diagnostics;

using PhotoMapper.Web.Components.Pages;

namespace PhotoMapper.Web.Tests;

public sealed class ErrorTests : BunitContext
{
    [Fact]
    public void ShowsTheTraceIdOfTheFailedRequestForSupport()
    {
        using Activity request = new Activity("request").Start();

        IRenderedComponent<Error> page = Render<Error>();

        Assert.Contains("An error occurred while processing your request.", page.Markup, StringComparison.Ordinal);
        page.Find("code").MarkupMatches($"<code>{request.Id}</code>");
    }
}
