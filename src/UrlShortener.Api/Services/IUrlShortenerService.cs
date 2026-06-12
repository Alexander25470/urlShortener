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
}
