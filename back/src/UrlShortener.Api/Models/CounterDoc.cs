using MongoDB.Bson.Serialization.Attributes;

namespace UrlShortener.Api.Models;

[BsonIgnoreExtraElements]
public class CounterDoc
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("seq")]
    public long Seq { get; set; }
}
