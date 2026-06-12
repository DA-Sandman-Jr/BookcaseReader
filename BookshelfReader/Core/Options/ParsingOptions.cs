namespace BookshelfReader.Core.Options;

public sealed class ParsingOptions
{
    public const string SectionName = "Parsing";
    public double BaseConfidence { get; set; } = 0.35;
    public string[] CommonAuthorTokens { get; set; } = new[] { "by", "author", "edited by" };
}
