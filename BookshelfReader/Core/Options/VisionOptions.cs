namespace BookshelfReader.Core.Options;

/// <summary>
/// Top-level vision pipeline selector. Chooses which provider's vision client
/// reads book spines; the selected provider's own option section
/// (<c>ClaudeVision</c>, <c>OpenAIVision</c>, or <c>GeminiVision</c>) supplies
/// the API key, model, and request limits. Defaults to
/// <see cref="VisionProvider.Claude"/> so existing configurations keep working
/// unchanged.
/// </summary>
public sealed class VisionOptions
{
    public const string SectionName = "Vision";

    public VisionProvider Provider { get; set; } = VisionProvider.Claude;
}
