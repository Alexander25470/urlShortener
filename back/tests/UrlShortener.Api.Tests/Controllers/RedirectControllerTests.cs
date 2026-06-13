using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using UrlShortener.Api.Controllers;
using UrlShortener.Api.Services.Commands;
using UrlShortener.Api.Services.Queries;

namespace UrlShortener.Api.Tests.Controllers;

public class RedirectControllerTests
{
    private readonly Mock<IUrlShortenerCommand> _commandMock;
    private readonly Mock<IUrlMappingQuery> _urlMappingQueryMock;

    public RedirectControllerTests()
    {
        _commandMock = new Mock<IUrlShortenerCommand>();
        _urlMappingQueryMock = new Mock<IUrlMappingQuery>();
    }

    [Fact]
    public async Task Redirect_Valid_Returns301()
    {
        _urlMappingQueryMock.Setup(q => q.GetLongUrlAsync("0000001", It.IsAny<CancellationToken>())).ReturnsAsync("https://ex.com/a");
        _commandMock.Setup(c => c.RecordClickAsync("0000001", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var ctrl = new RedirectController(_commandMock.Object, _urlMappingQueryMock.Object, Options.Create(new UrlShortenerOptions { RedirectType = 301 }));
        var r = Assert.IsType<RedirectResult>(await ctrl.Redirect("0000001", CancellationToken.None));
        Assert.True(r.Permanent);
        Assert.Equal("https://ex.com/a", r.Url);
        _commandMock.Verify(c => c.RecordClickAsync("0000001", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Redirect_With302_ReturnsNonPermanent()
    {
        _urlMappingQueryMock.Setup(q => q.GetLongUrlAsync("0000001", It.IsAny<CancellationToken>())).ReturnsAsync("https://ex.com/b");
        var ctrl = new RedirectController(_commandMock.Object, _urlMappingQueryMock.Object, Options.Create(new UrlShortenerOptions { RedirectType = 302 }));
        Assert.False(Assert.IsType<RedirectResult>(await ctrl.Redirect("0000001", CancellationToken.None)).Permanent);
    }

    [Fact]
    public async Task Redirect_NotFound_Returns404()
    {
        _urlMappingQueryMock.Setup(q => q.GetLongUrlAsync("9999999", It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);
        var ctrl = new RedirectController(_commandMock.Object, _urlMappingQueryMock.Object, Options.Create(new UrlShortenerOptions()));
        Assert.Equal(404, Assert.IsType<ObjectResult>(await ctrl.Redirect("9999999", CancellationToken.None)).StatusCode);
    }

    [Fact]
    public async Task Redirect_InvalidCode_Returns400()
    {
        var ctrl = new RedirectController(_commandMock.Object, _urlMappingQueryMock.Object, Options.Create(new UrlShortenerOptions()));
        Assert.Equal(400, Assert.IsType<ObjectResult>(await ctrl.Redirect("!!!!!!!", CancellationToken.None)).StatusCode);
        _urlMappingQueryMock.Verify(q => q.GetLongUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
