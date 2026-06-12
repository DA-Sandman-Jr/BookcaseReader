namespace BookshelfReader.Core.Models;

public sealed class OcrResult
{
    public string Text { get; init; } = string.Empty;
    public double Confidence { get; init; }
    public IReadOnlyList<string> Attempts { get; init; } = Array.Empty<string>();
}
