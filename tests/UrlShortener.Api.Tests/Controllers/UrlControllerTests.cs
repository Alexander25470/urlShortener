using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using UrlShortener.Api.Controllers;
using UrlShortener.Api.Models;
using UrlShortener.Api.Services;

namespace UrlShortener.Api.Tests.Controllers;

public class UrlControllerTests
{
    private readonly Mock<IUrlShortenerService> _serviceMock;
    private readonly Mock<IOptions<UrlShortenerOptions>> _optionsMock;
    private readonly UrlController _controller;

    public UrlControllerTests()
    {
        _serviceMock = new Mock<IUrlShortenerService>();

        var options = new UrlShortenerOptions
        {
            BaseUrl = "http://localhost:8080",
            RedirectType = 301
        };
        _optionsMock = new Mock<IOptions<UrlShortenerOptions>>();
        _optionsMock.Setup(o => o.Value).Returns(options);

        _controller = new UrlController(_serviceMock.Object, _optionsMock.Object);
    }

    [Fact]
    public async Task Shorten_ValidRequest_ReturnsOkWithShortUrl()
    {
        const string longUrl = "https://example.com/test";
        const string shortUrl = "http://localhost:8080/0000001";

        _serviceMock
            .Setup(s => s.ShortenAsync(longUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shortUrl);

        var request = new ShortenRequest(longUrl);
        var result = await _controller.Shorten(request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ShortenResponse>(okResult.Value);
        Assert.Equal(shortUrl, response.ShortUrl);
    }

    [Fact]
    public async Task Shorten_ValidRequest_ReturnsOkObjectResult()
    {
        const string longUrl = "https://example.com/valid-url";
        const string shortUrl = "http://localhost:8080/0000001";

        _serviceMock
            .Setup(s => s.ShortenAsync(longUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shortUrl);

        var request = new ShortenRequest(longUrl);
        var result = await _controller.Shorten(request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Redirect_ValidShortCode_ReturnsRedirectResult()
    {
        const string shortCode = "0000001";
        const string longUrl = "https://example.com/redirect-test";

        _serviceMock
            .Setup(s => s.GetLongUrlAsync(shortCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(longUrl);

        var result = await _controller.Redirect(shortCode, CancellationToken.None);

        var redirectResult = Assert.IsType<RedirectResult>(result);
        Assert.Equal(longUrl, redirectResult.Url);
        Assert.True(redirectResult.Permanent);
    }

    [Fact]
    public async Task Redirect_NonExistentShortCode_Returns404()
    {
        const string shortCode = "9999999";

        _serviceMock
            .Setup(s => s.GetLongUrlAsync(shortCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var result = await _controller.Redirect(shortCode, CancellationToken.None);

        var problemResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(404, problemResult.StatusCode);
    }

    [Fact]
    public async Task Redirect_InvalidShortCodeFormat_Returns400()
    {
        const string invalidShortCode = "!!!!!!!";
        var result = await _controller.Redirect(invalidShortCode, CancellationToken.None);

        var problemResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, problemResult.StatusCode);

        _serviceMock.Verify(
            s => s.GetLongUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("abcdef")]
    [InlineData("abcdefgh")]
    [InlineData("abc def")]
    [InlineData("abc+def")]
    [InlineData("")]
    public async Task Redirect_InvalidShortCodeFormats_Returns400(string shortCode)
    {
        var result = await _controller.Redirect(shortCode, CancellationToken.None);

        var problemResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, problemResult.StatusCode);

        _serviceMock.Verify(
            s => s.GetLongUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void Health_ReturnsOk()
    {
        var result = _controller.Health();
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
    }

    [Fact]
    public async Task Redirect_WithRedirectType302_ReturnsNonPermanentRedirect()
    {
        var options302 = new UrlShortenerOptions { RedirectType = 302 };
        var optionsMock302 = new Mock<IOptions<UrlShortenerOptions>>();
        optionsMock302.Setup(o => o.Value).Returns(options302);

        var controller302 = new UrlController(_serviceMock.Object, optionsMock302.Object);

        const string shortCode = "0000001";
        const string longUrl = "https://example.com/temp-redirect";

        _serviceMock
            .Setup(s => s.GetLongUrlAsync(shortCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(longUrl);

        var result = await controller302.Redirect(shortCode, CancellationToken.None);

        var redirectResult = Assert.IsType<RedirectResult>(result);
        Assert.Equal(longUrl, redirectResult.Url);
        Assert.False(redirectResult.Permanent);
    }

    // ---- Analytics ------

    [Fact]
    public async Task GetAllMappings_ReturnsOkWithList()
    {
        var mappings = new List<UrlMapping>
        {
            new() { ShortCode = "0000001", LongUrl = "https://example.com/a", CreatedAt = DateTime.UtcNow }
        };

        _serviceMock
            .Setup(s => s.GetAllMappingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mappings);

        var result = await _controller.GetAllMappings(CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetClickStats_ValidShortCode_ReturnsOkWithBuckets()
    {
        var buckets = new List<ClickBucket>
        {
            new(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), 5)
        };

        _serviceMock
            .Setup(s => s.GetClickStatsAsync("0000001",
                It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                "day", It.IsAny<CancellationToken>()))
            .ReturnsAsync(buckets);

        var result = await _controller.GetClickStats(
            "0000001", null, null, "day", CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
    }

    [Fact]
    public async Task GetClickStats_InvalidShortCode_Returns400()
    {
        var result = await _controller.GetClickStats(
            "!!!!!!!", null, null, "hour", CancellationToken.None);

        var problemResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, problemResult.StatusCode);
    }

    [Fact]
    public async Task GetClickStats_InvalidBucket_Returns400()
    {
        var result = await _controller.GetClickStats(
            "0000001", null, null, "week", CancellationToken.None);

        var problemResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, problemResult.StatusCode);
    }

    [Fact]
    public async Task GetTopUrls_DefaultLimit_ReturnsOk()
    {
        _serviceMock
            .Setup(s => s.GetTopUrlsAsync(10,
                It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TopUrlStat>());

        var result = await _controller.GetTopUrls(
            10, null, null, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetTopUrls_LimitClampedTo100_ReturnsOk()
    {
        _serviceMock
            .Setup(s => s.GetTopUrlsAsync(100,
                It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TopUrlStat>());

        var result = await _controller.GetTopUrls(
            999, null, null, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        _serviceMock.Verify(
            s => s.GetTopUrlsAsync(100,
                It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()), Times.Once);
    }
}
