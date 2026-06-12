using System.Diagnostics;
using System.Diagnostics.Metrics;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using UrlShortener.Api.Models;

namespace UrlShortener.Api.Services;

public class UrlShortenerService : IUrlShortenerService
{
    private readonly IMongoCollection<UrlMapping> _urlMappings;
    private readonly IMongoCollection<ClickEvent> _clicks;
    private readonly IMongoCollection<CounterDoc> _counters;
    private readonly string _baseUrl;
    private readonly Counter<long> _shortenCounter;
    private readonly Counter<long> _redirectCounter;
    private readonly Histogram<double> _shortenDuration;
    private readonly Histogram<double> _redirectDuration;

    public UrlShortenerService(
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
        _shortenCounter = meter.CreateCounter<long>("urlshortener.shorten.count");
        _redirectCounter = meter.CreateCounter<long>("urlshortener.redirect.count");
        _shortenDuration = meter.CreateHistogram<double>("urlshortener.shorten.duration",
            unit: "ms");
        _redirectDuration = meter.CreateHistogram<double>("urlshortener.redirect.duration",
            unit: "ms");
    }

    public async Task<string> ShortenAsync(string longUrl, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        // Idempotency: check if already shortened
        var existing = await _urlMappings
            .Find(m => m.LongUrl == longUrl)
            .FirstOrDefaultAsync(ct);

        if (existing is not null)
        {
            sw.Stop();
            _shortenCounter.Add(1);
            _shortenDuration.Record(sw.Elapsed.TotalMilliseconds);
            return $"{_baseUrl}/{existing.ShortCode}";
        }

        // Generate unique ID atomically
        var counterResult = await _counters.FindOneAndUpdateAsync<CounterDoc>(
            Builders<CounterDoc>.Filter.Eq(c => c.Id, "url_id"),
            Builders<CounterDoc>.Update.Inc(c => c.Seq, 1),
            new FindOneAndUpdateOptions<CounterDoc> { ReturnDocument = ReturnDocument.After },
            ct);

        var newId = (counterResult?.Seq) ?? 1;
        var shortCode = Base62Converter.Encode(newId);

        // Persist
        var mapping = new UrlMapping
        {
            Id = newId,
            ShortCode = shortCode,
            LongUrl = longUrl,
            CreatedAt = DateTime.UtcNow
        };
        await _urlMappings.InsertOneAsync(mapping, cancellationToken: ct);

        sw.Stop();
        _shortenCounter.Add(1);
        _shortenDuration.Record(sw.Elapsed.TotalMilliseconds);

        return $"{_baseUrl}/{shortCode}";
    }

    public async Task<string?> GetLongUrlAsync(string shortCode, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        var mapping = await _urlMappings
            .Find(m => m.ShortCode == shortCode)
            .FirstOrDefaultAsync(ct);

        if (mapping is null)
        {
            sw.Stop();
            _redirectDuration.Record(sw.Elapsed.TotalMilliseconds);
            return null;
        }

        // Record click event for analytics
        await _clicks.InsertOneAsync(
            new ClickEvent { ShortCode = shortCode, Timestamp = DateTime.UtcNow },
            cancellationToken: ct);

        sw.Stop();
        _redirectCounter.Add(1);
        _redirectDuration.Record(sw.Elapsed.TotalMilliseconds);

        return mapping.LongUrl;
    }

    /// <summary>
    /// Internal model for the atomic counter collection.
    /// </summary>
    [BsonIgnoreExtraElements]
    private class CounterDoc
    {
        [BsonId]
        public string Id { get; set; } = string.Empty;

        [BsonElement("seq")]
        public long Seq { get; set; }
    }
}
