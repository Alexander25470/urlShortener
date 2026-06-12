using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using UrlShortener.Api.Controllers;
using UrlShortener.Api.Services;

namespace UrlShortener.Api.Tests.Controllers;

public class RedirectControllerTests
{
    private readonly Mock<IUrlShortenerService> _serviceMock;
    private readonly RedirectController _controller301;
    private readonly RedirectController _controller302;

    public RedirectControllerTests()
    {
        _serviceMock = new Mock<IUrlShortenerService>();

        _controller301 = new RedirectController(_serviceMock.Object,
            Options.Create(new UrlShortenerOptions { RedirectType = 301 }));

        _controller302 = new RedirectController(_serviceMock.Object,
            Options.Create(new UrlShortenerOptions { RedirectType = 302 }));
    }

    [Fact]
    public async Task Redirect_ValidShortCode_Returns301()
    {
        _serviceMock.Setup(s => s.GetLongUrlAsync("0000001", It.IsAny<CancellationToken>())).ReturnsAsync("https://example.com/a");

        var result = await _controller301.Redirect("0000001", CancellationToken.None);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("https://example.com/a", redirect.Url);
        Assert.True(redirect.Permanent);
    }

    [Fact]
    public async Task Redirect_With302_ReturnsNonPermanent()
    {
        _serviceMock.Setup(s => s.GetLongUrlAsync("0000001", It.IsAny<CancellationToken>())).ReturnsAsync("https://example.com/b");

        var result = await _controller302.Redirect("0000001", CancellationToken.None);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.False(redirect.Permanent);
    }

    [Fact]
    public async Task Redirect_NonExistentShortCode_Returns404()
    {
        _serviceMock.Setup(s => s.GetLongUrlAsync("9999999", It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);

        var result = await _controller301.Redirect("9999999", CancellationToken.None);

        Assert.Equal(404, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task Redirect_InvalidShortCode_Returns400()
    {
        var result = await _controller301.Redirect("!!!!!!!", CancellationToken.None);

        Assert.Equal(400, Assert.IsType<ObjectResult>(result).StatusCode);
        _serviceMock.Verify(s => s.GetLongUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("abcdefgh")]
    [InlineData("abc def")]
    public async Task Redirect_VariousInvalidFormats_Returns400(string shortCode)
    {
        var result = await _controller301.Redirect(shortCode, CancellationToken.None);
        Assert.Equal(400, Assert.IsType<ObjectResult>(result).StatusCode);
    }
}
