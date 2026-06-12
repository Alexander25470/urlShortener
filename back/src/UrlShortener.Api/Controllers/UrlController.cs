using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using UrlShortener.Api.Services;

namespace UrlShortener.Api.Controllers;

[ApiController]
public class UrlController : ControllerBase
{
    private readonly IUrlShortenerService _urlShortener;

    public UrlController(IUrlShortenerService urlShortener)
    {
        _urlShortener = urlShortener;
    }

    /// <summary>
    /// Shortens a long URL. Idempotent: same longUrl always returns the same shortUrl.
    /// </summary>
    [HttpPost("api/v1/data/shorten")]
    public async Task<IActionResult> Shorten([FromBody] ShortenRequest request, CancellationToken ct)
    {
        var shortUrl = await _urlShortener.ShortenAsync(request.LongUrl, ct);
        return Ok(new ShortenResponse(shortUrl));
    }

    // ── Analytics ───────────────────────────────────────────────

    /// <summary>
    /// Returns all shortened URLs, newest first.
    /// </summary>
    [HttpGet("api/v1/urls")]
    public async Task<IActionResult> GetAllMappings(CancellationToken ct)
    {
        var mappings = await _urlShortener.GetAllMappingsAsync(ct);
        return Ok(mappings.Select(m => new
        {
            shortCode = m.ShortCode,
            longUrl = m.LongUrl,
            createdAt = m.CreatedAt
        }));
    }

    /// <summary>
    /// Returns click statistics for a shortCode, bucketed by time unit.
    /// Bucket: "minute", "hour" (default), or "day".
    /// </summary>
    [HttpGet("api/v1/{shortCode}/clicks")]
    public async Task<IActionResult> GetClickStats(
        string shortCode,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string bucket = "hour",
        CancellationToken ct = default)
    {
        if (!Base62Converter.TryDecode(shortCode, out _))
            return Problem(statusCode: 400, title: "Invalid short code format.");

        if (bucket is not ("minute" or "hour" or "day"))
            return Problem(
                statusCode: 400,
                title: "Invalid bucket unit.",
                detail: "Bucket must be 'minute', 'hour', or 'day'.");

        var fromDate = from ?? DateTime.UtcNow.AddDays(-30);
        var toDate = to ?? DateTime.UtcNow;

        var stats = await _urlShortener.GetClickStatsAsync(shortCode, fromDate, toDate, bucket, ct);
        return Ok(new { shortCode, from = fromDate, to = toDate, bucket, data = stats });
    }

    /// <summary>
    /// Returns the top most clicked URLs in the given time range.
    /// </summary>
    [HttpGet("api/v1/analytics/top")]
    public async Task<IActionResult> GetTopUrls(
        [FromQuery] int limit = 10,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 100);
        var fromDate = from ?? DateTime.UtcNow.AddDays(-7);
        var toDate = to ?? DateTime.UtcNow;

        var stats = await _urlShortener.GetTopUrlsAsync(limit, fromDate, toDate, ct);
        return Ok(new { from = fromDate, to = toDate, limit, data = stats });
    }
}

// ── DTOs ───────────────────────────────────────────────────────

public record ShortenRequest([Required, Url] string LongUrl);

public record ShortenResponse(string ShortUrl);
