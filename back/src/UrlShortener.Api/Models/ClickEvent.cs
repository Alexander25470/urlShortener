using MongoDB.Bson.Serialization.Attributes;

namespace UrlShortener.Api.Models;

public class ClickEvent
{
    [BsonElement("shortCode")]
    public string ShortCode { get; set; } = string.Empty;

    [BsonElement("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
