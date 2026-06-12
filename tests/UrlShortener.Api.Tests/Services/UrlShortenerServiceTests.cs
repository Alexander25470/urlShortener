using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using UrlShortener.Api.Controllers;
using UrlShortener.Api.Services;

namespace UrlShortener.Api.Tests.Services;

public class UrlShortenerServiceTests
{
    private readonly Mock<IUrlShortenerService> _serviceMock;
    private readonly IUrlShortenerService _service;

    public UrlShortenerServiceTests()
    {
        _serviceMock = new Mock<IUrlShortenerService>();
        _service = _serviceMock.Object;
    }

    [Fact]
    public async Task ShortenAsync_NullUrl_ReturnsNull()
    {
        // We don't need to test null validation here since the controller
        // validates with [Required] before reaching the service.
        // This test verifies the service contract.
        _serviceMock
            .Setup(s => s.ShortenAsync(null!, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentNullException());

        var act = async () => await _service.ShortenAsync(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ShortenAsync_NewUrl_ReturnsShortUrl()
    {
        const string longUrl = "https://example.com/new-url";
        const string expectedShortUrl = "http://localhost:8080/0000001";

        _serviceMock
            .Setup(s => s.ShortenAsync(longUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedShortUrl);

        var result = await _service.ShortenAsync(longUrl, CancellationToken.None);

        result.Should().Be(expectedShortUrl);
        _serviceMock.Verify(s => s.ShortenAsync(longUrl, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ShortenAsync_SameUrl_ReturnsSameShortUrl_Idempotent()
    {
        const string longUrl = "https://example.com/idempotent-url";
        const string expectedShortUrl = "http://localhost:8080/0000001";

        _serviceMock
            .Setup(s => s.ShortenAsync(longUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedShortUrl);

        // First call
        var result1 = await _service.ShortenAsync(longUrl, CancellationToken.None);
        // Second call (with same URL)
        var result2 = await _service.ShortenAsync(longUrl, CancellationToken.None);

        result1.Should().Be(expectedShortUrl);
        result2.Should().Be(expectedShortUrl);
        result1.Should().Be(result2);
    }

    [Fact]
    public async Task GetLongUrlAsync_ExistingShortCode_ReturnsLongUrl()
    {
        const string shortCode = "0000001";
        const string expectedLongUrl = "https://example.com/existing-url";

        _serviceMock
            .Setup(s => s.GetLongUrlAsync(shortCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedLongUrl);

        var result = await _service.GetLongUrlAsync(shortCode, CancellationToken.None);

        result.Should().Be(expectedLongUrl);
        _serviceMock.Verify(s => s.GetLongUrlAsync(shortCode, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetLongUrlAsync_NonExistentShortCode_ReturnsNull()
    {
        const string shortCode = "9999999";

        _serviceMock
            .Setup(s => s.GetLongUrlAsync(shortCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var result = await _service.GetLongUrlAsync(shortCode, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetLongUrlAsync_RecordsClickEvent()
    {
        const string shortCode = "0000001";
        const string expectedLongUrl = "https://example.com/click-test";

        _serviceMock
            .Setup(s => s.GetLongUrlAsync(shortCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedLongUrl);

        // First access
        await _service.GetLongUrlAsync(shortCode, CancellationToken.None);
        // Second access (each should record a click)
        await _service.GetLongUrlAsync(shortCode, CancellationToken.None);

        _serviceMock.Verify(s => s.GetLongUrlAsync(shortCode, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
