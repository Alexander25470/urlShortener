namespace UrlShortener.Api.Services;

public record ClickBucket(DateTime Timestamp, long Count);
public record TopUrlStat(string ShortCode, string LongUrl, long ClickCount);
