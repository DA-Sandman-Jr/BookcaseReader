namespace BookshelfReader.Core.Options;

/// <summary>
/// Options for the OpenAI vision provider (Chat Completions API). Used only when
/// <c>Vision:Provider</c> is set to <c>OpenAI</c>.
/// </summary>
public sealed class OpenAIVisionOptions
{
    public const string SectionName = "OpenAIVision";

    /// <summary>
    /// OpenAI API key. If left empty, <c>AddBookshelfReader</c> falls back to the
    /// <c>OPENAI_API_KEY</c> environment variable.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.openai.com/";

    /// <summary>
    /// A vision-capable OpenAI chat model. Defaults to a current, low-cost model
    /// that accepts image inputs.
    /// </summary>
    public string Model { get; set; } = "gpt-4o-mini";

    public int MaxTokens { get; set; } = 2048;

    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Images are downscaled so their longest edge does not exceed this many
    /// pixels before being sent to the OpenAI API.
    /// </summary>
    public int MaxImageDimension { get; set; } = 1568;
}
