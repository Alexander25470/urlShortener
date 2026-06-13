using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using UrlShortener.Api.Services;
using UrlShortener.Api.Services.Commands;
using UrlShortener.Api.Services.Queries;

namespace UrlShortener.Api.Controllers;

[ApiController]
public class UrlController : ControllerBase
{
    private readonly IUrlShortenerCommand _command;
    private readonly IUrlAnalyticsQuery _query;

    public UrlController(IUrlShortenerCommand command, IUrlAnalyticsQuery query)
    {
        _command = command;
        _query = query;
    }

    [HttpPost("api/v1/data/shorten")]
    public async Task<IActionResult> Shorten([FromBody] ShortenRequest request, CancellationToken ct)
    {
        var shortUrl = await _command.ShortenAsync(request.LongUrl, ct);
        return Ok(new ShortenResponse(shortUrl));
    }

    [HttpGet("api/v1/urls")]
    public async Task<IActionResult> GetAllMappings(CancellationToken ct)
    {
        var mappings = await _query.GetAllMappingsAsync(ct);
        return Ok(mappings.Select(m => new { shortCode = m.ShortCode, longUrl = m.LongUrl, createdAt = m.CreatedAt }));
    }

    [HttpGet("api/v1/{shortCode}/clicks")]
    public async Task<IActionResult> GetClickStats(
        string shortCode, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null,
        [FromQuery] string bucket = "hour", CancellationToken ct = default)
    {
        if (!Base62Converter.TryDecode(shortCode, out _))
            return Problem(statusCode: 400, title: "Invalid short code format.");
        if (bucket is not ("minute" or "hour" or "day"))
            return Problem(statusCode: 400, title: "Invalid bucket unit.");

        var stats = await _query.GetClickStatsAsync(shortCode, from ?? DateTime.UtcNow.AddDays(-30), to ?? DateTime.UtcNow, bucket, ct);
        return Ok(new { shortCode, from, to, bucket, data = stats });
    }

    [HttpGet("api/v1/analytics/top")]
    public async Task<IActionResult> GetTopUrls(
        [FromQuery] int limit = 10, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 100);
        var stats = await _query.GetTopUrlsAsync(limit, from ?? DateTime.UtcNow.AddDays(-7), to ?? DateTime.UtcNow, ct);
        return Ok(new { from, to, limit, data = stats });
    }
}

public record ShortenRequest([Required, Url] string LongUrl);
public record ShortenResponse(string ShortUrl);
