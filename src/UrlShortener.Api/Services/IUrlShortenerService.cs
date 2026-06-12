using UrlShortener.Api.Models;

namespace UrlShortener.Api.Services;

/// <summary>
/// Deep module: simple interface hiding all complexity (ID generation,
/// base62 conversion, idempotency, MongoDB persistence, click tracking).
/// </summary>
public interface IUrlShortenerService
{
    /// <summary>
    /// Shortens a long URL. Idempotent: returns the same shortCode
    /// if the long URL has already been shortened.
    /// </summary>
    Task<string> ShortenAsync(string longUrl, CancellationToken ct = default);

    /// <summary>
    /// Resolves a shortCode to its original long URL.
    /// Returns null if not found.
    /// Also records a click event for analytics.
    /// </summary>
    Task<string?> GetLongUrlAsync(string shortCode, CancellationToken ct = default);

    /// <summary>
    /// Returns all URL mappings, newest first.
    /// </summary>
    Task<IReadOnlyList<UrlMapping>> GetAllMappingsAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns click stats for a shortCode, bucketed by the specified time unit.
    /// Bucket values: "minute", "hour", or "day".
    /// </summary>
    Task<IReadOnlyList<ClickBucket>> GetClickStatsAsync(
        string shortCode, DateTime from, DateTime to, string bucket, CancellationToken ct = default);

    /// <summary>
    /// Returns the top N most clicked URLs in the given time range.
    /// </summary>
    Task<IReadOnlyList<TopUrlStat>> GetTopUrlsAsync(
        int limit, DateTime from, DateTime to, CancellationToken ct = default);
}

// ── DTOs ───────────────────────────────────────────────────────

public record ClickBucket(DateTime Timestamp, long Count);

public record TopUrlStat(string ShortCode, string LongUrl, long ClickCount);
