using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using UrlShortener.Api.Services;

namespace UrlShortener.Api.Controllers;

[ApiController]
public class RedirectController : ControllerBase
{
    private readonly IUrlShortenerService _urlShortener;
    private readonly IOptions<UrlShortenerOptions> _options;

    public RedirectController(IUrlShortenerService urlShortener, IOptions<UrlShortenerOptions> options)
    {
        _urlShortener = urlShortener;
        _options = options;
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
}
