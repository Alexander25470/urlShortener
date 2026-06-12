using Microsoft.AspNetCore.Mvc;
using Moq;
using UrlShortener.Api.Controllers;
using UrlShortener.Api.Models;
using UrlShortener.Api.Services;

namespace UrlShortener.Api.Tests.Controllers;

public class UrlControllerTests
{
    private readonly Mock<IUrlShortenerService> _serviceMock;
    private readonly UrlController _controller;

    public UrlControllerTests()
    {
        _serviceMock = new Mock<IUrlShortenerService>();
        _controller = new UrlController(_serviceMock.Object);
    }

    [Fact]
    public async Task Shorten_ValidRequest_ReturnsOkWithShortUrl()
    {
        const string longUrl = "https://example.com/test";
        const string shortUrl = "http://localhost:8080/0000001";
        _serviceMock.Setup(s => s.ShortenAsync(longUrl, It.IsAny<CancellationToken>())).ReturnsAsync(shortUrl);

        var result = await _controller.Shorten(new ShortenRequest(longUrl), CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(shortUrl, Assert.IsType<ShortenResponse>(okResult.Value).ShortUrl);
    }

    [Fact]
    public async Task GetAllMappings_ReturnsOk()
    {
        _serviceMock.Setup(s => s.GetAllMappingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UrlMapping> { new() { ShortCode = "0000001" } });

        var result = await _controller.GetAllMappings(CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetClickStats_ValidShortCode_ReturnsOk()
    {
        _serviceMock.Setup(s => s.GetClickStatsAsync("0000001", It.IsAny<DateTime>(), It.IsAny<DateTime>(), "day", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ClickBucket> { new(DateTime.UtcNow, 5) });

        var result = await _controller.GetClickStats("0000001", null, null, "day", CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetClickStats_InvalidShortCode_Returns400()
    {
        var result = await _controller.GetClickStats("!!!!!!!", null, null, "hour", CancellationToken.None);
        Assert.Equal(400, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task GetClickStats_InvalidBucket_Returns400()
    {
        var result = await _controller.GetClickStats("0000001", null, null, "week", CancellationToken.None);
        Assert.Equal(400, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task GetTopUrls_DefaultLimit_ReturnsOk()
    {
        _serviceMock.Setup(s => s.GetTopUrlsAsync(10, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TopUrlStat>());

        var result = await _controller.GetTopUrls(10, null, null, CancellationToken.None);
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetTopUrls_LimitClampedTo100_ReturnsOk()
    {
        _serviceMock.Setup(s => s.GetTopUrlsAsync(100, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TopUrlStat>());

        var result = await _controller.GetTopUrls(999, null, null, CancellationToken.None);
        Assert.IsType<OkObjectResult>(result);
    }
}
