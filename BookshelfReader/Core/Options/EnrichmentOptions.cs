namespace BookshelfReader.Core.Options;

public sealed class EnrichmentOptions
{
    public const string SectionName = "Enrichment";

    public bool Enabled { get; set; } = true;

    public int MaxConcurrentLookups { get; set; } = 4;

    /// <summary>
    /// Minimum fuzzy-match score (0-100) between the parsed title and a catalog
    /// result before metadata is attached to the candidate.
    /// </summary>
    public int MinMatchScore { get; set; } = 55;
}
