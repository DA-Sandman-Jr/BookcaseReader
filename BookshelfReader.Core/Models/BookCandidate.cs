namespace BookshelfReader.Core.Models;

public sealed class BookCandidate
{
    public Rect BoundingBox { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public List<string> Genres { get; set; } = new();
    public double Confidence { get; set; }
    public string RawText { get; set; } = string.Empty;
    public List<string> Notes { get; set; } = new();
}
