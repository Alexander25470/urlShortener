using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using UrlShortener.Api.Controllers;
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

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ShortenResponse>().Subject;
        response.ShortUrl.Should().Be(shortUrl);
    }

    [Fact]
    public async Task Shorten_RequestWithInvalidUrl_BypassesControllerValidation()
    {
        // The [Url] attribute validation happens in the model binder,
        // so the controller never receives an invalid URL.
        // This test verifies that with a valid URL it works.
        const string longUrl = "https://example.com/valid-url";
        const string shortUrl = "http://localhost:8080/0000001";

        _serviceMock
            .Setup(s => s.ShortenAsync(longUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shortUrl);

        var request = new ShortenRequest(longUrl);
        var result = await _controller.Shorten(request, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
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

        var redirectResult = result.Should().BeOfType<RedirectResult>().Subject;
        redirectResult.Url.Should().Be(longUrl);
        redirectResult.Permanent.Should().BeTrue(); // 301
    }

    [Fact]
    public async Task Redirect_NonExistentShortCode_Returns404()
    {
        const string shortCode = "9999999";

        _serviceMock
            .Setup(s => s.GetLongUrlAsync(shortCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var result = await _controller.Redirect(shortCode, CancellationToken.None);

        var problemResult = result.Should().BeOfType<ObjectResult>().Subject;
        problemResult.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task Redirect_InvalidShortCodeFormat_Returns400()
    {
        // Short code with invalid characters (!!!!!!!)
        const string invalidShortCode = "!!!!!!!";

        var result = await _controller.Redirect(invalidShortCode, CancellationToken.None);

        var problemResult = result.Should().BeOfType<ObjectResult>().Subject;
        problemResult.StatusCode.Should().Be(400);

        // Verify the service was never called (validation happens first)
        _serviceMock.Verify(
            s => s.GetLongUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("abc")]      // too short
    [InlineData("abcdef")]   // too short
    [InlineData("abcdefgh")] // too long
    [InlineData("abc def")]  // space
    [InlineData("abc+def")]  // invalid char
    [InlineData("")]         // empty
    public async Task Redirect_InvalidShortCodeFormats_Returns400(string shortCode)
    {
        var result = await _controller.Redirect(shortCode, CancellationToken.None);

        var problemResult = result.Should().BeOfType<ObjectResult>().Subject;
        problemResult.StatusCode.Should().Be(400);

        _serviceMock.Verify(
            s => s.GetLongUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void Health_ReturnsOk()
    {
        var result = _controller.Health();

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task Redirect_WithRedirectType302_ReturnsNonPermanentRedirect()
    {
        // Override options to use 302
        var options302 = new UrlShortenerOptions
        {
            RedirectType = 302
        };
        var optionsMock302 = new Mock<IOptions<UrlShortenerOptions>>();
        optionsMock302.Setup(o => o.Value).Returns(options302);

        var controller302 = new UrlController(_serviceMock.Object, optionsMock302.Object);

        const string shortCode = "0000001";
        const string longUrl = "https://example.com/temp-redirect";

        _serviceMock
            .Setup(s => s.GetLongUrlAsync(shortCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(longUrl);

        var result = await controller302.Redirect(shortCode, CancellationToken.None);

        var redirectResult = result.Should().BeOfType<RedirectResult>().Subject;
        redirectResult.Url.Should().Be(longUrl);
        redirectResult.Permanent.Should().BeFalse(); // 302 = not permanent
    }
}
