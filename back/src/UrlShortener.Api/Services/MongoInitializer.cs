using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using UrlShortener.Api.Models;

namespace UrlShortener.Api.Services;

/// <summary>
/// Creates MongoDB indexes and initial collections on startup.
/// Idempotent: safe to run multiple times.
/// </summary>
public class MongoInitializer
{
    private readonly IMongoClient _mainClient;
    private readonly IMongoClient _analyticsClient;
    private readonly ILogger<MongoInitializer> _logger;

    public MongoInitializer(
        IMongoClient mainClient,
        IMongoClient analyticsClient,
        ILogger<MongoInitializer> logger)
    {
        _mainClient = mainClient;
        _analyticsClient = analyticsClient;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        try
        {
            await CreateIndexesAsync();
            await SeedCounterAsync();
            await CreateTimeSeriesCollectionAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize MongoDB. The app will start but may not function correctly.");
        }
    }

    private async Task CreateIndexesAsync()
    {
        var db = _mainClient.GetDatabase("urlshortener");
        var urlMappings = db.GetCollection<UrlMapping>("url_mappings");

        await urlMappings.Indexes.CreateManyAsync([
            new CreateIndexModel<UrlMapping>(
                Builders<UrlMapping>.IndexKeys.Ascending(m => m.ShortCode),
                new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<UrlMapping>(
                Builders<UrlMapping>.IndexKeys.Ascending(m => m.LongUrl),
                new CreateIndexOptions { Unique = true })
        ]);
    }

    private async Task SeedCounterAsync()
    {
        var db = _mainClient.GetDatabase("urlshortener");
        var counters = db.GetCollection<CounterDoc>("counters");

        try
        {
            await counters.InsertOneAsync(new CounterDoc { Id = "url_id", Seq = 0 });
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // Already initialized, ignore
        }
    }

    private async Task CreateTimeSeriesCollectionAsync()
    {
        var db = _analyticsClient.GetDatabase("urlshortener_analytics");

        try
        {
            await db.CreateCollectionAsync("clicks", new CreateCollectionOptions
            {
                TimeSeriesOptions = new TimeSeriesOptions(
                    timeField: "timestamp",
                    metaField: "shortCode",
                    granularity: TimeSeriesGranularity.Seconds)
            });
        }
        catch (MongoCommandException ex) when (ex.CodeName == "NamespaceExists")
        {
            // Already created, ignore
        }
    }
}