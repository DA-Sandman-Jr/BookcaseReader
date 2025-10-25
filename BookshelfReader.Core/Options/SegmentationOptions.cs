namespace BookshelfReader.Core.Options;

public sealed class SegmentationOptions
{
    public const string SectionName = "Segmentation";
    public double MinAspectRatio { get; set; } = 0.1;
    public double MaxAspectRatio { get; set; } = 10.0;
    public double MinAreaFraction { get; set; } = 0.0025;
    public double MaxAreaFraction { get; set; } = 0.9;
    public int MaxSegments { get; set; } = 64;
    public int MaxImagePixels { get; set; } = 25_000_000;
}
