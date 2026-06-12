namespace BookshelfReader.Core.Options;

public sealed class ParseRateLimitOptions
{
    public const string SectionName = "RateLimiting:Parse";

    /// <summary>
    /// Off by default so existing consumers are unaffected; the parse endpoint
    /// only requires the rate-limit policy when this is enabled, and the host
    /// must register it via AddBookshelfReaderRateLimiting and UseRateLimiter.
    /// </summary>
    public bool Enabled { get; set; }

    public int PermitLimit { get; set; } = 10;

    public int WindowSeconds { get; set; } = 60;

    public int QueueLimit { get; set; }
}
