using Microsoft.Extensions.Options;
using Moq;
using UrlShortener.Api.Controllers;
using UrlShortener.Api.Models;
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
        _serviceMock
            .Setup(s => s.ShortenAsync(null!, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentNullException());

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _service.ShortenAsync(null!, CancellationToken.None));
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

        Assert.Equal(expectedShortUrl, result);
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

        var result1 = await _service.ShortenAsync(longUrl, CancellationToken.None);
        var result2 = await _service.ShortenAsync(longUrl, CancellationToken.None);

        Assert.Equal(expectedShortUrl, result1);
        Assert.Equal(expectedShortUrl, result2);
        Assert.Equal(result1, result2);
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

        Assert.Equal(expectedLongUrl, result);
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

        Assert.Null(result);
    }

    [Fact]
    public async Task GetLongUrlAsync_RecordsClickEvent()
    {
        const string shortCode = "0000001";
        const string expectedLongUrl = "https://example.com/click-test";

        _serviceMock
            .Setup(s => s.GetLongUrlAsync(shortCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedLongUrl);

        await _service.GetLongUrlAsync(shortCode, CancellationToken.None);
        await _service.GetLongUrlAsync(shortCode, CancellationToken.None);

        _serviceMock.Verify(s => s.GetLongUrlAsync(shortCode, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    // ── Analytics ───────────────────────────────────────────────

    [Fact]
    public async Task GetAllMappingsAsync_ReturnsMappingList()
    {
        var mappings = new List<UrlMapping>
        {
            new() { ShortCode = "0000002", LongUrl = "https://example.com/b", CreatedAt = DateTime.UtcNow },
            new() { ShortCode = "0000001", LongUrl = "https://example.com/a", CreatedAt = DateTime.UtcNow.AddMinutes(-5) }
        };

        _serviceMock
            .Setup(s => s.GetAllMappingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mappings);

        var result = await _service.GetAllMappingsAsync(CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal(mappings[0].ShortCode, result[0].ShortCode);
        _serviceMock.Verify(s => s.GetAllMappingsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetClickStatsAsync_ReturnsBucketedResults()
    {
        var buckets = new List<ClickBucket>
        {
            new(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), 5),
            new(new DateTime(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc), 3)
        };

        _serviceMock
            .Setup(s => s.GetClickStatsAsync("0000001",
                It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                "day", It.IsAny<CancellationToken>()))
            .ReturnsAsync(buckets);

        var from = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);
        var result = await _service.GetClickStatsAsync("0000001", from, to, "day", CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), result[0].Timestamp);
        Assert.Equal(5, result[0].Count);
        Assert.Equal(3, result[1].Count);
    }

    [Fact]
    public async Task GetClickStatsAsync_NoData_ReturnsEmpty()
    {
        _serviceMock
            .Setup(s => s.GetClickStatsAsync(It.IsAny<string>(),
                It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ClickBucket>());

        var result = await _service.GetClickStatsAsync("9999999",
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow,
            "hour", CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetTopUrlsAsync_ReturnsRankedResults()
    {
        var stats = new List<TopUrlStat>
        {
            new("0000001", "https://example.com/a", 10),
            new("0000002", "https://example.com/b", 5)
        };

        _serviceMock
            .Setup(s => s.GetTopUrlsAsync(10,
                It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(stats);

        var result = await _service.GetTopUrlsAsync(10,
            DateTime.UtcNow.AddDays(-7), DateTime.UtcNow,
            CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal("0000001", result[0].ShortCode);
        Assert.Equal(10, result[0].ClickCount);
        Assert.Equal("https://example.com/a", result[0].LongUrl);
        Assert.Equal(5, result[1].ClickCount);
    }

    [Fact]
    public async Task GetTopUrlsAsync_NoData_ReturnsEmpty()
    {
        _serviceMock
            .Setup(s => s.GetTopUrlsAsync(It.IsAny<int>(),
                It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TopUrlStat>());

        var result = await _service.GetTopUrlsAsync(10,
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow,
            CancellationToken.None);

        Assert.Empty(result);
    }
}
