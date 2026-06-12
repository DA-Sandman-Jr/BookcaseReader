using System.Text.Json.Serialization;

namespace BookshelfReader.Infrastructure.VisionLlm;

/// <summary>
/// Minimal model of an Anthropic <c>POST /v1/messages</c> response - only the
/// fields <see cref="ClaudeVisionBookReader"/> needs.
/// </summary>
internal sealed class MessagesResponse
{
    [JsonPropertyName("content")]
    public List<ContentBlockResponse> Content { get; set; } = new();

    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; set; }
}

internal sealed class ContentBlockResponse
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

/// <summary>
/// The structured-output payload requested via <c>output_config.format</c>.
/// </summary>
internal sealed class BooksPayload
{
    [JsonPropertyName("books")]
    public List<BookEntryPayload> Books { get; set; } = new();
}

internal sealed class BookEntryPayload
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("author")]
    public string Author { get; set; } = string.Empty;

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }
}
