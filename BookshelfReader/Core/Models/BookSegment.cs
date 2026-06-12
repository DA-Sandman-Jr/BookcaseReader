namespace BookshelfReader.Core.Models;

public sealed class BookSegment
{
    public Rect BoundingBox { get; init; }
    public byte[] ImageData { get; init; } = Array.Empty<byte>();
}
