using System.Text.Json.Serialization;

namespace BookshelfReader.Infrastructure.VisionLlm;

/// <summary>
/// Minimal model of an OpenAI <c>POST /v1/chat/completions</c> response - only
/// the fields <see cref="OpenAIVisionBookReader"/> needs.
/// </summary>
internal sealed class OpenAIChatResponse
{
    [JsonPropertyName("choices")]
    public List<OpenAIChoice> Choices { get; set; } = new();
}

internal sealed class OpenAIChoice
{
    [JsonPropertyName("message")]
    public OpenAIMessage? Message { get; set; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

internal sealed class OpenAIMessage
{
    [JsonPropertyName("content")]
    public string? Content { get; set; }
}
