using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using OpenTelemetry.Metrics;
using Scalar.AspNetCore;
using UrlShortener.Api;
using UrlShortener.Api.Models;
using UrlShortener.Api.Services;
using UrlShortener.Api.Services.Commands;
using UrlShortener.Api.Services.Queries;

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
builder.Services.AddSingleton<IUrlShortenerCommand>(sp =>
{
    var urlMappings = sp.GetRequiredService<IMongoCollection<UrlMapping>>();
    var clicks = sp.GetRequiredService<IMongoCollection<ClickEvent>>();
    var mainClient = sp.GetRequiredService<IMongoClient>();
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<UrlShortenerOptions>>();
    var meterFactory = sp.GetRequiredService<System.Diagnostics.Metrics.IMeterFactory>();

    return new UrlShortenerCommand(urlMappings, clicks, mainClient, options.Value.BaseUrl, meterFactory);
});

builder.Services.AddSingleton<IUrlAnalyticsQuery>(sp =>
{
    var urlMappings = sp.GetRequiredService<IMongoCollection<UrlMapping>>();
    var clicks = sp.GetRequiredService<IMongoCollection<ClickEvent>>();
    return new UrlAnalyticsQuery(urlMappings, clicks);
});

builder.Services.AddSingleton<IUrlMappingQuery>(sp =>
{
    var urlMappings = sp.GetRequiredService<IMongoCollection<UrlMapping>>();
    var meterFactory = sp.GetRequiredService<System.Diagnostics.Metrics.IMeterFactory>();
    return new UrlMappingQuery(urlMappings, meterFactory);
});

// ── MongoDB initializer ────────────────────────────────────────
builder.Services.AddSingleton<MongoInitializer>(sp =>
{
    var main = sp.GetRequiredService<IMongoClient>();
    var analytics = sp.GetRequiredKeyedService<IMongoClient>("analytics");
    var logger = sp.GetRequiredService<ILogger<MongoInitializer>>();
    return new MongoInitializer(main, analytics, logger);
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

// ── Initialize MongoDB ─────────────────────────────────────────
await app.Services.GetRequiredService<MongoInitializer>().InitializeAsync();

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
