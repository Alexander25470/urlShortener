using System.Diagnostics;
using System.Diagnostics.Metrics;
using MongoDB.Driver;
using UrlShortener.Api.Models;

namespace UrlShortener.Api.Services.Commands;

public class UrlShortenerCommand : IUrlShortenerCommand
{
    private readonly IMongoCollection<UrlMapping> _urlMappings;
    private readonly IMongoCollection<ClickEvent> _clicks;
    private readonly IMongoCollection<CounterDoc> _counters;
    private readonly string _baseUrl;
    private readonly Histogram<double> _shortenDuration;

    public UrlShortenerCommand(
        IMongoCollection<UrlMapping> urlMappings,
        IMongoCollection<ClickEvent> clicks,
        IMongoClient mainClient,
        string baseUrl,
        IMeterFactory meterFactory)
    {
        _urlMappings = urlMappings;
        _clicks = clicks;
        _counters = mainClient.GetDatabase("urlshortener")
            .GetCollection<CounterDoc>("counters");
        _baseUrl = baseUrl.TrimEnd('/');

        var meter = meterFactory.Create("urlshortener");
        _shortenDuration = meter.CreateHistogram<double>("urlshortener.shorten.duration", unit: "ms");
    }

    public async Task<string> ShortenAsync(string longUrl, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        var existing = await _urlMappings.Find(m => m.LongUrl == longUrl).FirstOrDefaultAsync(ct);
        if (existing is not null)
        {
            sw.Stop();
            _shortenDuration.Record(sw.Elapsed.TotalMilliseconds);
            return $"{_baseUrl}/{existing.ShortCode}";
        }

        var counterResult = await _counters.FindOneAndUpdateAsync<CounterDoc>(
            Builders<CounterDoc>.Filter.Eq(c => c.Id, "url_id"),
            Builders<CounterDoc>.Update.Inc(c => c.Seq, 1),
            new FindOneAndUpdateOptions<CounterDoc> { ReturnDocument = ReturnDocument.After }, ct);

        var newId = (counterResult?.Seq) ?? 1;
        var shortCode = Base62Converter.Encode(newId);

        await _urlMappings.InsertOneAsync(new UrlMapping
        {
            Id = newId, ShortCode = shortCode, LongUrl = longUrl, CreatedAt = DateTime.UtcNow
        }, cancellationToken: ct);

        sw.Stop();
        _shortenDuration.Record(sw.Elapsed.TotalMilliseconds);
        return $"{_baseUrl}/{shortCode}";
    }

    public async Task RecordClickAsync(string shortCode, CancellationToken ct = default)
    {
        await _clicks.InsertOneAsync(
            new ClickEvent { ShortCode = shortCode, Timestamp = DateTime.UtcNow }, cancellationToken: ct);
    }
}
