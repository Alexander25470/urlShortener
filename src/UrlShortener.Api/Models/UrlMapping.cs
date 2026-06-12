using MongoDB.Bson.Serialization.Attributes;

namespace UrlShortener.Api.Models;

public class UrlMapping
{
    [BsonId]
    public long Id { get; set; }

    [BsonElement("shortCode")]
    public string ShortCode { get; set; } = string.Empty;

    [BsonElement("longUrl")]
    public string LongUrl { get; set; } = string.Empty;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
