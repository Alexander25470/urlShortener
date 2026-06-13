using Moq;
using UrlShortener.Api.Models;
using UrlShortener.Api.Services;
using UrlShortener.Api.Services.Commands;
using UrlShortener.Api.Services.Queries;

namespace UrlShortener.Api.Tests.Services;

public class UrlShortenerCommandTests
{
    private readonly Mock<IUrlShortenerCommand> _commandMock;

    public UrlShortenerCommandTests()
    {
        _commandMock = new Mock<IUrlShortenerCommand>();
    }

    [Fact]
    public async Task ShortenAsync_ReturnsShortUrl()
    {
        _commandMock.Setup(c => c.ShortenAsync("https://ex.com", It.IsAny<CancellationToken>())).ReturnsAsync("http://localhost:8080/0000001");
        Assert.Equal("http://localhost:8080/0000001", await _commandMock.Object.ShortenAsync("https://ex.com"));
    }

    [Fact]
    public async Task RecordClickAsync_Completes()
    {
        _commandMock.Setup(c => c.RecordClickAsync("0000001", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        await _commandMock.Object.RecordClickAsync("0000001");
        _commandMock.Verify(c => c.RecordClickAsync("0000001", It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class UrlAnalyticsQueryTests
{
    private readonly Mock<IUrlAnalyticsQuery> _queryMock;

    public UrlAnalyticsQueryTests()
    {
        _queryMock = new Mock<IUrlAnalyticsQuery>();
    }

    [Fact]
    public async Task GetAllMappingsAsync_ReturnsList()
    {
        _queryMock.Setup(q => q.GetAllMappingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<UrlMapping> { new() { ShortCode = "0000001" } });
        var r = await _queryMock.Object.GetAllMappingsAsync();
        Assert.Single(r);
    }

    [Fact]
    public async Task GetClickStatsAsync_ReturnsBuckets()
    {
        _queryMock.Setup(q => q.GetClickStatsAsync("0000001", It.IsAny<DateTime>(), It.IsAny<DateTime>(), "day", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ClickBucket> { new(DateTime.UtcNow, 5) });
        var r = await _queryMock.Object.GetClickStatsAsync("0000001", DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, "day");
        Assert.Single(r);
    }

    [Fact]
    public async Task GetTopUrlsAsync_ReturnsRanked()
    {
        _queryMock.Setup(q => q.GetTopUrlsAsync(10, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TopUrlStat> { new("0000001", "https://ex.com", 10) });
        var r = await _queryMock.Object.GetTopUrlsAsync(10, DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);
        Assert.Single(r);
        Assert.Equal(10, r[0].ClickCount);
    }
}

public class UrlMappingQueryTests
{
    private readonly Mock<IUrlMappingQuery> _queryMock;

    public UrlMappingQueryTests()
    {
        _queryMock = new Mock<IUrlMappingQuery>();
    }

    [Fact]
    public async Task GetLongUrlAsync_Existing_ReturnsUrl()
    {
        _queryMock.Setup(q => q.GetLongUrlAsync("0000001", It.IsAny<CancellationToken>())).ReturnsAsync("https://ex.com/a");
        Assert.Equal("https://ex.com/a", await _queryMock.Object.GetLongUrlAsync("0000001"));
    }

    [Fact]
    public async Task GetLongUrlAsync_NotFound_ReturnsNull()
    {
        _queryMock.Setup(q => q.GetLongUrlAsync("9999999", It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);
        Assert.Null(await _queryMock.Object.GetLongUrlAsync("9999999"));
    }
}
