namespace BookshelfReader.Infrastructure.VisionLlm;

/// <summary>
/// Shared prompt text and media type used by the OpenAI and Gemini vision
/// readers. The Claude reader keeps its own prompt because it constrains output
/// with Anthropic's <c>output_config</c> JSON schema; OpenAI and Gemini instead
/// ask for the JSON shape directly in the prompt (alongside provider-native
/// JSON/structured-output settings), so the instruction is spelled out here.
/// </summary>
internal static class VisionPrompt
{
    public const string ImageMediaType = "image/jpeg";

    public const string PromptText =
        "You are looking at a photo of a bookshelf. Identify every book whose title " +
        "is clearly legible on its spine or cover. For each one, record its title, " +
        "its author if visible, and a confidence score from 0 to 1 for how certain " +
        "you are the title is correct. Skip books where the text is too blurry, too " +
        "small, obscured, or at too extreme an angle to read with confidence - do " +
        "not guess at titles you cannot read. Respond only with a JSON object of the " +
        "form {\"books\":[{\"title\":string,\"author\":string,\"confidence\":number}]} " +
        "and no other text.";
}
