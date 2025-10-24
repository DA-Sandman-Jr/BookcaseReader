using System.Text.Json.Serialization;

namespace BookshelfReader.Core.Models.External;

public sealed class OpenLibrarySearchResult
{
    [JsonPropertyName("docs")]
    public List<OpenLibraryDoc> Docs { get; set; } = new();
}

public sealed class OpenLibraryDoc
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("author_name")]
    public List<string>? AuthorName { get; set; }

    [JsonPropertyName("first_publish_year")]
    public int? FirstPublishYear { get; set; }

    [JsonPropertyName("isbn")]
    public List<string>? Isbn { get; set; }

    [JsonPropertyName("cover_i")]
    public int? CoverId { get; set; }
}
