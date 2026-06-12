namespace BookshelfReader.Core.Abstractions;

/// <summary>
/// Thrown when <see cref="IVisionBookReader.ReadAsync"/> cannot obtain a usable
/// result from the vision model: an authentication/configuration problem, a
/// transient failure (rate limiting, overload, timeout, network error), or a
/// response that could not be understood. Callers should treat this as a
/// server-side failure, distinct from <see cref="InvalidOperationException"/>
/// which signals an unusable input image.
/// </summary>
public sealed class VisionBookReaderException : Exception
{
    public VisionBookReaderException(string message)
        : base(message)
    {
    }

    public VisionBookReaderException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
