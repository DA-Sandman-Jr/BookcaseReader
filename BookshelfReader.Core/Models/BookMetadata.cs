namespace BookshelfReader.Core.Models;

public sealed class BookMetadata
{
    public string Title { get; set; } = string.Empty;
    public string? Author { get; set; }
    public int? PublishYear { get; set; }
    public string? Isbn { get; set; }
    public string? CoverUrl { get; set; }
}
