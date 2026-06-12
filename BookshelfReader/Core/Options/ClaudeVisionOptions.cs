namespace BookshelfReader.Core.Options;

public sealed class ClaudeVisionOptions
{
    public const string SectionName = "ClaudeVision";

    /// <summary>
    /// Anthropic API key. If left empty, <c>AddBookshelfReader</c> falls back to the
    /// <c>ANTHROPIC_API_KEY</c> environment variable.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.anthropic.com/";

    public string Model { get; set; } = "claude-haiku-4-5";

    public int MaxTokens { get; set; } = 2048;

    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Images are downscaled so their longest edge does not exceed this many
    /// pixels before being sent to the Claude API.
    /// </summary>
    public int MaxImageDimension { get; set; } = 1568;
}
