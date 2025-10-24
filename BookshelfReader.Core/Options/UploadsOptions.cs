namespace BookshelfReader.Core.Options;

public sealed class UploadsOptions
{
    private static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;
    private HashSet<string> _allowedContentTypes = new(Comparer)
    {
        "image/jpeg",
        "image/png"
    };

    public const string SectionName = "Uploads";

    public int MaxBytes { get; set; } = 10 * 1024 * 1024;

    public HashSet<string> AllowedContentTypes
    {
        get => _allowedContentTypes;
        set
        {
            if (value is null)
            {
                _allowedContentTypes = new HashSet<string>(Comparer);
                return;
            }

            _allowedContentTypes = value.Count == 0
                ? new HashSet<string>(Comparer)
                : new HashSet<string>(value, Comparer);
        }
    }
}
