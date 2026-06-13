using System.Diagnostics;
using System.Diagnostics.Metrics;
using MongoDB.Driver;
using UrlShortener.Api.Models;

namespace UrlShortener.Api.Services.Queries;

public class UrlMappingQuery : IUrlMappingQuery
{
    private readonly IMongoCollection<UrlMapping> _urlMappings;
    private readonly Histogram<double> _redirectDuration;

    public UrlMappingQuery(IMongoCollection<UrlMapping> urlMappings, IMeterFactory meterFactory)
    {
        _urlMappings = urlMappings;
        var meter = meterFactory.Create("urlshortener");
        _redirectDuration = meter.CreateHistogram<double>("urlshortener.redirect.duration", unit: "ms");
    }

    public async Task<string?> GetLongUrlAsync(string shortCode, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var mapping = await _urlMappings.Find(m => m.ShortCode == shortCode).FirstOrDefaultAsync(ct);
        sw.Stop();
        _redirectDuration.Record(sw.Elapsed.TotalMilliseconds);
        return mapping?.LongUrl;
    }
}
