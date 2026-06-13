using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using UrlShortener.Api.Services;
using UrlShortener.Api.Services.Commands;
using UrlShortener.Api.Services.Queries;

namespace UrlShortener.Api.Controllers;

[ApiController]
public class RedirectController : ControllerBase
{
    private readonly IUrlShortenerCommand _command;
    private readonly IUrlMappingQuery _urlMappingQuery;
    private readonly IOptions<UrlShortenerOptions> _options;

    public RedirectController(IUrlShortenerCommand command, IUrlMappingQuery urlMappingQuery, IOptions<UrlShortenerOptions> options)
    {
        _command = command;
        _urlMappingQuery = urlMappingQuery;
        _options = options;
    }

    /// <summary>
    /// Redirects a short code to its original long URL.
    /// </summary>
    [HttpGet("api/v1/{shortCode}")]
    [HttpGet("{shortCode}")]
    public async Task<IActionResult> Redirect(string shortCode, CancellationToken ct)
    {
        if (!Base62Converter.TryDecode(shortCode, out _))
            return Problem(statusCode: 400, title: "Invalid short code format.",
                detail: "Short code must be exactly 7 characters containing [0-9, a-z, A-Z].");

        var longUrl = await _urlMappingQuery.GetLongUrlAsync(shortCode, ct);

        if (longUrl is null)
            return Problem(statusCode: 404, title: "Short code not found.",
                detail: $"No URL mapping exists for short code '{shortCode}'.");

        await _command.RecordClickAsync(shortCode, ct);
        return new RedirectResult(longUrl, _options.Value.RedirectType != 302);
    }
}
