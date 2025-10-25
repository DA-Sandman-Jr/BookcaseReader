namespace BookshelfReader.Core.Models;

public sealed class ParseResult
{
    public Guid ImageId { get; set; }
    public List<BookCandidate> Books { get; set; } = new();
    public Diagnostics Diagnostics { get; set; } = new();
}

public sealed class Diagnostics
{
    public int SegmentCount { get; set; }
    public long ElapsedMs { get; set; }
    public List<string> Notes { get; set; } = new();
}
