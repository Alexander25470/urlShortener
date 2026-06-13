using Microsoft.AspNetCore.Mvc;
using Moq;
using UrlShortener.Api.Controllers;
using UrlShortener.Api.Models;
using UrlShortener.Api.Services;
using UrlShortener.Api.Services.Commands;
using UrlShortener.Api.Services.Queries;

namespace UrlShortener.Api.Tests.Controllers;

public class UrlControllerTests
{
    private readonly Mock<IUrlShortenerCommand> _commandMock;
    private readonly Mock<IUrlAnalyticsQuery> _queryMock;
    private readonly UrlController _controller;

    public UrlControllerTests()
    {
        _commandMock = new Mock<IUrlShortenerCommand>();
        _queryMock = new Mock<IUrlAnalyticsQuery>();
        _controller = new UrlController(_commandMock.Object, _queryMock.Object);
    }

    [Fact]
    public async Task Shorten_ReturnsOk()
    {
        _commandMock.Setup(c => c.ShortenAsync("https://example.com", It.IsAny<CancellationToken>())).ReturnsAsync("http://localhost:8080/0000001");
        var result = await _controller.Shorten(new ShortenRequest("https://example.com"), CancellationToken.None);
        Assert.Equal("http://localhost:8080/0000001", Assert.IsType<ShortenResponse>(Assert.IsType<OkObjectResult>(result).Value).ShortUrl);
    }

    [Fact]
    public async Task GetAllMappings_ReturnsOk()
    {
        _queryMock.Setup(q => q.GetAllMappingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<UrlMapping> { new() { ShortCode = "0000001" } });
        Assert.IsType<OkObjectResult>(await _controller.GetAllMappings(CancellationToken.None));
    }

    [Fact]
    public async Task GetClickStats_InvalidBucket_Returns400()
    {
        Assert.Equal(400, Assert.IsType<ObjectResult>(await _controller.GetClickStats("0000001", null, null, "week", CancellationToken.None)).StatusCode);
    }

    [Fact]
    public async Task GetTopUrls_ReturnsOk()
    {
        _queryMock.Setup(q => q.GetTopUrlsAsync(10, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<TopUrlStat>());
        Assert.IsType<OkObjectResult>(await _controller.GetTopUrls(10, null, null, CancellationToken.None));
    }
}
