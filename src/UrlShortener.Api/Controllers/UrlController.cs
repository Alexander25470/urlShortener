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
}

// ── DTOs ───────────────────────────────────────────────────────

public record ShortenRequest([Required, Url] string LongUrl);

public record ShortenResponse(string ShortUrl);
