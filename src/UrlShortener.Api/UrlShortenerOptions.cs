namespace UrlShortener.Api;

public class UrlShortenerOptions
{
    public const string SectionName = "UrlShortener";

    /// <summary>
    /// Base URL for constructing short URLs (e.g. "http://localhost:8080").
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:8080";

    /// <summary>
    /// HTTP status code for redirects: 301 (permanent) or 302 (temporary).
    /// Configurable via REDIRECT_TYPE env var.
    /// </summary>
    public int RedirectType { get; set; } = 301;
}
