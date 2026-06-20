namespace BookshelfReader.Core.Options;

/// <summary>
/// Selects which vision model provider reads book spines from an uploaded photo.
/// <see cref="Claude"/> is the default and preserves the package's original
/// behavior; <see cref="OpenAI"/> and <see cref="Gemini"/> let consumers bring
/// their own key for a provider they already have access to.
/// </summary>
public enum VisionProvider
{
    /// <summary>Anthropic Claude vision (default). Configured via <c>ClaudeVision</c>.</summary>
    Claude = 0,

    /// <summary>OpenAI vision-capable chat model. Configured via <c>OpenAIVision</c>.</summary>
    OpenAI = 1,

    /// <summary>Google Gemini multimodal model. Configured via <c>GeminiVision</c>.</summary>
    Gemini = 2,
}
