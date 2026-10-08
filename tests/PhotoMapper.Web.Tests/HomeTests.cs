using PhotoMapper.Web.Components.Pages;

namespace PhotoMapper.Web.Tests;

public sealed class HomeTests : BunitContext
{
    [Fact]
    public void RendersAppHeading()
    {
        IRenderedComponent<Home> page = Render<Home>();

        page.Find("h1").MarkupMatches("<h1>PhotoMapper</h1>");
    }
}
