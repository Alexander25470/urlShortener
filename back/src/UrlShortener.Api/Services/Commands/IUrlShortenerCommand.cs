namespace UrlShortener.Api.Services.Commands;

public interface IUrlShortenerCommand
{
    Task<string> ShortenAsync(string longUrl, CancellationToken ct = default);
    Task RecordClickAsync(string shortCode, CancellationToken ct = default);
}
