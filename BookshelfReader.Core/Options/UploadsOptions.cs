namespace BookshelfReader.Core.Options;

public sealed class UploadsOptions
{
    public const string SectionName = "Uploads";
    public int MaxBytes { get; set; } = 10 * 1024 * 1024;
    public HashSet<string> AllowedContentTypes { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png"
    };
}
