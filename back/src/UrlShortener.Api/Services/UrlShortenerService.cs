using System.Diagnostics;
using System.Diagnostics.Metrics;
using MongoDB.Bson;
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

    // ── Analytics ───────────────────────────────────────────────

    public async Task<IReadOnlyList<UrlMapping>> GetAllMappingsAsync(CancellationToken ct = default)
    {
        return await _urlMappings
            .Find(Builders<UrlMapping>.Filter.Empty)
            .SortByDescending(m => m.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ClickBucket>> GetClickStatsAsync(
        string shortCode, DateTime from, DateTime to, string bucket, CancellationToken ct = default)
    {
        var unit = bucket switch
        {
            "minute" => "minute",
            "hour" => "hour",
            "day" => "day",
            _ => "hour"
        };

        var pipeline = new BsonDocument[]
        {
            new("$match", new BsonDocument
            {
                { "shortCode", shortCode },
                { "timestamp", new BsonDocument
                    {
                        { "$gte", from },
                        { "$lte", to }
                    }
                }
            }),
            new("$group", new BsonDocument
            {
                { "_id", new BsonDocument("$dateTrunc", new BsonDocument
                    {
                        { "date", "$timestamp" },
                        { "unit", unit }
                    })
                },
                { "count", new BsonDocument("$sum", 1) }
            }),
            new("$sort", new BsonDocument("_id", 1))
        };

        var docs = await _clicks
            .Aggregate<BsonDocument>(pipeline, cancellationToken: ct)
            .ToListAsync(ct);

        return docs.Select(d => new ClickBucket(
            d["_id"].ToUniversalTime(),
            d["count"].ToInt64()))
            .ToList();
    }

    public async Task<IReadOnlyList<TopUrlStat>> GetTopUrlsAsync(
        int limit, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var pipeline = new BsonDocument[]
        {
            new("$match", new BsonDocument
            {
                { "timestamp", new BsonDocument
                    {
                        { "$gte", from },
                        { "$lte", to }
                    }
                }
            }),
            new("$group", new BsonDocument
            {
                { "_id", "$shortCode" },
                { "clickCount", new BsonDocument("$sum", 1) }
            }),
            new("$sort", new BsonDocument("clickCount", -1)),
            new("$limit", limit)
        };

        var aggregated = await _clicks
            .Aggregate<BsonDocument>(pipeline, cancellationToken: ct)
            .ToListAsync(ct);

        if (aggregated.Count == 0)
            return Array.Empty<TopUrlStat>();

        var shortCodes = aggregated.Select(d => d["_id"].AsString).ToList();
        var mappings = await _urlMappings
            .Find(m => shortCodes.Contains(m.ShortCode))
            .ToListAsync(ct);

        var urlMap = mappings.ToDictionary(m => m.ShortCode, m => m.LongUrl);

        return aggregated.Select(d => new TopUrlStat(
            d["_id"].AsString,
            urlMap.GetValueOrDefault(d["_id"].AsString, "unknown"),
            d["clickCount"].ToInt64()))
            .ToList();
    }
}
