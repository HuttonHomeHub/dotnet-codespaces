using PhotoMapper.Web.Components.Pages;

namespace PhotoMapper.Web.Tests;

public sealed class NotFoundTests : BunitContext
{
    [Fact]
    public void RendersNotFoundHeading()
    {
        IRenderedComponent<NotFound> page = Render<NotFound>();

        page.Find("h1").MarkupMatches("<h1>Not found</h1>");
    }
}
