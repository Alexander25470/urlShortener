using MongoDB.Bson;
using MongoDB.Driver;
using UrlShortener.Api.Models;

namespace UrlShortener.Api.Services.Queries;

public class UrlAnalyticsQuery : IUrlAnalyticsQuery
{
    private readonly IMongoCollection<UrlMapping> _urlMappings;
    private readonly IMongoCollection<ClickEvent> _clicks;

    public UrlAnalyticsQuery(
        IMongoCollection<UrlMapping> urlMappings,
        IMongoCollection<ClickEvent> clicks)
    {
        _urlMappings = urlMappings;
        _clicks = clicks;
    }

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
                { "timestamp", new BsonDocument { { "$gte", from }, { "$lte", to } } }
            }),
            new("$group", new BsonDocument
            {
                { "_id", new BsonDocument("$dateTrunc", new BsonDocument { { "date", "$timestamp" }, { "unit", unit } }) },
                { "count", new BsonDocument("$sum", 1) }
            }),
            new("$sort", new BsonDocument("_id", 1))
        };

        var docs = await _clicks.Aggregate<BsonDocument>(pipeline, cancellationToken: ct).ToListAsync(ct);
        return docs.Select(d => new ClickBucket(d["_id"].ToUniversalTime(), d["count"].ToInt64())).ToList();
    }

    public async Task<IReadOnlyList<TopUrlStat>> GetTopUrlsAsync(
        int limit, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var pipeline = new BsonDocument[]
        {
            new("$match", new BsonDocument
            {
                { "timestamp", new BsonDocument { { "$gte", from }, { "$lte", to } } }
            }),
            new("$group", new BsonDocument
            {
                { "_id", "$shortCode" },
                { "clickCount", new BsonDocument("$sum", 1) }
            }),
            new("$sort", new BsonDocument("clickCount", -1)),
            new("$limit", limit)
        };

        var aggregated = await _clicks.Aggregate<BsonDocument>(pipeline, cancellationToken: ct).ToListAsync(ct);
        if (aggregated.Count == 0) return Array.Empty<TopUrlStat>();

        var shortCodes = aggregated.Select(d => d["_id"].AsString).ToList();
        var mappings = await _urlMappings.Find(m => shortCodes.Contains(m.ShortCode)).ToListAsync(ct);
        var urlMap = mappings.ToDictionary(m => m.ShortCode, m => m.LongUrl);

        return aggregated.Select(d => new TopUrlStat(
            d["_id"].AsString, urlMap.GetValueOrDefault(d["_id"].AsString, "unknown"), d["clickCount"].ToInt64())).ToList();
    }

}
