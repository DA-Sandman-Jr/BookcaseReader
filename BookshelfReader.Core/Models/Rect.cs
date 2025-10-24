namespace BookshelfReader.Core.Models;

/// <summary>
/// Represents an axis-aligned rectangle using integer coordinates.
/// </summary>
public readonly record struct Rect(int X, int Y, int Width, int Height)
{
    public bool IsEmpty => Width <= 0 || Height <= 0;
}
