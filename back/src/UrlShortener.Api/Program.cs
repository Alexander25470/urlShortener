using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using OpenTelemetry.Metrics;
using Scalar.AspNetCore;
using UrlShortener.Api;
using UrlShortener.Api.Models;
using UrlShortener.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Configuration ──────────────────────────────────────────────
builder.Services.Configure<UrlShortenerOptions>(
    builder.Configuration.GetSection(UrlShortenerOptions.SectionName));

// ── MongoDB - two separate instances for cache isolation ───────
var mainConnectionString = builder.Configuration["MongoDB:MainConnectionString"]
    ?? "mongodb://localhost:27017";
var analyticsConnectionString = builder.Configuration["MongoDB:AnalyticsConnectionString"]
    ?? "mongodb://localhost:27018";

builder.Services.AddSingleton<IMongoClient>(_ =>
    new MongoClient(mainConnectionString));
builder.Services.AddKeyedSingleton<IMongoClient>("analytics", (_, _) =>
    new MongoClient(analyticsConnectionString));

builder.Services.AddSingleton(sp =>
{
    var client = sp.GetRequiredService<IMongoClient>();
    return client.GetDatabase("urlshortener").GetCollection<UrlMapping>("url_mappings");
});
builder.Services.AddSingleton(sp =>
{
    var client = sp.GetRequiredKeyedService<IMongoClient>("analytics");
    return client.GetDatabase("urlshortener_analytics").GetCollection<ClickEvent>("clicks");
});

// ── Services ───────────────────────────────────────────────────
builder.Services.AddSingleton<IUrlShortenerService>(sp =>
{
    var urlMappings = sp.GetRequiredService<IMongoCollection<UrlMapping>>();
    var clicks = sp.GetRequiredService<IMongoCollection<ClickEvent>>();
    var mainClient = sp.GetRequiredService<IMongoClient>();
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<UrlShortenerOptions>>();
    var meterFactory = sp.GetRequiredService<System.Diagnostics.Metrics.IMeterFactory>();

    return new UrlShortenerService(
        urlMappings,
        clicks,
        mainClient,
        options.Value.BaseUrl,
        meterFactory);
});

// ── Controllers & OpenAPI ──────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

// ── CORS ───────────────────────────────────────────────────────
var corsOrigins = builder.Configuration.GetSection("Cors:Origins")
    .Get<string[]>() ?? ["http://localhost"];

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(corsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// ── OpenTelemetry Metrics ──────────────────────────────────────
builder.Services.AddOpenTelemetry()
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddMeter("urlshortener")
        .AddPrometheusExporter());

var app = builder.Build();

// ── Initialize MongoDB (idempotent, best-effort) ───────────────
await InitializeMongoAsync(app.Services);

// ── Middleware pipeline ────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseExceptionHandler();
app.UseCors();
app.MapPrometheusScrapingEndpoint();

app.MapControllers();

app.Run();

// ── MongoDB initialization ─────────────────────────────────────
static async Task InitializeMongoAsync(IServiceProvider services)
{
    try
    {
        // Create indexes on main database
        var mainClient = services.GetRequiredService<IMongoClient>();
        var mainDb = mainClient.GetDatabase("urlshortener");
        var urlMappings = mainDb.GetCollection<UrlMapping>("url_mappings");

        await urlMappings.Indexes.CreateManyAsync([
            new CreateIndexModel<UrlMapping>(
                Builders<UrlMapping>.IndexKeys.Ascending(m => m.ShortCode),
                new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<UrlMapping>(
                Builders<UrlMapping>.IndexKeys.Ascending(m => m.LongUrl),
                new CreateIndexOptions { Unique = true })
        ]);

        // Initialize counter document if not exists
        var counters = mainDb.GetCollection<CounterDoc>("counters");
        try
        {
            await counters.InsertOneAsync(new CounterDoc { Id = "url_id", Seq = 0 });
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // Already initialized, ignore
        }

        // Create Time Series Collection on analytics database
        var analyticsClient = services.GetRequiredKeyedService<IMongoClient>("analytics");
        var analyticsDb = analyticsClient.GetDatabase("urlshortener_analytics");
        try
        {
            await analyticsDb.CreateCollectionAsync("clicks", new CreateCollectionOptions
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
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Failed to initialize MongoDB. The app will start but may not function correctly.");
    }
}

/// <summary>
/// Internal model for the atomic counter collection.
/// </summary>
[BsonIgnoreExtraElements]
internal class CounterDoc
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("seq")]
    public long Seq { get; set; }
}
