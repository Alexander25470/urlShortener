using UrlShortener.Api.Models;

namespace UrlShortener.Api.Services.Queries;

public interface IUrlAnalyticsQuery
{
    Task<IReadOnlyList<UrlMapping>> GetAllMappingsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ClickBucket>> GetClickStatsAsync(string shortCode, DateTime from, DateTime to, string bucket, CancellationToken ct = default);
    Task<IReadOnlyList<TopUrlStat>> GetTopUrlsAsync(int limit, DateTime from, DateTime to, CancellationToken ct = default);
}
