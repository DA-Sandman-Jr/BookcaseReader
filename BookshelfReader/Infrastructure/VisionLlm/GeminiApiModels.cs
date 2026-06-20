using System.Text.Json.Serialization;

namespace BookshelfReader.Infrastructure.VisionLlm;

/// <summary>
/// Minimal model of a Gemini <c>generateContent</c> response - only the fields
/// <see cref="GeminiVisionBookReader"/> needs.
/// </summary>
internal sealed class GeminiGenerateContentResponse
{
    [JsonPropertyName("candidates")]
    public List<GeminiCandidate> Candidates { get; set; } = new();
}

internal sealed class GeminiCandidate
{
    [JsonPropertyName("content")]
    public GeminiContent? Content { get; set; }

    [JsonPropertyName("finishReason")]
    public string? FinishReason { get; set; }
}

internal sealed class GeminiContent
{
    [JsonPropertyName("parts")]
    public List<GeminiPart> Parts { get; set; } = new();
}

internal sealed class GeminiPart
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }
}
