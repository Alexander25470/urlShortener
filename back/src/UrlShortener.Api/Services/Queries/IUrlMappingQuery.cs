namespace UrlShortener.Api.Services.Queries;

public interface IUrlMappingQuery
{
    Task<string?> GetLongUrlAsync(string shortCode, CancellationToken ct = default);
}
