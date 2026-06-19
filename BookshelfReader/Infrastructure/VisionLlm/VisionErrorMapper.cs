using System.Net;
using System.Text.Json;
using BookshelfReader.Core.Abstractions;

namespace BookshelfReader.Infrastructure.VisionLlm;

/// <summary>
/// Maps a non-success HTTP response from a vision provider to a
/// <see cref="VisionBookReaderException"/> with a caller-friendly message.
/// Shared by the OpenAI and Gemini readers so error wording is consistent
/// across providers.
/// </summary>
internal static class VisionErrorMapper
{
    public static VisionBookReaderException Map(HttpStatusCode statusCode, string responseBody)
    {
        string? apiMessage = TryExtractErrorMessage(responseBody);
        string suffix = string.IsNullOrWhiteSpace(apiMessage) ? string.Empty : $" {apiMessage}";

        return statusCode switch
        {
            HttpStatusCode.Unauthorized => new VisionBookReaderException(
                $"The vision model rejected the configured API key (401).{suffix}"),
            HttpStatusCode.Forbidden => new VisionBookReaderException(
                $"The vision model rejected the configured API key (403).{suffix}"),
            HttpStatusCode.TooManyRequests => new VisionBookReaderException(
                $"The vision model is rate limiting requests (429).{suffix}"),
            (HttpStatusCode)529 => new VisionBookReaderException(
                $"The vision model is temporarily overloaded (529).{suffix}"),
            _ when (int)statusCode >= 500 => new VisionBookReaderException(
                $"The vision model returned a server error ({(int)statusCode}).{suffix}"),
            _ => new VisionBookReaderException(
                $"The vision model request failed ({(int)statusCode}).{suffix}")
        };
    }

    private static string? TryExtractErrorMessage(string responseBody)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            if (document.RootElement.TryGetProperty("error", out JsonElement error))
            {
                // OpenAI: {"error":{"message":"..."}}; Gemini: {"error":{"message":"..."}}.
                if (error.ValueKind == JsonValueKind.Object
                    && error.TryGetProperty("message", out JsonElement messageElement))
                {
                    return messageElement.GetString();
                }

                // Some providers return error as a bare string.
                if (error.ValueKind == JsonValueKind.String)
                {
                    return error.GetString();
                }
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }
}
