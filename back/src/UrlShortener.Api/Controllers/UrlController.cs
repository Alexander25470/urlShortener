using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using UrlShortener.Api.Services;

namespace UrlShortener.Api.Controllers;

[ApiController]
public class UrlController : ControllerBase
{
    private readonly IUrlShortenerService _urlShortener;
    private readonly IOptions<UrlShortenerOptions> _options;

    public UrlController(IUrlShortenerService urlShortener, IOptions<UrlShortenerOptions> options)
    {
        _urlShortener = urlShortener;
        _options = options;
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

    /// <summary>
    /// Redirects a short code to its original long URL.
    /// Configurable between 301 (permanent) and 302 (temporary) via UrlShortener:RedirectType.
    /// </summary>
    [HttpGet("api/v1/{shortCode}")]
    [HttpGet("{shortCode}")]
    public async Task<IActionResult> Redirect(string shortCode, CancellationToken ct)
    {
        if (!Base62Converter.TryDecode(shortCode, out _))
            return Problem(
                statusCode: 400,
                title: "Invalid short code format.",
                detail: "Short code must be exactly 7 characters containing [0-9, a-z, A-Z].");

        var longUrl = await _urlShortener.GetLongUrlAsync(shortCode, ct);

        if (longUrl is null)
            return Problem(
                statusCode: 404,
                title: "Short code not found.",
                detail: $"No URL mapping exists for short code '{shortCode}'.");

        var isPermanent = _options.Value.RedirectType != 302;
        return new RedirectResult(longUrl, isPermanent);
    }

    /// <summary>
    /// Health check endpoint for Docker healthcheck and load balancer probes.
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health() => Ok(new { status = "healthy" });

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
